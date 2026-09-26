using System;
using Birdsong;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class V1ProjectSetup
{
    public const string ScenePath = "Assets/2DBirds_Pack3/Scenes/MainScene.unity";

    [MenuItem("Birdsong/Configure V1 Scene")]
    public static void Configure()
    {
        EditorSceneManager.OpenScene(ScenePath);
        // The prototype referenced a lighting asset that is absent from the source project.
        Lightmapping.lightingSettings = null;
        var board = UnityEngine.Object.FindObjectOfType<BoardManager>();
        var input = UnityEngine.Object.FindObjectOfType<InputManager>();
        var camera = Camera.main;
        if (board == null || input == null || camera == null) throw new InvalidOperationException("Original gameplay scene objects are missing.");
        // A Bird was accidentally attached to the board container in the prototype.
        var stray = board.GetComponent<Bird>();
        if (stray != null) UnityEngine.Object.DestroyImmediate(stray);
        board.transform.position = Vector3.zero;
        board.showGrid = false;
        board.birdVisualScale = 1.55f;
        var visualizer = UnityEngine.Object.FindObjectOfType<GridVisualizer>();
        if (visualizer != null) visualizer.showGrid = false;
        board.birdPrefabs = new[]
        {
            Prefab("Bird1/B1 Red Sheet_0"),
            Prefab("Bird5/Bird 5 Pink_0"),
            Prefab("Bird2/Bird 2 Blue_0"),
            Prefab("Bird4/Bird 4 Yellow_0"),
            Prefab("Bird5/Bird 5 Green_0")
        };
        string error = board.ValidateConfiguration();
        if (error != null) throw new InvalidOperationException(error);
        foreach (var prefab in board.birdPrefabs)
        {
            string texturePath = AssetDatabase.GetAssetPath(prefab.GetComponent<SpriteRenderer>().sprite.texture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            var desktopTexture = importer.GetPlatformTextureSettings("Standalone");
            var androidTexture = importer.GetPlatformTextureSettings("Android");
            if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear ||
                desktopTexture.maxTextureSize != 2048 || !androidTexture.overridden || androidTexture.maxTextureSize != 2048)
            {
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 2048;
                desktopTexture.overridden = true;
                desktopTexture.maxTextureSize = 2048;
                desktopTexture.format = TextureImporterFormat.DXT5;
                importer.SetPlatformTextureSettings(desktopTexture);
                androidTexture.overridden = true;
                androidTexture.maxTextureSize = 2048;
                androidTexture.format = TextureImporterFormat.ETC2_RGBA8;
                importer.SetPlatformTextureSettings(androidTexture);
                importer.SaveAndReimport();
            }
        }
        var controller = UnityEngine.Object.FindObjectOfType<GameController>();
        if (controller == null) controller = new GameObject("Birdsong").AddComponent<GameController>();
        controller.board = board;
        controller.input = input;
        controller.gameCamera = camera;
        controller.campaignData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Campaign.json");
        controller.ui = controller.GetComponent<GameUI>() ?? controller.gameObject.AddComponent<GameUI>();
        controller.audioPlayer = controller.GetComponent<GameAudio>() ?? controller.gameObject.AddComponent<GameAudio>();
        var audio = controller.audioPlayer;
        audio.select = Clip("select");
        audio.swap = Clip("swap");
        audio.match = Clip("match");
        audio.invalid = Clip("invalid");
        audio.cascade = Clip("cascade");
        audio.win = Clip("win");
        audio.lose = Clip("lose");
        audio.button = Clip("button");
        var source = controller.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        camera.backgroundColor = new Color32(247, 243, 230, 255);
        PlayerSettings.productName = "Birdsong";
        PlayerSettings.companyName = "Birdsong";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.example.birdsong");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.runInBackground = false;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("BIRDSONG_SETUP_OK: Original scene, five existing prefabs, campaign and UI are wired.");
    }
    [MenuItem("Birdsong/Build Windows Development")]
    public static void BuildWindows()
    {
        Configure();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = "Build/Windows/Birdsong.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed: " + report.summary.result);
        Debug.Log("BIRDSONG_BUILD_OK: " + report.summary.totalSize + " bytes");
    }
    static GameObject Prefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2DBirds_Pack3/Prefabs/" + path + ".prefab");
        if (prefab == null) throw new InvalidOperationException("Missing existing bird prefab: " + path);
        return prefab;
    }
    static AudioClip Clip(string name)
    {
        string path = "Assets/Birdsong/Audio/" + name + ".wav";
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Missing sound effect: " + path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new InvalidOperationException("Could not import sound effect: " + path);
        return clip;
    }
}
