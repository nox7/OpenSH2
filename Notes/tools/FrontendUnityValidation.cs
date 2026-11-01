using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

[InitializeOnLoad]
public static class FrontendValidation
{
    const string Output = "C:/Users/garet/Documents/GitHub/OpenSH2/Temp/FrontendValidation";
    static double started;
    static int stage;
    static double captured;
    static RenderTexture target;
    static int layoutIndex;
    static int layoutFrame;
    static readonly Vector2[] Sizes = {new(1024,768),new(1920,1080),new(600,900)};
    static FrontendValidation()
    {
        if (SessionState.GetBool("FrontendValidation", false))
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Main").AddComponent<Main>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        SessionState.SetBool("FrontendValidation", true);
        EditorApplication.EnterPlaymode();
    }
    static void Log(string message, string stack, LogType type)
    {
        if (!stack.Contains("UnityEditor.Search.") && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            Fail(message + "\n" + stack);
    }
    static void Fail(string message)
    {
        File.WriteAllText(Output + "/result.txt", "FAIL: " + message);
        SessionState.SetBool("FrontendValidation", false);
        EditorApplication.Exit(1);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - started > 120) { Fail("Timed out at stage " + stage); return; }
        try
        {
            var doc = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            if (doc == null && stage != 6) return;
            var root = doc != null ? doc.rootVisualElement : null;
            if (doc != null && target == null)
            {
                target = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
                target.Create();
                doc.panelSettings.targetTexture = target;
            }
            if (stage == 0 && root.Q<Image>("intro-video")?.image != null)
            {
                Debug.Log("VALIDATION: logo is displaying decoded video; waiting for natural completion.");
                stage = 1;
            }
            else if (stage == 1)
            {
                var movie = UnityEngine.Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None)
                    .FirstOrDefault(v => v.url.Contains("Stronghold2") && v.frame > 2);
                if (movie == null) return;
                var input = new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(100,100) };
                using (var evt = PointerDownEvent.GetPooled(input)) root.Q("intro-screen").SendEvent(evt);
                Debug.Log("VALIDATION: natural logo completion advanced to intro; sent left click to skip it.");
                stage = 2;
            }
            else if (stage == 2 && root.Q("menu-screen").resolvedStyle.display == DisplayStyle.Flex)
            {
                if (root.Q<Image>("background-video").image == null || root.Q<Image>("curtain-idle").image == null) return;
                if (root.Q<Image>("curtain-opening").image != null) throw new Exception("Opening was not released after idle began.");
                if (root.Q<Image>("logo").image == null) throw new Exception("Original game logo missing.");
                if (root.Q<Button>("play").enabledSelf || !root.Q<Button>("exit").enabledSelf) throw new Exception("Menu action availability incorrect.");
                var players = UnityEngine.Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None);
                if (players.Length != 2) return; // Destroy is deferred until the end of the frame.
                if (players.Any(p => !p.isLooping)) throw new Exception("Expected looping menu videos.");
                var main = UnityEngine.Object.FindAnyObjectByType<Main>();
                if (main.Labor != null || main.Inventory != null) throw new Exception("Test map was loaded at startup.");

                captured = EditorApplication.timeSinceStartup;
                Debug.Log("VALIDATION: main menu has original artwork and two looping videos; capturing frame.");
                stage = 3;
            }
            else if (stage == 3 && EditorApplication.timeSinceStartup - captured > 3)
            {
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
                image.Apply();
                File.WriteAllBytes(Output + "/menu.png",image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.active = previous;
                layoutIndex = 0;
                stage = 4;
            }
            else if (stage == 4)
            {
                root.style.width = Sizes[layoutIndex].x;
                root.style.height = Sizes[layoutIndex].y;
                layoutFrame = Time.frameCount;
                stage = 5;
            }
            else if (stage == 5 && Time.frameCount > layoutFrame + 3)
            {
                var frame = root.Q("menu-frame").worldBound;
                var bounds = root.Q("menu-screen").worldBound;
                if (Mathf.Abs(frame.width / frame.height - 4f/3f) > .001f ||
                    frame.xMin < bounds.xMin - 1 || frame.yMin < bounds.yMin - 1 ||
                    frame.xMax > bounds.xMax + 1 || frame.yMax > bounds.yMax + 1)
                    throw new Exception("Menu frame does not fit viewport " + Sizes[layoutIndex] + ": " + frame + " within " + bounds);
                foreach(var button in root.Query<Button>().ToList())
                    if (!frame.Contains(button.worldBound.min) || !frame.Contains(button.worldBound.max))
                        throw new Exception("Menu button overflows frame: " + button.name);
                Debug.Log("VALIDATION: reference frame and buttons fit viewport " + Sizes[layoutIndex]);
                layoutIndex++;
                if (layoutIndex < Sizes.Length) stage = 4;
                else
                {
                    UnityEngine.Object.FindAnyObjectByType<Assets.Code.UI.Frontend.FrontendController>().enabled = false;
                    layoutFrame = Time.frameCount;
                    stage = 6;
                }
            }
            else if (stage == 6 && Time.frameCount > layoutFrame + 3)
            {
                if (UnityEngine.Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Length != 0)
                    throw new Exception("Video players leaked after disabling frontend.");
                if (UnityEngine.Object.FindAnyObjectByType<UIDocument>() != null)
                    throw new Exception("UIDocument leaked after disabling frontend.");
                File.WriteAllText(Output + "/result.txt", "PASS: natural logo completion, intro click-to-skip, menu artwork, curtain handoff, two looping menu videos, no test map, 4:3/wide/portrait layouts, frontend cleanup.");
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                SessionState.SetBool("FrontendValidation", false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { Fail(error.ToString()); }
    }
}
