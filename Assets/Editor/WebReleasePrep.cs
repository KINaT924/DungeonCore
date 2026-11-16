using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Web 빌드 직전 정리 도구 (에디터 전용).
///
/// 1) 타이틀 종료 버튼: 종료 확인 버튼 → TitleSceneController.QuitGame 연결, 웹에서 숨길 종료 버튼 참조 연결
/// 2) 웹 화질: WebGL이 쓰는 Mobile URP 설정의 렌더 스케일 1.0, 물체당 조명 8개, 부드러운 그림자 켜기
/// 3) 웹 페이지: 기본 캔버스 1280×720, 커스텀 템플릿(DungeonCore) 지정
/// 4) 용량: 빌드 씬에서 쓰는 텍스처 중 1024보다 큰 것에 WebGL 전용 최대 크기 1024 지정
///
/// 원본 값은 output/web-prep/에 기록하며, 메뉴의 Undo로 되돌릴 수 있다.
/// 여러 번 실행해도 결과는 같다.
/// </summary>
public static class WebReleasePrep
{
    const string Output = "output/web-prep";
    const string TitleScenePath = "Assets/01.Scenes/TitleScene.unity";
    const string MobileRpPath = "Assets/06.Settings/Mobile_RPAsset.asset";
    const string TemplateName = "PROJECT:DungeonCore";
    const string TemplateFolder = "Assets/WebGLTemplates/DungeonCore";
    const string WebGLPlatform = "WebGL";
    const int WebMaxTexture = 1024;

    static string TextureLog => Output + "/texture-overrides.txt";
    static string SettingsLog => Output + "/settings-before.txt";

    /// <summary>메뉴 실행 시 호출. 네 가지 작업을 차례로 하고 결과를 창으로 알린다.</summary>
    [MenuItem("Tools/DungeonCore/Web/Apply Web Release Prep")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Output);

        var report = new List<string>();
        string previousScene = SceneManager.GetActiveScene().path;

        report.Add(ConnectQuitButton());
        report.Add(ImproveMobileQuality());
        report.Add(ApplyPlayerSettings());
        report.Add(LimitTextureSize());

        if (!string.IsNullOrEmpty(previousScene) && previousScene != TitleScenePath && File.Exists(previousScene))
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

