#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Android / Google Play 빌드.
///
/// Play Store (AAB):
///   -executeMethod AndroidBuild.BuildPlayAab
///
/// 로컬 테스트 (APK):
///   -executeMethod AndroidBuild.BuildApk
/// </summary>
public static class AndroidBuild
{
    const string MainScene = "Assets/Scenes/SampleScene.unity";
    const string PackageName = "com.sunsoft.earthsmasher";
    const string ProductName = "Earth Smasher";
    const string CompanyName = "sunsoft";
    const string KeystorePropsRelative = "Build/android/play-keystore.properties";
    const string ReleaseOutputDir = "Build/Android/Release";
    const string AppIconPath = "Assets/Art/AppIcon/earth-smasher-icon-512.png";
    const string AppIconBgPath = "Assets/Art/AppIcon/earth-smasher-icon-bg-512.png";

    public static void BuildPlayAab() => BuildAndroid(release: true, appBundle: true, exitWhenDone: true);

    public static void BuildApk() => BuildAndroid(release: false, appBundle: false, exitWhenDone: true);

    [MenuItem("Build/Google Play AAB (Release)")]
    public static void MenuBuildPlayAab()
    {
        BuildAndroid(release: true, appBundle: true, exitWhenDone: false);
    }

    [MenuItem("Build/Android APK (Debug)")]
    public static void MenuBuildApk()
    {
        BuildAndroid(release: false, appBundle: false, exitWhenDone: false);
    }

    static void BuildAndroid(bool release, bool appBundle, bool exitWhenDone)
    {
        try
        {
            EnsureShadersIncluded();
            EnsureFleetCatalogForRelease();
            ApplySettings(release, appBundle);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, ReleaseOutputDir));
            Directory.CreateDirectory(outDir);

            string ext = appBundle ? "aab" : "apk";
            string fileName = appBundle ? "EarthSmasher.aab" : "EarthSmasher.apk";
            string output = Path.Combine(outDir, fileName);

            if (!File.Exists(Path.GetFullPath(Path.Combine(projectRoot, MainScene))))
            {
                Debug.LogError($"[AndroidBuild] Scene not found: {MainScene}");
                if (exitWhenDone) EditorApplication.Exit(2);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.CompressWithLz4HC
            };

