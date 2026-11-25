using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Management;

namespace HoloWall.Editor
{
    /// <summary>
    /// Batch entry points for HoloWall (Made by Maheshwaran).
    ///   SetupProject : creates materials, TMP font, plane prefab, the Main scene and Android player settings.
    ///   BuildAndroid : runs SetupProject and builds Builds/Android/HoloWall-1.0.0.apk.
    /// Run headless, e.g.
    ///   Unity.exe -batchmode -nographics --burst-disable-compilation -quit -buildTarget Android
    ///             -projectPath . -executeMethod HoloWall.Editor.HoloWallBuild.BuildAndroid -logFile build.log
    /// </summary>
    public static class HoloWallBuild
    {
        public const string Version = "1.0.0";
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string ApkPath = "Builds/Android/HoloWall-" + Version + ".apk";

        const string Root = "Assets/HoloWall";
        public const string MaterialsDir = Root + "/Materials";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string FontsDir = Root + "/Fonts";
        public const string UiDir = Root + "/UI";
        public const string IconDir = Root + "/Icon";

        [MenuItem("HoloWall/Setup Project")]
        public static void SetupProject()
        {
            try
            {
                EnsureFolders();
                ImportTextures();
                CreateMaterials();
                var font = CreateFontAsset();
                var planePrefab = CreatePlanePrefab();
                var sprites = HoloWallSceneBuilder.CreateUiSprites();
                HoloWallSceneBuilder.BuildScene(font, planePrefab, sprites);
                ConfigurePlayerSettings();
                ConfigureXR();
                ConfigureIcons();
                RemoveLeftovers();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[HoloWall] SetupProject finished.");
            }
            catch (Exception e)
            {
                Debug.LogError("[HoloWall] SetupProject failed: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("HoloWall/Build Android APK")]
        public static void BuildAndroid()
        {
            SetupProject();

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.development = false;

            string apk = Path.GetFullPath(ApkPath);
            Directory.CreateDirectory(Path.GetDirectoryName(apk));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[HoloWall] Build result: {summary.result}, size {summary.totalSize / (1024.0 * 1024.0):F1} MB, errors {summary.totalErrors}, warnings {summary.totalWarnings}, output {summary.outputPath}");
            if (summary.result == BuildResult.Succeeded) Debug.Log("Build succeeded");

            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        // ---------- Folders / textures ----------

        static void EnsureFolders()
        {
            foreach (var dir in new[] { "Assets/Scenes", MaterialsDir, PrefabsDir, FontsDir, UiDir, IconDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        static void ImportTextures()
        {
            // Icons: no compression, no mipmaps, keep alpha.
            foreach (var name in new[] { "AppIcon", "AdaptiveForeground", "AdaptiveBackground" })
                SetTextureImport(IconDir + "/" + name + ".png", TextureImporterType.Default, 1024);

            // Logo is also used inside the UI.
            SetTextureImport(IconDir + "/Logo.png", TextureImporterType.Sprite, 512);
        }

        static void SetTextureImport(string path, TextureImporterType type, int maxSize)
        {
            if (!File.Exists(path)) { Debug.LogWarning("[HoloWall] Missing texture: " + path); return; }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = type;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = maxSize;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None;
            if (type == TextureImporterType.Sprite) imp.spriteImportMode = SpriteImportMode.Single;
            imp.SaveAndReimport();
        }

        // ---------- Materials ----------

        static void CreateMaterials()
        {
            MakeMaterial(MaterialsDir + "/HologramScreen.mat", "HoloWall/HoloChromaKey", m =>
            {
                m.SetColor("_KeyColor", new Color(0f, 1f, 0f, 1f));
                m.SetFloat("_Similarity", 0.28f);
                m.SetFloat("_Smoothness", 0.12f);
                m.SetFloat("_Spill", 0.8f);
                m.SetFloat("_Hologram", 1f);
                m.SetFloat("_CyanTint", 0f);
                m.renderQueue = 3000;
            });
            MakeMaterial(MaterialsDir + "/Reticle.mat", "HoloWall/Reticle", null);
            MakeMaterial(MaterialsDir + "/PlaneGrid.mat", "HoloWall/PlaneGrid", null);
        }

        static void MakeMaterial(string path, string shaderName, Action<Material> setup)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception("Shader not found: " + shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            setup?.Invoke(mat);
            EditorUtility.SetDirty(mat);
        }

        // ---------- TextMeshPro font ----------

        static TMP_FontAsset CreateFontAsset()
        {
            const string ttf = FontsDir + "/Rajdhani-Bold.ttf";
            const string assetPath = FontsDir + "/Rajdhani-Bold SDF.asset";

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) throw new Exception("Font not found: " + ttf);

            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
            fontAsset.name = "Rajdhani-Bold SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // Pre-bake printable ASCII so no glyph has to be generated at runtime.
            var chars = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
            fontAsset.TryAddCharacters(chars, out _);

            fontAsset.material.name = "Rajdhani-Bold SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            foreach (var tex in fontAsset.atlasTextures)
            {
                tex.name = "Rajdhani-Bold SDF Atlas";
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        // ---------- AR plane prefab ----------

        static GameObject CreatePlanePrefab()
        {
            const string path = PrefabsDir + "/PlaneGrid.prefab";
            var go = new GameObject("AR Plane Grid");
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/PlaneGrid.mat");
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<UnityEngine.XR.ARFoundation.ARPlane>();
            if (go.GetComponent<UnityEngine.XR.ARFoundation.ARPlaneMeshVisualizer>() == null)
                go.AddComponent<UnityEngine.XR.ARFoundation.ARPlaneMeshVisualizer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- Player settings ----------

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Maheshwaran";
            PlayerSettings.productName = "HoloWall";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.maheshwaran.holowall");
            PlayerSettings.Android.bundleVersionCode = 1;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26; // minimum for ARCore (required) apps
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // Camera permission text (used by the camera permission prompt / store listing text).
            PlayerSettings.iOS.cameraUsageDescription = "HoloWall uses the camera to detect walls and place AR holograms.";

            // Strip unused code; keep the video / AR modules.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.useCustomKeystore = false; // debug signing, fine for sideloading
        }

        static void ConfigureXR()
        {
            // ARCore is mandatory: the app cannot do anything useful without it.
            var arcore = ARCoreSettings.GetOrCreateSettings();
            arcore.requirement = ARCoreSettings.Requirement.Required;
            arcore.depth = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arcore);

            var perTarget = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (perTarget == null)
            {
                Debug.LogWarning("[HoloWall] No XR general settings for Android yet; enable ARCore in Project Settings > XR Plug-in Management.");
                return;
            }
            perTarget.InitManagerOnStart = true;
            var manager = perTarget.Manager;
            if (manager != null)
            {
                bool hasArCore = manager.activeLoaders.Any(l => l != null && l.GetType().Name == "ARCoreLoader");
                if (!hasArCore)
                    XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android);
                EditorUtility.SetDirty(manager);
            }
            EditorUtility.SetDirty(perTarget);
        }

        static void ConfigureIcons()
        {
            var app = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AppIcon.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AdaptiveForeground.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AdaptiveBackground.png");
            if (app == null || fg == null || bg == null)
            {
                Debug.LogWarning("[HoloWall] Icon textures missing; skipping icon setup.");
                return;
            }

            // Adaptive icon (layer 0 = background, layer 1 = foreground).
            var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive) icon.SetTextures(bg, fg);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);

            // Default icon (used as the fallback / legacy source).
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { app }, IconKind.Application);
        }

        static void RemoveLeftovers()
        {
            // The template's sample scene is replaced by Main.unity.
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            // ARKit is not used (Android only); drop its leftover settings assets.
            foreach (var p in new[] { "Assets/XR/Loaders/AR Kit Loader.asset", "Assets/XR/Settings/AR Kit Settings.asset" })
                if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
        }
    }
}
