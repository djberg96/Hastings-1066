using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectBuilder
{
    private const string ScenePath = "Assets/Scenes/Hastings.unity";

    [MenuItem("Hastings/Create Main Scene")]
    public static void CreateScene()
    {
        PlayerSettings.companyName = "Hastings 1066";
        PlayerSettings.productName = "Hastings 1066";
        ConfigureTextures();
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 12;
        camera.backgroundColor = new Color(0.11f, 0.12f, 0.14f);
        cameraObject.transform.position = new Vector3(0, 0, -10);
        new GameObject("Hastings Game", typeof(HastingsGame));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Created Hastings main scene");
    }

    public static void BuildMac()
    {
        PlayerSettings.companyName = "Hastings 1066";
        PlayerSettings.productName = "Hastings 1066";
        ConfigureTextures();
        if (!File.Exists(ScenePath)) CreateScene();
        Directory.CreateDirectory("../Builds/Mac");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath },
            locationPathName = "../Builds/Mac/Hastings 1066.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Mac build failed: " + report.summary.result);
    }

    private static void ConfigureTextures()
    {
        var map=AssetImporter.GetAtPath("Assets/Resources/Art/Map/hex_map.png") as TextureImporter;
        if(map!=null && map.maxTextureSize!=8192)
        {map.maxTextureSize=8192;map.mipmapEnabled=false;map.SaveAndReimport();}
        foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Art/Counters"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer!=null && importer.maxTextureSize!=512)
            {importer.maxTextureSize=512;importer.mipmapEnabled=false;importer.SaveAndReimport();}
        }
    }
}