            Debug.Log($"[AndroidBuild] Building {(appBundle ? "AAB" : "APK")} → {output}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                if (!File.Exists(output))
                {
                    Debug.LogError($"[AndroidBuild] Build reported success but file missing: {output}");
                    if (exitWhenDone) EditorApplication.Exit(4);
                    return;
                }

                if (appBundle)
                {
                    if (!ValidateAppBundle(output, out string reason))
                    {
                        Debug.LogError($"[AndroidBuild] Invalid AAB: {reason}. " +
                            "APK를 .aab로 바꿔 올리면 Play Console이 거부합니다. Build → Google Play AAB 로 다시 빌드하세요.");
                        if (exitWhenDone) EditorApplication.Exit(5);
                        return;
                    }
                }

                Debug.Log($"[AndroidBuild] SUCCESS {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}");
                Debug.Log($"[AndroidBuild] package={PackageName} bundleVersionCode={PlayerSettings.Android.bundleVersionCode}");
                Debug.Log($"[AndroidBuild] Upload file: {output}");
                if (exitWhenDone) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[AndroidBuild] FAILED result={summary.result} errors={summary.totalErrors}");
                if (exitWhenDone) EditorApplication.Exit(1);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidBuild] EXCEPTION {e}");
            if (exitWhenDone) EditorApplication.Exit(3);
        }
    }

    static void ApplySettings(bool release, bool appBundle)
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;

        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, PackageName);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.bundleVersionCode = Math.Max(6, PlayerSettings.Android.bundleVersionCode);

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        EditorUserBuildSettings.buildAppBundle = appBundle;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        EditorUserBuildSettings.development = !release;
        EditorUserBuildSettings.allowDebugging = !release;

        ApplyKeystore(release);
        ApplyAppIcon();
    }

    static void EnsureFleetCatalogForRelease()
    {
        FleetAssetBootstrap.LinkAllImportedAssetsSilent();

        var catalog = Resources.Load<FleetVisualCatalog>("Fleet/Catalog");
        if (catalog == null)
        {
            Debug.LogError("[AndroidBuild] Fleet catalog missing at Resources/Fleet/Catalog.asset");
            return;
        }

        if (catalog.battleship == null || catalog.ufo == null || catalog.fighter == null)
            Debug.LogError("[AndroidBuild] Fleet catalog has missing prefab refs. Run EarthSmasher → Link All Imported Assets.");
        else
            Debug.Log("[AndroidBuild] Fleet catalog OK for release build.");
    }

    static void ApplyAppIcon()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AppIconPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AppIconBgPath, ImportAssetOptions.ForceUpdate);

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
        var iconBg = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconBgPath);
        if (icon == null)
        {
            Debug.LogError("[AndroidBuild] App icon not found: " + AppIconPath +
                " (check .meta GUID is 32 hex characters)");
            return;
        }

        if (iconBg == null)
            iconBg = icon;

        var platform = NamedBuildTarget.Android;
        PlatformIcon[] adaptiveIcons = PlayerSettings.GetPlatformIcons(platform, AndroidPlatformIconKind.Adaptive);
        for (int i = 0; i < adaptiveIcons.Length; i++)
            adaptiveIcons[i].SetTextures(new[] { icon, iconBg });
        PlayerSettings.SetPlatformIcons(platform, AndroidPlatformIconKind.Adaptive, adaptiveIcons);

        Debug.Log("[AndroidBuild] App icon applied: " + AppIconPath);
    }

    /// <summary>AAB는 ZIP 형식이며 base/ 또는 BundleConfig.pb 가 있어야 한다.</summary>
    static bool ValidateAppBundle(string path, out string reason)
    {
        reason = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            reason = "file not found";
            return false;
        }

        if (!path.EndsWith(".aab", StringComparison.OrdinalIgnoreCase))
        {
            reason = "extension is not .aab";
            return false;
        }

        try
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            bool hasBase = zip.Entries.Any(e =>
                e.FullName.StartsWith("base/", StringComparison.OrdinalIgnoreCase)
                || e.FullName.Equals("BundleConfig.pb", StringComparison.OrdinalIgnoreCase)
                || e.FullName.EndsWith("/BundleConfig.pb", StringComparison.OrdinalIgnoreCase));
            if (!hasBase)
            {
                reason = "missing base/ module (probably an APK renamed to .aab)";
                return false;
            }
        }
        catch (InvalidDataException)
        {
            reason = "not a valid zip/app bundle";
            return false;
        }

        return true;
    }

    static void ApplyKeystore(bool release)
    {
        if (TryLoadKeystoreProps(out KeystoreProps props))
        {
            string keystorePath = Path.GetFullPath(props.KeystorePath);
            if (!File.Exists(keystorePath))
            {
                Debug.LogError($"[AndroidBuild] Keystore not found: {keystorePath}");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = props.KeystorePass;
                PlayerSettings.Android.keyaliasName = props.KeyAlias;
                PlayerSettings.Android.keyaliasPass = props.KeyAliasPass;
                Debug.Log($"[AndroidBuild] Release signing: {props.KeyAlias} ({keystorePath})");
                return;
            }
        }

        PlayerSettings.Android.useCustomKeystore = false;
        if (release)
        {
            Debug.LogWarning(
                "[AndroidBuild] play-keystore.properties 없음 — 디버그 키로 AAB가 만들어집니다. " +
                "Play Console 업로드용은 Build/android/play-keystore.properties.example 참고.");
        }
    }

    struct KeystoreProps
    {
        public string KeystorePath;
        public string KeystorePass;
        public string KeyAlias;
        public string KeyAliasPass;
    }

    static bool TryLoadKeystoreProps(out KeystoreProps props)
    {
        props = default;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string propsPath = Path.Combine(projectRoot, KeystorePropsRelative);
        if (!File.Exists(propsPath))
            return false;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(propsPath))
        {
            string t = line.Trim();
            if (t.Length == 0 || t.StartsWith("#"))
                continue;
            int eq = t.IndexOf('=');
            if (eq <= 0)
                continue;
            map[t.Substring(0, eq).Trim()] = t.Substring(eq + 1).Trim();
        }

        if (!map.TryGetValue("keystorePath", out string path) || string.IsNullOrWhiteSpace(path))
            return false;
        if (!map.TryGetValue("keystorePass", out string storePass))
            storePass = "";
        if (!map.TryGetValue("keyAlias", out string alias) || string.IsNullOrWhiteSpace(alias))
            return false;
        if (!map.TryGetValue("keyAliasPass", out string aliasPass))
            aliasPass = storePass;

        props = new KeystoreProps
        {
            KeystorePath = path,
            KeystorePass = storePass,
            KeyAlias = alias,
            KeyAliasPass = aliasPass
        };
        return true;
    }

    /// <summary>
    /// 머티리얼을 전부 런타임에 Shader.Find로 만든다. Always Included Shaders에 강제 등록.
    /// </summary>
    static void EnsureShadersIncluded()
    {
        string[] builtIn =
        {
            "Standard",
            "Sprites/Default",
            "Skybox/Panoramic",
            "Legacy Shaders/Diffuse",
            "Universal Render Pipeline/Lit"
        };

        var wanted = new List<Shader>();

        foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" }))
        {
            var s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null)
                wanted.Add(s);
        }

        foreach (string name in builtIn)
        {
            var s = Shader.Find(name);
            if (s != null)
                wanted.Add(s);
        }

        var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
        if (settings == null)
        {
            Debug.LogError("[AndroidBuild] GraphicsSettings.asset not found — shaders may be stripped.");
            return;
        }

        var so = new SerializedObject(settings);
        var list = so.FindProperty("m_AlwaysIncludedShaders");

        var already = new HashSet<Shader>();
        for (int i = 0; i < list.arraySize; i++)
        {
            var s = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (s != null)
                already.Add(s);
        }

        int added = 0;
        foreach (Shader s in wanted)
        {
            if (!already.Add(s))
                continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
            added++;
        }

        if (added > 0)
        {
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        EnsureStandardVariantsKept();
    }

    static void EnsureStandardVariantsKept()
    {
        const string dir = "Assets/ShaderVariants";
        const string path = dir + "/StandardRuntime.shadervariants";

        var standard = Shader.Find("Standard");
        if (standard == null)
            return;

        try
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var svc = new ShaderVariantCollection();
            string[][] combos =
            {
                new[] { "_EMISSION" },
                new[] { "_ALPHABLEND_ON" },
                new[] { "_ALPHAPREMULTIPLY_ON" },
                new[] { "_ALPHABLEND_ON", "_EMISSION" },
                new[] { "_NORMALMAP" },
                Array.Empty<string>()
            };

            var passes = new[]
            {
                UnityEngine.Rendering.PassType.ForwardBase,
                UnityEngine.Rendering.PassType.ForwardAdd
            };

            foreach (var pass in passes)
            {
                foreach (string[] keywords in combos)
                {
                    try
                    {
                        svc.Add(new ShaderVariantCollection.ShaderVariant(standard, pass, keywords));
                    }
                    catch (Exception)
                    {
                        // skip invalid combo
                    }
                }
            }

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(svc, path);
            AssetDatabase.SaveAssets();

            var graphics = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphics == null)
                return;

            var so = new SerializedObject(graphics);
            var list = so.FindProperty("m_PreloadedShaders");
            if (list == null)
                return;

            var asset = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset)
                {
                    present = true;
                    break;
                }
            }

            if (!present)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AndroidBuild] variant keeper skipped: {e.Message}");
        }
    }

    /// <summary>진단용 Windows 빌드.</summary>
    public static void BuildWindows()
    {
        try
        {
            EnsureShadersIncluded();
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Windows"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = Path.Combine(outDir, "EarthSmasher.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[AndroidBuild] WIN_SUCCESS");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[AndroidBuild] WIN_FAILED {report.summary.result}");
                EditorApplication.Exit(1);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidBuild] WIN_EXCEPTION {e}");
            EditorApplication.Exit(3);
        }
    }
}
#endif
