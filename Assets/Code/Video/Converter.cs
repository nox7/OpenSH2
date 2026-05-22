using UnityEngine;

/// <summary>
/// Responsible for converting BIK videos into playable cached formats using FFMPEG
/// by invoking the process and passing the appropriate arguments to it.
/// This is necessary because we cannot ship an open-source project with propietary video codecs, and BIK is a proprietary format.
/// </summary>
public class Converter
{
  public const string OutputFormatMp4 = "mp4";
  public const string OutputFormatWebM = "webm";

  /// <summary>
  /// Converts an input file path of a BIK video to an output file path in the requested output format.
  /// </summary>
  /// <param name="inputFilePath">The file path of the input BIK video.</param>
  /// <param name="outputFilePath">The file path of the output video.</param>
  /// <param name="outputFormat">The desired output format. Supported values are "mp4" and "webm".</param>
  public static void Convert(string inputFilePath, string outputFilePath, string outputFormat)
  {
    string locationOfFFMPEG = Application.dataPath + "/External/ffmpeg-windows/bin/ffmpeg.exe";
    string normalizedOutputFormat = (outputFormat ?? OutputFormatMp4).Trim().ToLowerInvariant();

    string ffmpegArguments;
    if (normalizedOutputFormat == OutputFormatWebM)
    {
      ffmpegArguments = $"-fflags +genpts -i \"{inputFilePath}\" -vsync cfr -r 30 -vf \"crop=trunc(iw/2)*2:trunc(ih/2)*2,format=yuva420p\" -c:v libvpx -pix_fmt yuva420p -auto-alt-ref 0 -deadline good -cpu-used 0 -crf 10 -b:v 0 -c:a libvorbis -q:a 5 \"{outputFilePath}\"";
    }
    else if (normalizedOutputFormat == OutputFormatMp4)
    {
      ffmpegArguments = $"-fflags +genpts -i \"{inputFilePath}\" -vsync cfr -r 30 -vf \"crop=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -profile:v high -level 4.0 -preset slow -crf 17 -pix_fmt yuv420p -movflags +faststart -c:a aac -ar 48000 -ac 2 -b:a 192k \"{outputFilePath}\"";
    }
    else
    {
      throw new System.ArgumentException($"Unsupported output format: {outputFormat}", nameof(outputFormat));
    }

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
      Debug.Log("Tried to run FFMPEG with arguments: " + ffmpegArguments);
      Debug.LogError($"FFMPEG conversion failed ({normalizedOutputFormat}): {error}");
    }
    else
    {
      Debug.Log($"FFMPEG conversion succeeded: {output}");
    }
  }
}