        string message = string.Join("\n", report);
        File.AppendAllText(Output + "/apply-log.txt", System.DateTime.Now + "\n" + message + "\n\n");
        Debug.Log("[WebReleasePrep]\n" + message);
        EditorUtility.DisplayDialog("Web Release Prep", message, "확인");
    }

    // ───────────────────────── 1) 종료 버튼 ─────────────────────────

    /// <summary>종료 확인 버튼에 QuitGame을 연결하고, 숨길 종료 버튼을 컨트롤러에 넣는다.</summary>
    static string ConnectQuitButton()
    {
        Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        var controller = Object.FindFirstObjectByType<TitleSceneController>(FindObjectsInactive.Include);
        Button quit = FindButton(scene, "QuitButton");
        Button confirm = FindButton(scene, "ConfirmQuitButton");
        if (controller == null || quit == null || confirm == null)
            return "✗ 종료 버튼: TitleSceneController / QuitButton / ConfirmQuitButton 중 찾지 못한 것이 있음";

        var so = new SerializedObject(controller);
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 이미 연결돼 있으면 다시 추가하지 않는다
        bool connected = false;
        for (int i = 0; i < confirm.onClick.GetPersistentEventCount(); i++)
            if (confirm.onClick.GetPersistentTarget(i) == controller && confirm.onClick.GetPersistentMethodName(i) == nameof(TitleSceneController.QuitGame))
                connected = true;
        if (!connected)
            UnityEventTools.AddPersistentListener(confirm.onClick, new UnityAction(controller.QuitGame));

        EditorUtility.SetDirty(confirm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "✓ 종료 버튼: 확인 버튼 → QuitGame 연결" + (connected ? "(이미 연결됨)" : "") + ", 웹 빌드에서 종료 버튼 숨김";
    }

    static Button FindButton(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
                if (b.name == name) return b;
        return null;
    }

    // ───────────────────────── 2) 웹 화질 ─────────────────────────

    /// <summary>WebGL 기본 품질(Mobile)이 쓰는 URP 설정을 PC에 가깝게 올린다. 렌더링 방식(Forward)은 웹 호환을 위해 그대로 둔다.</summary>
    static string ImproveMobileQuality()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRpPath);
        if (asset == null) return "✗ 웹 화질: Mobile_RPAsset을 찾지 못함";

        var so = new SerializedObject(asset);
        SerializedProperty scale = so.FindProperty("m_RenderScale");
        SerializedProperty lights = so.FindProperty("m_AdditionalLightsPerObjectLimit");
        SerializedProperty soft = so.FindProperty("m_SoftShadowsSupported");
        if (scale == null || lights == null || soft == null) return "✗ 웹 화질: URP 설정 항목 이름이 달라 변경하지 못함";

        if (!File.Exists(SettingsLog))
            File.WriteAllText(SettingsLog, $"renderScale={scale.floatValue}\nlightsPerObject={lights.intValue}\nsoftShadows={soft.boolValue}\n" +
                                           $"webWidth={PlayerSettings.defaultWebScreenWidth}\nwebHeight={PlayerSettings.defaultWebScreenHeight}\ntemplate={PlayerSettings.WebGL.template}\n");

        scale.floatValue = 1f;
        lights.intValue = 8;
        soft.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return "✓ 웹 화질: 렌더 스케일 0.8→1.0, 물체당 조명 4→8, 부드러운 그림자 켬";
    }

    // ───────────────────────── 3) 웹 페이지 ─────────────────────────

    /// <summary>기본 캔버스 크기와 웹 템플릿을 지정한다. 템플릿 폴더가 없으면 템플릿은 건너뛴다.</summary>
    static string ApplyPlayerSettings()
    {
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;

        bool hasTemplate = File.Exists(TemplateFolder + "/index.html");
        if (hasTemplate) PlayerSettings.WebGL.template = TemplateName;
        AssetDatabase.SaveAssets();
        return "✓ 웹 페이지: 캔버스 1280×720" + (hasTemplate ? ", 템플릿 DungeonCore 지정" : " (템플릿 폴더 없음 → 기본 템플릿 유지)");
    }

    // ───────────────────────── 4) 텍스처 용량 ─────────────────────────

    /// <summary>
    /// 빌드 씬 4개가 실제로 쓰는 텍스처만 대상으로, 원본이 1024보다 크고 WebGL 전용 설정이 없는 것에 최대 1024를 지정한다.
    /// UI 스프라이트는 화면에 크게 보이므로 제외한다. PC 빌드 화질에는 영향이 없다.
    /// </summary>
    static string LimitTextureSize()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string[] deps = AssetDatabase.GetDependencies(scenes, true);
        var already = File.Exists(TextureLog) ? new HashSet<string>(File.ReadAllLines(TextureLog)) : new HashSet<string>();
        var changed = new List<string>();
        int skippedSprite = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < deps.Length; i++)
            {
                if (!deps[i].StartsWith("Assets/")) continue;             // 패키지 폴더는 읽기 전용
                // UI 텍스처는 제외: HUD 아이콘처럼 아틀라스의 픽셀 좌표로 잘라 쓴 스프라이트(.asset)는
                // 텍스처가 줄어들면 엉뚱한 영역이 보이게 된다
                if (deps[i].StartsWith("Assets/03.Art/UI/")) continue;
                if (!(AssetImporter.GetAtPath(deps[i]) is TextureImporter importer)) continue;
                if (i % 20 == 0) EditorUtility.DisplayProgressBar("Web Release Prep", "텍스처 확인 중: " + deps[i], (float)i / deps.Length);

                if (importer.textureType == TextureImporterType.Sprite || importer.textureType == TextureImporterType.GUI) { skippedSprite++; continue; }
                if (already.Contains(deps[i])) continue;

                TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings(WebGLPlatform);
                if (web.overridden) continue;                       // 사용자가 직접 정한 WebGL 설정은 건드리지 않는다

                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                int effective = Mathf.Min(Mathf.Max(w, h), importer.maxTextureSize);
                if (effective <= WebMaxTexture) continue;

                TextureImporterPlatformSettings def = importer.GetDefaultPlatformTextureSettings();
                web.overridden = true;
                web.maxTextureSize = WebMaxTexture;
                web.format = TextureImporterFormat.Automatic;
                web.textureCompression = def.textureCompression;
                web.compressionQuality = def.compressionQuality;
                web.crunchedCompression = def.crunchedCompression;
                importer.SetPlatformTextureSettings(web);
                importer.SaveAndReimport();
                changed.Add(deps[i]);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        if (changed.Count > 0) File.AppendAllLines(TextureLog, changed);
        return $"✓ 텍스처: WebGL 최대 {WebMaxTexture} 지정 {changed.Count}개 (이전 실행 포함 총 {already.Count + changed.Count}개, UI 스프라이트 {skippedSprite}개 제외)";
    }

    // ───────────────────────── 되돌리기 ─────────────────────────

    /// <summary>이 도구가 지정한 텍스처 WebGL 설정과 화질·페이지 설정을 원래대로 돌린다. 종료 버튼 연결은 유지한다.</summary>
    [MenuItem("Tools/DungeonCore/Web/Undo Web Release Prep")]
    public static void UndoPrep()
    {
        int restored = 0;
        if (File.Exists(TextureLog))
        {
            foreach (string path in File.ReadAllLines(TextureLog).Distinct())
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.ClearPlatformTextureSettings(WebGLPlatform);
                importer.SaveAndReimport();
                restored++;
            }
            File.Move(TextureLog, TextureLog + ".undone-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        if (File.Exists(SettingsLog))
        {
            var values = File.ReadAllLines(SettingsLog).Where(l => l.Contains('=')).ToDictionary(l => l.Split('=')[0], l => l.Substring(l.IndexOf('=') + 1));
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRpPath);
            if (asset != null)
            {
                var so = new SerializedObject(asset);
                so.FindProperty("m_RenderScale").floatValue = float.Parse(values["renderScale"]);
                so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = int.Parse(values["lightsPerObject"]);
                so.FindProperty("m_SoftShadowsSupported").boolValue = bool.Parse(values["softShadows"]);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
            PlayerSettings.defaultWebScreenWidth = int.Parse(values["webWidth"]);
            PlayerSettings.defaultWebScreenHeight = int.Parse(values["webHeight"]);
            PlayerSettings.WebGL.template = values["template"];
            AssetDatabase.SaveAssets();
            File.Move(SettingsLog, SettingsLog + ".undone-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        EditorUtility.DisplayDialog("Web Release Prep", $"되돌리기 완료\n텍스처 {restored}개 WebGL 설정 해제, 화질·페이지 설정 복원", "확인");
    }
}
