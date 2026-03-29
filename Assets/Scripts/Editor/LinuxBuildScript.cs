using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds Linux client and Linux dedicated server from CLI (batch mode).
///
/// Usage:
///   Unity.exe -batchmode -quit -projectPath "D:/Juego" -executeMethod LinuxBuildScript.BuildAll
///   Unity.exe -batchmode -quit -projectPath "D:/Juego" -executeMethod LinuxBuildScript.BuildClient
///   Unity.exe -batchmode -quit -projectPath "D:/Juego" -executeMethod LinuxBuildScript.BuildServer
/// </summary>
public static class LinuxBuildScript
{
    const string k_ClientOutput = "Builds/clientserver/BossRoom.x86_64";
    const string k_ServerOutput = "Builds/linuxserver/BossRoom.x86_64";

    static string[] GetScenes() =>
        EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

    [MenuItem("Boss Room/Linux Builds/Build All (Client + Server)")]
    public static void BuildAll()
    {
        BuildClient();
        BuildServer();
    }

    [MenuItem("Boss Room/Linux Builds/Build Linux Client")]
    public static void BuildClient()
    {
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        var options = new BuildPlayerOptions
        {
            scenes = GetScenes(),
            locationPathName = k_ClientOutput,
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.StrictMode,
        };

        RunBuild("Linux Client", options);
    }

    [MenuItem("Boss Room/Linux Builds/Build Linux Server")]
    public static void BuildServer()
    {
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

        var options = new BuildPlayerOptions
        {
            scenes = GetScenes(),
            locationPathName = k_ServerOutput,
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.StrictMode,
        };

        RunBuild("Linux Server", options);
    }

    static void RunBuild(string label, BuildPlayerOptions options)
    {
        Debug.Log($"[LinuxBuildScript] Starting {label} build → {options.locationPathName}");

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[LinuxBuildScript] {label} build succeeded — {summary.totalSize / 1024 / 1024} MB at {summary.outputPath}");
        }
        else
        {
            string error = $"[LinuxBuildScript] {label} build FAILED ({summary.totalErrors} errors)";
            Debug.LogError(error);
            // Exit with error code so CI/scripts can detect failure
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                throw new Exception(error);
        }
    }
}
