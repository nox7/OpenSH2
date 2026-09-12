using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenSH2.Granny2MeshConverter
{
  internal sealed class GrannyNative : IDisposable
  {
    private const int FloatsPerVertex = 8;
    private readonly IntPtr library;
    private readonly ReadEntireFileDelegate readEntireFile;
    private readonly GetFileInfoDelegate getFileInfo;
    private readonly FreeFileDelegate freeFile;
    private readonly GetMeshIntDelegate getMeshVertexCount;
    private readonly GetMeshIntDelegate getMeshIndexCount;
    private readonly GetMeshIntDelegate getMeshBytesPerIndex;
    private readonly GetMeshIntDelegate getMeshTriangleGroupCount;
    private readonly GetMeshPointerDelegate getMeshTriangleGroups;
    private readonly CopyMeshVerticesDelegate copyMeshVertices;
    private readonly CopyMeshIndicesDelegate copyMeshIndices;
    private readonly IntPtr pnt332VertexType;

    public string Version { get; }

    public GrannyNative(string dllPath)
    {
      if (IntPtr.Size != 4) throw new PlatformNotSupportedException("The Stronghold 2 Granny runtime requires an x86 converter process.");
      library = LoadLibrary(dllPath);
      if (library == IntPtr.Zero) throw new InvalidOperationException($"Could not load the installed Granny runtime: {dllPath} (Win32 error {Marshal.GetLastWin32Error()}).");

      readEntireFile = GetFunction<ReadEntireFileDelegate>("_GrannyReadEntireFile@4");
      getFileInfo = GetFunction<GetFileInfoDelegate>("_GrannyGetFileInfo@4");
      freeFile = GetFunction<FreeFileDelegate>("_GrannyFreeFile@4");
      getMeshVertexCount = GetFunction<GetMeshIntDelegate>("_GrannyGetMeshVertexCount@4");
      getMeshIndexCount = GetFunction<GetMeshIntDelegate>("_GrannyGetMeshIndexCount@4");
      getMeshBytesPerIndex = GetFunction<GetMeshIntDelegate>("_GrannyGetMeshBytesPerIndex@4");
      getMeshTriangleGroupCount = GetFunction<GetMeshIntDelegate>("_GrannyGetMeshTriangleGroupCount@4");
      getMeshTriangleGroups = GetFunction<GetMeshPointerDelegate>("_GrannyGetMeshTriangleGroups@4");
      copyMeshVertices = GetFunction<CopyMeshVerticesDelegate>("_GrannyCopyMeshVertices@12");
      copyMeshIndices = GetFunction<CopyMeshIndicesDelegate>("_GrannyCopyMeshIndices@12");

      IntPtr typeExport = GetRequiredExport("GrannyPNT332VertexType");
      pnt332VertexType = Marshal.ReadIntPtr(typeExport);
      if (pnt332VertexType == IntPtr.Zero) throw new InvalidDataException("The Granny PNT332 vertex type is unavailable.");

      var getVersion = GetFunction<GetVersionStringDelegate>("_GrannyGetVersionString@0");
      Version = ReadString(getVersion());
    }

    public GrannyModel ReadModel(string filePath)
    {
      IntPtr file = readEntireFile(filePath);
      if (file == IntPtr.Zero) throw new InvalidDataException($"Granny could not read {filePath}.");
      try
      {
        IntPtr infoPointer = getFileInfo(file);
        if (infoPointer == IntPtr.Zero) throw new InvalidDataException("Granny returned no file information.");
        GrannyFileInfo32 info = Marshal.PtrToStructure<GrannyFileInfo32>(infoPointer);
        ValidateCount(info.MeshCount, "mesh");

        var model = new GrannyModel { ModelCount = info.ModelCount };
        for (int i = 0; i < info.MeshCount; i++)
        {
          IntPtr meshPointer = Marshal.ReadIntPtr(info.Meshes, i * IntPtr.Size);
          model.Meshes.Add(ReadMesh(meshPointer));
        }
        return model;
      }
      finally
      {
        freeFile(file);
      }
    }

    private GrannyMesh ReadMesh(IntPtr meshPointer)
    {
      if (meshPointer == IntPtr.Zero) throw new InvalidDataException("A Granny mesh pointer is null.");
      GrannyMesh32 nativeMesh = Marshal.PtrToStructure<GrannyMesh32>(meshPointer);
      int vertexCount = getMeshVertexCount(meshPointer);
      int indexCount = getMeshIndexCount(meshPointer);
      int sourceIndexSize = getMeshBytesPerIndex(meshPointer);
      int groupCount = getMeshTriangleGroupCount(meshPointer);
      ValidateCount(vertexCount, "vertex");
      ValidateCount(indexCount, "index");
      ValidateCount(groupCount, "triangle group");
      if (sourceIndexSize != 2 && sourceIndexSize != 4)
        throw new InvalidDataException($"Unsupported Granny index size {sourceIndexSize}.");

      var mesh = new GrannyMesh
      {
        Name = ReadString(nativeMesh.Name),
        Vertices = CopyVertices(meshPointer, vertexCount),
        Indices = CopyIndices(meshPointer, indexCount)
      };

      ValidateCount(nativeMesh.MaterialBindingCount, "material binding");
      for (int i = 0; i < nativeMesh.MaterialBindingCount; i++)
      {
        IntPtr materialPointer = Marshal.ReadIntPtr(nativeMesh.MaterialBindings, i * IntPtr.Size);
        mesh.Materials.Add(ReadMaterial(materialPointer));
      }

      IntPtr groups = getMeshTriangleGroups(meshPointer);
      for (int i = 0; i < groupCount; i++)
      {
        GrannyTriangleGroup32 group = Marshal.PtrToStructure<GrannyTriangleGroup32>(IntPtr.Add(groups, i * 12));
        if (group.TriangleFirst < 0 || group.TriangleCount < 0 || (group.TriangleFirst + group.TriangleCount) * 3 > indexCount)
          throw new InvalidDataException("A Granny triangle group lies outside its mesh index array.");
        mesh.Groups.Add(new GrannyTriangleGroup
        {
          MaterialIndex = group.MaterialIndex,
          TriangleFirst = group.TriangleFirst,
          TriangleCount = group.TriangleCount
        });
      }

      if (mesh.Groups.Count == 0 && indexCount > 0)
        mesh.Groups.Add(new GrannyTriangleGroup { MaterialIndex = 0, TriangleFirst = 0, TriangleCount = indexCount / 3 });
      return mesh;
    }

    private float[] CopyVertices(IntPtr mesh, int vertexCount)
    {
      int floatCount = checked(vertexCount * FloatsPerVertex);
      IntPtr buffer = Marshal.AllocHGlobal(checked(floatCount * sizeof(float)));
      try
      {
        copyMeshVertices(mesh, pnt332VertexType, buffer);
        var result = new float[floatCount];
        Marshal.Copy(buffer, result, 0, result.Length);
        return result;
      }
      finally { Marshal.FreeHGlobal(buffer); }
    }

    private int[] CopyIndices(IntPtr mesh, int indexCount)
    {
      IntPtr buffer = Marshal.AllocHGlobal(checked(indexCount * sizeof(int)));
      try
      {
        copyMeshIndices(mesh, sizeof(int), buffer);
        var result = new int[indexCount];
        Marshal.Copy(buffer, result, 0, result.Length);
        return result;
      }
      finally { Marshal.FreeHGlobal(buffer); }
    }

    private static GrannyMaterial ReadMaterial(IntPtr materialPointer)
    {
      if (materialPointer == IntPtr.Zero) return new GrannyMaterial();
      GrannyMaterial32 material = Marshal.PtrToStructure<GrannyMaterial32>(materialPointer);
      return new GrannyMaterial
      {
        Name = ReadString(material.Name),
        TexturePath = FindTexturePath(materialPointer, 0)
      };
    }

    private static string FindTexturePath(IntPtr materialPointer, int depth)
    {
      if (materialPointer == IntPtr.Zero || depth > 8) return string.Empty;
      GrannyMaterial32 material = Marshal.PtrToStructure<GrannyMaterial32>(materialPointer);
      if (material.Texture != IntPtr.Zero)
      {
        GrannyTexture32 texture = Marshal.PtrToStructure<GrannyTexture32>(material.Texture);
        string path = ReadString(texture.FromFileName);
        if (!string.IsNullOrWhiteSpace(path)) return path;
      }

      if (material.MapCount < 0 || material.MapCount > 128 || material.Maps == IntPtr.Zero) return string.Empty;
      for (int i = 0; i < material.MapCount; i++)
      {
        IntPtr childMaterial = Marshal.ReadIntPtr(material.Maps, i * 8 + 4);
        string path = FindTexturePath(childMaterial, depth + 1);
        if (!string.IsNullOrWhiteSpace(path)) return path;
      }
      return string.Empty;
    }

    private T GetFunction<T>(string name) where T : Delegate
    {
      return Marshal.GetDelegateForFunctionPointer<T>(GetRequiredExport(name));
    }

    private IntPtr GetRequiredExport(string name)
    {
      IntPtr address = GetProcAddress(library, name);
      if (address == IntPtr.Zero) throw new MissingMethodException($"The installed granny2.dll does not export {name}.");
      return address;
    }

    private static void ValidateCount(int value, string description)
    {
      if (value < 0 || value > 100000000) throw new InvalidDataException($"Invalid Granny {description} count {value}.");
    }

    private static string ReadString(IntPtr value) => value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(value) ?? string.Empty;

    public void Dispose()
    {
      if (library != IntPtr.Zero) FreeLibrary(library);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct GrannyFileInfo32
    {
      public IntPtr ArtToolInfo, ExporterInfo, FromFileName;
      public int TextureCount; public IntPtr Textures;
      public int MaterialCount; public IntPtr Materials;
      public int SkeletonCount; public IntPtr Skeletons;
      public int VertexDataCount; public IntPtr VertexDatas;
      public int TriTopologyCount; public IntPtr TriTopologies;
      public int MeshCount; public IntPtr Meshes;
      public int ModelCount; public IntPtr Models;
      public int TrackGroupCount; public IntPtr TrackGroups;
      public int AnimationCount; public IntPtr Animations;
      public IntPtr ExtendedDataType, ExtendedDataObject;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct GrannyMesh32
    {
      public IntPtr Name, PrimaryVertexData;
      public int MorphTargetCount; public IntPtr MorphTargets, PrimaryTopology;
      public int MaterialBindingCount; public IntPtr MaterialBindings;
      public int BoneBindingCount; public IntPtr BoneBindings, ExtendedDataType, ExtendedDataObject;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct GrannyMaterial32
    {
      public IntPtr Name;
      public int MapCount;
      public IntPtr Maps, Texture, ExtendedDataType, ExtendedDataObject;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct GrannyTexture32
    {
      public IntPtr FromFileName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct GrannyTriangleGroup32
    {
      public int MaterialIndex, TriangleFirst, TriangleCount;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr ReadEntireFileDelegate([MarshalAs(UnmanagedType.LPStr)] string path);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr GetFileInfoDelegate(IntPtr file);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void FreeFileDelegate(IntPtr file);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetMeshIntDelegate(IntPtr mesh);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr GetMeshPointerDelegate(IntPtr mesh);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void CopyMeshVerticesDelegate(IntPtr mesh, IntPtr vertexType, IntPtr destination);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void CopyMeshIndicesDelegate(IntPtr mesh, int bytesPerIndex, IntPtr destination);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr GetVersionStringDelegate();

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FreeLibrary(IntPtr module);
  }
}
