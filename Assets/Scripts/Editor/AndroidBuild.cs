#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 명령줄 APK 빌드.
///
///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -buildTarget Android \
///             -executeMethod AndroidBuild.BuildApk -logFile &lt;log&gt;
/// </summary>
public static class AndroidBuild
{
    const string MainScene = "Assets/Scenes/SampleScene.unity";
    const string PackageName = "com.sunsoft.earthsmasher";

    public static void BuildApk()
    {
        try
        {
            EnsureShadersIncluded();
            ApplySettings();

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Android"));
            Directory.CreateDirectory(outDir);
            string apk = Path.Combine(outDir, "EarthSmasher.apk");

            if (!File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", MainScene))))
            {
                Debug.LogError($"[AndroidBuild] Scene not found: {MainScene}");
                EditorApplication.Exit(2);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[AndroidBuild] Building to {apk}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuild] SUCCESS {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[AndroidBuild] FAILED result={summary.result} errors={summary.totalErrors}");
                EditorApplication.Exit(1);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidBuild] EXCEPTION {e}");
            EditorApplication.Exit(3);
        }
    }

    /// <summary>
    /// 이 프로젝트는 머티리얼을 전부 런타임에 Shader.Find로 만든다. 씬/에셋이 직접
    /// 참조하지 않는 셰이더는 빌드에서 제거되어 폰에서 자홍색 덩어리만 남고,
    /// new Material(null)이 예외를 던져 부트스트랩 전체가 죽는다.
    /// 그래서 Always Included Shaders에 강제로 넣어준다.
    /// </summary>
    static void EnsureShadersIncluded()
    {
        // Shader.Find로 참조하는 내장 셰이더
        string[] builtIn =
        {
            "Standard",
            "Sprites/Default",
            "Skybox/Panoramic",
            "Legacy Shaders/Diffuse",
            "Universal Render Pipeline/Lit"
        };

        var wanted = new System.Collections.Generic.List<Shader>();

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

        var already = new System.Collections.Generic.HashSet<Shader>();
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
            Debug.Log($"[AndroidBuild] always-include shader: {s.name}");
        }

        if (added > 0)
        {
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
        Debug.Log($"[AndroidBuild] shaders always-included: {already.Count} (added {added})");

        EnsureStandardVariantsKept();
    }

    /// <summary>
    /// Always Included Shaders 는 셰이더 자체만 남기고, 프로젝트 에셋이 쓰지 않는
    /// 키워드 변형은 그대로 제거한다. 이 프로젝트는 Standard 머티리얼을 전부
    /// 런타임에 만들어서 _ALPHABLEND_ON / _EMISSION 변형이 통째로 사라지고,
    /// 그러면 알파가 1로 강제되거나 발광이 안 먹는다.
    /// ShaderVariantCollection 을 Preloaded Shaders 에 물려 변형을 보존한다.
    /// </summary>
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
                new string[0]
            };

            var passes = new[]
            {
                UnityEngine.Rendering.PassType.ForwardBase,
                UnityEngine.Rendering.PassType.ForwardAdd
            };

            int kept = 0;
            foreach (var pass in passes)
            {
                foreach (string[] keywords in combos)
                {
                    try
                    {
                        svc.Add(new ShaderVariantCollection.ShaderVariant(standard, pass, keywords));
                        kept++;
                    }
                    catch (Exception)
                    {
                        // 존재하지 않는 조합은 조용히 건너뛴다.
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

            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == svc)
                {
                    present = true;
                    break;
                }
            }

            if (!present)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[AndroidBuild] Standard variants preloaded: {kept}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AndroidBuild] variant keeper skipped: {e.Message}");
        }
    }

    /// <summary>같은 스트리핑 조건에서 문제를 재현하기 위한 PC 빌드 (진단용).</summary>
    public static void BuildWindows()
    {
        try
        {
            EnsureShadersIncluded();
            PlayerSettings.companyName = "sunsoft";
            PlayerSettings.productName = "Earth Smasher";

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

    static void ApplySettings()
    {
        PlayerSettings.companyName = "sunsoft";
        PlayerSettings.productName = "Earth Smasher";

        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, PackageName);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // 사이드로딩용 — 디버그 키스토어로 서명
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);

        // 가로 고정
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
    }
}
#endif
