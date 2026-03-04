using UnityEditor;
using UnityEngine;

public class MultiplayerBuildAndRun
{
    #region Win64

    [MenuItem("Tools/Run Multiplayer/Win64/1 Player")]
    static void PerformWin64Build1() => PerformWin64Build(1);

    [MenuItem("Tools/Run Multiplayer/Win64/2 Players")]
    static void PerformWin64Build2() => PerformWin64Build(2);

    [MenuItem("Tools/Run Multiplayer/Win64/3 Players")]
    static void PerformWin64Build3() => PerformWin64Build(3);

    [MenuItem("Tools/Run Multiplayer/Win64/4 Players")]
    static void PerformWin64Build4() => PerformWin64Build(4);

    static void PerformWin64Build(int playerCount)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        string projectName = GetProjectName();
        for (int i = 1; i <= playerCount; i++)
        {
            BuildPipeline.BuildPlayer(
                GetScenePaths(),
                $"Builds/Win64/{projectName}{i}/{projectName}{i}.exe",
                BuildTarget.StandaloneWindows64,
                BuildOptions.AutoRunPlayer);
        }
    }

    #endregion

    #region Mac

    [MenuItem("Tools/Run Multiplayer/Mac/1 Player")]
    static void PerformMacBuild1() => PerformMacBuild(1);

    [MenuItem("Tools/Run Multiplayer/Mac/2 Players")]
    static void PerformMacBuild2() => PerformMacBuild(2);

    [MenuItem("Tools/Run Multiplayer/Mac/3 Players")]
    static void PerformMacBuild3() => PerformMacBuild(3);

    [MenuItem("Tools/Run Multiplayer/Mac/4 Players")]
    static void PerformMacBuild4() => PerformMacBuild(4);

    static void PerformMacBuild(int playerCount)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

        string projectName = GetProjectName();
        for (int i = 1; i <= playerCount; i++)
        {
            BuildPipeline.BuildPlayer(
                GetScenePaths(),
                $"Builds/Mac/{projectName}{i}/{projectName}{i}.app",
                BuildTarget.StandaloneOSX,
                BuildOptions.AutoRunPlayer);
        }
    }

    #endregion

    static string GetProjectName()
    {
        string[] s = Application.dataPath.Split('/');
        return s[s.Length - 2];
    }

    static string[] GetScenePaths()
    {
        string[] scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
            scenes[i] = EditorBuildSettings.scenes[i].path;
        return scenes;
    }
}
