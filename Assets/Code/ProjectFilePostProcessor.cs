using UnityEditor;

public class ProjectFilePostprocessor : AssetPostprocessor
{
  public static string OnGeneratedSlnSolution(string path, string content)
  {
    // Adds a file reference to the solution
    content = content.Replace(
      "<Solution>", 
      @"<Solution>\n
        <Folder Name=""/Solution Items/"">
          <File Path=""AGENTS.md"" />
        </Folder>
      ");
    return content;
  }

  public static string OnGeneratedCSProject(string path, string content)
  {
    return content;
  }
}