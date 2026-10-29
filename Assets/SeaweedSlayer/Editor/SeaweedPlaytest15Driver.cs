using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class SeaweedPlaytest15Driver
{
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string Request => Path.Combine(Root, "Captures", "playtest-15.request");
    static string Running => Path.Combine(Root, "Captures", "playtest-15.running");
    static string Done => Path.Combine(Root, "Captures", "playtest-15.done");
    // Survives the domain reload on entering Play Mode; normal Play presses never set it.
    static bool Active
    {
        get => SessionState.GetBool("SeaweedPlaytest15.Active", false);
        set => SessionState.SetBool("SeaweedPlaytest15.Active", value);
    }
    static bool pending;
    static double requestedAt = -1;
    static double playStartedAt = -1;

    static SeaweedPlaytest15Driver()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Seaweed Slayer/Playtest 15 Snapshots")]
    static void StartFromMenu()
    {
        Directory.CreateDirectory(Path.Combine(Root, "Captures"));
        if (File.Exists(Done)) File.Delete(Done);
        File.WriteAllText(Request, "15");
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!pending && File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            pending = true; Active = true;
            requestedAt = EditorApplication.timeSinceStartup;
            Directory.CreateDirectory(Path.Combine(Root, "Captures"));
            if (File.Exists(Done)) File.Delete(Done);
            File.WriteAllText(Running, DateTime.UtcNow.ToString("o"));
            File.Delete(Request);
            Debug.Log("PLAYTEST15_ENTER");
            EditorApplication.isPlaying = true;
        }
        if (pending && !EditorApplication.isPlayingOrWillChangePlaymode && requestedAt > 0 &&
            EditorApplication.timeSinceStartup - requestedAt > 8)
        {
            File.WriteAllText(Done, "fail: play mode did not start");
            pending = false;
        }
        // A leftover .done file from an old playtest must not stop a normal Play press.
        if (!Active) return;
        if (EditorApplication.isPlaying && File.Exists(Running))
        {
            if (playStartedAt < 0) playStartedAt = EditorApplication.timeSinceStartup;
            if (!File.Exists(Done) && EditorApplication.timeSinceStartup - playStartedAt > 300)
                File.WriteAllText(Done, "fail: editor timeout");
        }
        if (!File.Exists(Done)) return;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        if (File.Exists(Running)) File.Delete(Running);
        Active = false; pending = false; playStartedAt = -1;
    }
}
