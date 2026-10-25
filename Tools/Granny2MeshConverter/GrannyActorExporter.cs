using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenSH2.Granny2MeshConverter
{
  // Runs only in the existing isolated x86 converter, never inside Unity.
  internal sealed partial class GrannyNative
  {
    public void ExportActor(string modelPath, string animationPath, string output, int fps)
    {
      if (fps < 1 || fps > 60) throw new ArgumentOutOfRangeException(nameof(fps));
      IntPtr modelFile = readEntireFile(modelPath), animationFile = IntPtr.Zero;
      IntPtr instance = IntPtr.Zero, local = IntPtr.Zero, world = IntPtr.Zero, control = IntPtr.Zero;
      string temporary = output + ".tmp";
      try
      {
        if (modelFile == IntPtr.Zero) throw new InvalidDataException("Cannot read actor model.");
        animationFile = readEntireFile(animationPath);
        if (animationFile == IntPtr.Zero) throw new InvalidDataException("Cannot read actor animation.");
        var info = Marshal.PtrToStructure<GrannyFileInfo32>(getFileInfo(modelFile));
        var animations = Marshal.PtrToStructure<GrannyFileInfo32>(getFileInfo(animationFile));
        if (info.ModelCount != 1)
          throw new InvalidDataException("Actor export requires one model and an animation.");
        IntPtr model = Marshal.ReadIntPtr(info.Models);
        IntPtr skeleton = Marshal.ReadIntPtr(model, 4);
        int bones = Marshal.ReadInt32(skeleton, 4);
        if (bones < 1 || bones > 1024) throw new InvalidDataException("Invalid actor skeleton.");
        IntPtr animation = animations.AnimationCount > 0 ? Marshal.ReadIntPtr(animations.Animations) : IntPtr.Zero;
        float duration = animation != IntPtr.Zero ? Marshal.PtrToStructure<AnimationHeader>(animation).Duration : 1;
        if (!(duration > 0) || duration > 600) throw new InvalidDataException("Invalid clip duration.");
        int frames = animation == IntPtr.Zero ? 2 : Math.Max(2, (int)Math.Ceiling(duration * fps) + 1);
        instance = GetFunction<Pointer1>("_GrannyInstantiateModel@4")(model);
        local = GetFunction<NewPose>("_GrannyNewLocalPose@4")(bones);
        world = GetFunction<NewPose>("_GrannyNewWorldPose@4")(bones);
        if (instance == IntPtr.Zero || local == IntPtr.Zero || world == IntPtr.Zero) throw new InvalidOperationException("Cannot allocate actor pose.");
        if (animation != IntPtr.Zero)
        {
          control = GetFunction<PlayAnimation>("_GrannyPlayControlledAnimation@12")(0, animation, instance);
          if (control == IntPtr.Zero) throw new InvalidDataException("Animation cannot bind to this model.");
          GetFunction<SetLoopCount>("_GrannySetControlLoopCount@8")(control, 0);
        }
        var clock = GetFunction<SetClock>("_GrannySetModelClock@8");
        var sample = GetFunction<SampleAnimations>("_GrannySampleModelAnimations@16");
        var build = GetFunction<BuildWorld>("_GrannyBuildWorldPose@24");
        var matrices = GetFunction<Pointer1>("_GrannyGetWorldPoseComposite4x4Array@4");
        var worldMatrices = GetFunction<Pointer1>("_GrannyGetWorldPose4x4Array@4");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        using (var writer = new BinaryWriter(File.Create(temporary), Encoding.UTF8))
        {
          writer.Write(Encoding.ASCII.GetBytes("OSH2ACT\0")); writer.Write(2);
          writer.Write(duration); writer.Write(bones * 2); writer.Write(frames);
          writer.Write(bones);
          IntPtr boneArray = Marshal.ReadIntPtr(skeleton, 8);
          for (int b = 0; b < bones; b++) writer.Write(ReadString(Marshal.ReadIntPtr(boneArray, b * 152)));
          int drawable = 0;
          for (int m = 0; m < info.MeshCount; m++)
            if (getMeshIndexCount(Marshal.ReadIntPtr(info.Meshes, m * 4)) > 0) drawable++;
          writer.Write(drawable);
          IntPtr vertexType = Marshal.ReadIntPtr(GetRequiredExport("GrannyPWNT3432VertexType"));
          var bindingNew = GetFunction<NewBinding>("_GrannyNewMeshBinding@12");
          var bindingMap = GetFunction<Pointer1>("_GrannyGetMeshBindingToBoneIndices@4");
          var bindingFree = GetFunction<FreeFileDelegate>("_GrannyFreeMeshBinding@4");
          for (int m = 0; m < info.MeshCount; m++)
          {
            IntPtr mesh = Marshal.ReadIntPtr(info.Meshes, m * 4);
            if (getMeshIndexCount(mesh) == 0) continue;
            int vertices = getMeshVertexCount(mesh);
            var meshInfo = Marshal.PtrToStructure<GrannyMesh32>(mesh);
            if (meshInfo.BoneBindingCount < 1) throw new InvalidDataException("Actor mesh has no bone bindings.");
            IntPtr binding = bindingNew(mesh, skeleton, skeleton);
            if (binding == IntPtr.Zero) throw new InvalidDataException("Cannot bind actor mesh.");
            IntPtr buffer = Marshal.AllocHGlobal(checked(vertices * 40));
            try
            {
              copyMeshVertices(mesh, vertexType, buffer);
              IntPtr map = bindingMap(binding);
              writer.Write(vertices);
              for (int v = 0; v < vertices; v++)
              {
                // PWNT3432: position, four byte weights, four byte indices, normal, UV.
                int sum = 0;
                for (int w = 0; w < 4; w++) sum += Marshal.ReadByte(buffer, v * 40 + 12 + w);
                for (int w = 0; w < 4; w++)
                {
                  int weight = Marshal.ReadByte(buffer, v * 40 + 12 + w);
                  int index = Marshal.ReadByte(buffer, v * 40 + 16 + w);
                  if (sum == 0) { weight = w == 0 ? 255 : 0; index = 0; }
                  int bone = weight == 0 ? 0 : index < meshInfo.BoneBindingCount ? Marshal.ReadInt32(map, index * 4) : -1;
                  if (bone < 0 || bone >= bones) throw new InvalidDataException("Actor mesh has an invalid bone binding.");
                  writer.Write(bone); writer.Write(weight / (float)(sum == 0 ? 255 : sum));
                }
              }
            }
            finally { Marshal.FreeHGlobal(buffer); bindingFree(binding); }
          }
          var data = new float[bones * 16];
          var worldData = new float[data.Length];
          for (int f = 0; f < frames; f++)
          {
            if (animation != IntPtr.Zero)
            {
              clock(instance, Math.Min(duration - .00001f, duration * f / (frames - 1)));
              sample(instance, 0, bones, local);
              build(skeleton, 0, bones, local, IntPtr.Zero, world);
            }
            if (animation != IntPtr.Zero)
            {
              Marshal.Copy(matrices(world), data, 0, data.Length);
              Marshal.Copy(worldMatrices(world), worldData, 0, worldData.Length);
            }
            else
            {
              // A rigid placed model is already in bind space and needs no native pose evaluation.
              Array.Clear(data, 0, data.Length);
              for (int b = 0; b < bones; b++) for (int d = 0; d < 4; d++) data[b * 16 + d * 5] = 1;
              Array.Copy(data, worldData, data.Length);
            }
            foreach (float value in data)
            {
              if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite actor pose.");
              writer.Write(value);
            }
            foreach (float value in worldData)
            {
              if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite attachment pose.");
              writer.Write(value);
            }
          }
        }
        if (File.Exists(output)) File.Delete(output);
        File.Move(temporary, output);
      }
      finally
      {
        if (control != IntPtr.Zero) GetFunction<FreeFileDelegate>("_GrannyFreeControl@4")(control);
        if (instance != IntPtr.Zero) GetFunction<FreeFileDelegate>("_GrannyFreeModelInstance@4")(instance);
        if (local != IntPtr.Zero) GetFunction<FreeFileDelegate>("_GrannyFreeLocalPose@4")(local);
        if (world != IntPtr.Zero) GetFunction<FreeFileDelegate>("_GrannyFreeWorldPose@4")(world);
        if (animationFile != IntPtr.Zero) freeFile(animationFile);
        if (modelFile != IntPtr.Zero) freeFile(modelFile);
        if (File.Exists(temporary)) File.Delete(temporary);
      }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)] private struct AnimationHeader { public IntPtr Name; public float Duration; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr Pointer1(IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr NewPose(int count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr PlayAnimation(float time, IntPtr animation, IntPtr instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void SetLoopCount(IntPtr control, int count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void SetClock(IntPtr model, float time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void SampleAnimations(IntPtr model, int first, int count, IntPtr pose);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void BuildWorld(IntPtr skeleton, int first, int count, IntPtr local, IntPtr offset, IntPtr world);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr NewBinding(IntPtr mesh, IntPtr from, IntPtr to);
  }
}
