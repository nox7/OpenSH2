using UnityEngine;

/// <summary>
/// Responsible for converting BIK videos into MP4 using FFMPEG
/// by invoking the process and passing the appropriate arguments to it.
/// This is necessary because we cannot ship an open-source project with propietary video codecs, and BIK is a proprietary format. 
/// By converting to MP4, we can ensure compatibility across different platforms and devices without relying on proprietary codecs.
/// </summary>
public class Converter
{
  /// <summary>
  /// Converts an input file path of a BIK video to an output file path of an MP4 video using FFMPEG.
  /// </summary>
  /// <param name="inputFilePath">The file path of the input BIK video.</param>
  /// <param name="outputFilePath">The file path of the output MP4 video.</param>
  public static void ConvertToMP4(string inputFilePath, string outputFilePath)
  {
    string locationOfFFMPEG = Application.dataPath + "/External/ffmpeg-windows/bin/ffmpeg.exe";
    string ffmpegArguments = $"-fflags +genpts -i \"{inputFilePath}\" -vsync cfr -r 30 -c:v libx264 -profile:v baseline -level 3.1 -pix_fmt yuv420p -movflags +faststart+write_colr -color_primaries 1 -color_trc 1 -colorspace 1 -c:a aac -ar 48000 -ac 2 -b:a 128k \"{outputFilePath}\"";

    System.Diagnostics.Process process = new();
    process.StartInfo.FileName = locationOfFFMPEG;
    process.StartInfo.Arguments = ffmpegArguments;
    process.StartInfo.RedirectStandardOutput = true;
    process.StartInfo.RedirectStandardError = true;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.CreateNoWindow = true;

    System.Text.StringBuilder outputBuilder = new();
    System.Text.StringBuilder errorBuilder = new();

    process.OutputDataReceived += (_, e) =>
    {
      if (!string.IsNullOrEmpty(e.Data))
      {
        outputBuilder.AppendLine(e.Data);
      }
    };

    process.ErrorDataReceived += (_, e) =>
    {
      if (!string.IsNullOrEmpty(e.Data))
      {
        errorBuilder.AppendLine(e.Data);
      }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    const int timeoutMs = 30 * 60 * 1000;
    if (!process.WaitForExit(timeoutMs))
    {
      process.Kill();
      Debug.LogError("FFMPEG conversion timed out and was terminated.");
      return;
    }

    string output = outputBuilder.ToString();
    string error = errorBuilder.ToString();

    if (process.ExitCode != 0)
    {
      Debug.LogError($"FFMPEG conversion failed: {error}");
    }
    else
    {
      Debug.Log($"FFMPEG conversion succeeded: {output}");
    }
  }
}
