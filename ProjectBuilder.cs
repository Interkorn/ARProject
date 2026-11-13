#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

/// <summary>
/// Автонастройка проекта и сборка двух APK из меню «AR Student».
/// Из командной строки:
///   Unity -batchmode -quit -projectPath . -executeMethod ProjectBuilder.BuildAllCli -logFile build.log
/// </summary>
public static class ProjectBuilder
{
    const string FurnScene = "Assets/Scenes/FurniturePlace.unity";
    const string GameScene = "Assets/Scenes/CoinHunt.unity";

    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(FurnScene) && File.Exists(GameScene)) return;
            try { Setup(); }
            catch (Exception e) { Debug.LogError("AR Student: автонастройка не удалась: " + e); }
        };
    }

    [MenuItem("AR Student/1. Настроить проект (сцены, Android, ARCore)")]
    public static void Setup()
    {
        ConfigurePlayer();
        ConfigureXR();
        IncludeShaders();
        CreateScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("AR Student: проект настроен.");
    }

    [MenuItem("AR Student/2. Собрать APK: AR Мебель")]
    public static void BuildFurniture() => Build(FurnScene, "AR-Mebel.apk", "com.arstudent.furnitureplace", "AR Мебель");

    [MenuItem("AR Student/3. Собрать APK: AR Охота за монетами")]
    public static void BuildGame() => Build(GameScene, "AR-Monety.apk", "com.arstudent.coinhunt", "AR Монеты");

    [MenuItem("AR Student/4. Собрать оба APK")]
    public static void BuildBoth()
    {
        BuildFurniture();
        BuildGame();
    }

    /// <summary>Для командной строки: собирает оба APK и завершает процесс с кодом результата.</summary>
    public static void BuildAllCli()
    {
        bool ok = Build(FurnScene, "AR-Mebel.apk", "com.arstudent.furnitureplace", "AR Мебель")
               && Build(GameScene, "AR-Monety.apk", "com.arstudent.coinhunt", "AR Монеты");
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // ------------------------------------------------------------ настройки

    static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "ARStudent";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // требование ARCore/AR Foundation
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.forceSDCardPermission = false;
    }

    static void ConfigureXR()
    {
        try
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            var group = BuildTargetGroup.Android;
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var manager = perTarget.ManagerSettingsForBuildTarget(group);
            XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.ARCore.ARCoreLoader", group);
            EditorUtility.SetDirty(perTarget);
        }
        catch (Exception e)
        {
            Debug.LogError("AR Student: не удалось включить ARCore автоматически (" + e.Message + "). " +
                           "Сделайте вручную: Edit > Project Settings > XR Plug-in Management > вкладка Android > галочка ARCore.");
        }
    }

    // Шейдеры, которые код ищет через Shader.Find — должны попасть в сборку.
    static void IncludeShaders()
    {
        var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (gs == null || gs.Length == 0) return;
        var so = new SerializedObject(gs[0]);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        foreach (var name in new[] { "Standard", "Sprites/Default", "Unlit/Color" })
        {
            var sh = Shader.Find(name);
            if (sh == null) continue;
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
            if (has) continue;
            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
        }
        so.ApplyModifiedProperties();
    }

    static void CreateScenes()
    {
        Directory.CreateDirectory("Assets/Scenes");
        MakeScene(FurnScene, typeof(FurnitureApp));
        MakeScene(GameScene, typeof(CoinGame));
    }

    static void MakeScene(string path, Type component)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("App").AddComponent(component);
        EditorSceneManager.SaveScene(scene, path);
    }

    // --------------------------------------------------------------- сборка

    static bool Build(string scene, string fileName, string applicationId, string productName)
    {
        Setup();
        PlayerSettings.productName = productName;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
        PlayerSettings.bundleVersion = "1.0";

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        ConfigureXR(); // после смены платформы

        Directory.CreateDirectory("Builds");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = Path.Combine("Builds", fileName),
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });

        bool ok = report.summary.result == BuildResult.Succeeded;
        Debug.Log(ok
            ? "AR Student: готово → " + Path.GetFullPath(Path.Combine("Builds", fileName)) + " (" + report.summary.totalSize / 1024 / 1024 + " МБ)"
            : "AR Student: сборка " + fileName + " не удалась: " + report.summary.result);
        if (ok && !Application.isBatchMode) EditorUtility.RevealInFinder(Path.Combine("Builds", fileName));
        return ok;
    }
}
#endif
