using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CoinRushHUDGenerator
{
    private static bool importing;
    public static bool IsImporting => importing;

    [MenuItem("Coin Rush/Criar HUD e Timer")]
    public static void GenerateHUD() => GenerateHUD(true);

    public static void GenerateHUD(bool saveScene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || importing)
        { Debug.LogWarning("[CoinRush] HUD aguardando fim do Play Mode ou importação de fontes."); return; }
        string scenePath = CoinRushCompleteGameGenerator.ResolveScenePath();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        TMP_FontAsset font = LoadFont();
        if (font == null) { Debug.LogWarning("[CoinRush] HUD aguardando fonte TextMeshPro."); return; }
        TryGenerateHUD(scene, font, saveScene);
    }

    public static bool TryGenerateHUD(Scene scene, TMP_FontAsset font, bool saveScene)
    {
        if (!scene.IsValid() || !scene.isLoaded || font == null)
            throw new System.InvalidOperationException("HUD: cena invalida, nao carregada ou fonte ausente.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || importing)
            throw new System.InvalidOperationException("HUD: Editor em Play Mode ou importacao TMP em andamento.");

        if (!FindSingle(scene, out CoinRushPlayerController player)
            || !FindSingle(scene, out CoinRushCameraFollow view)
            || !FindSingle(scene, out CoinRushCoinSpawner coins)
            || !FindSingle(scene, out CoinRushGameManager manager)
            || !FindSingle(scene, out Canvas canvas)
            || !FindSingle(scene, out EventSystem events)) return false;
        if (player == null || view == null || coins == null)
        { Debug.LogError("[CoinRush] HUD: jogador, camera FPS ou spawner ausente na cena " + scene.path); return false; }

        if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
            throw new System.InvalidOperationException("HUD: nao foi possivel ativar " + scene.path);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Criar HUD e Timer Coin Rush");
        if (manager == null)
        {
            var root = new GameObject("CoinRush GameManager");
            Undo.RegisterCreatedObjectUndo(root, "Criar GameManager");
            manager = Undo.AddComponent<CoinRushGameManager>(root);
        }
        if (canvas == null)
        {
            var root = new GameObject("CoinRush Canvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Criar Canvas");
            canvas = Undo.AddComponent<Canvas>(root);
        }
        Undo.RecordObject(canvas, "Configurar Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvas.enabled = true;
        canvas.gameObject.SetActive(true);
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
        Undo.RecordObject(scaler, "Configurar escala do HUD");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        if (canvas.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

        if (events == null)
        {
            var root = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(root, "Criar EventSystem");
            events = Undo.AddComponent<EventSystem>(root);
        }
        Undo.RecordObject(events, "Configurar EventSystem");
        events.enabled = true;
        events.gameObject.SetActive(true);
        foreach (BaseInputModule old in events.GetComponents<BaseInputModule>())
            if (!(old is InputSystemUIInputModule)) Undo.DestroyObjectImmediate(old);
        InputSystemUIInputModule module = events.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = Undo.AddComponent<InputSystemUIInputModule>(events.gameObject);
        Undo.RecordObject(module, "Configurar Input System UI");
        module.enabled = true;
        ConfigureUIActions(module);

        // Replace only this generator's UI subtree; preserve unrelated Canvas children.
        Transform oldHUD = canvas.transform.Find("CoinRushHUD");
        if (oldHUD != null) Undo.DestroyObjectImmediate(oldHUD.gameObject);
        RectTransform hud = Rect("CoinRushHUD", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        hud.offsetMin = Vector2.zero;
        hud.offsetMax = Vector2.zero;
        Undo.RegisterCreatedObjectUndo(hud.gameObject, "Criar interface Coin Rush");
        TMP_Text score = Label("Score", hud, font, "Pontos: 0", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(24, -20), new Vector2(450, 60), 32, TextAlignmentOptions.Left);
        TMP_Text timer = Label("Timer", hud, font, "00:20", Vector2.one, Vector2.one,
            new Vector2(-24, -20), new Vector2(200, 60), 36, TextAlignmentOptions.Right);
        RectTransform panel = Rect("EndPanel", hud, Vector2.one * 0.5f, Vector2.one * 0.5f,
            Vector2.zero, new Vector2(520, 360));
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.045f, 0.09f, 0.12f, 0.97f);
        Label("Title", panel, font, "Fim de Partida", Vector2.one * 0.5f, Vector2.one * 0.5f,
            new Vector2(0, 110), new Vector2(480, 65), 42, TextAlignmentOptions.Center);
        TMP_Text final = Label("FinalScore", panel, font, "Pontuação final: 0", Vector2.one * 0.5f,
            Vector2.one * 0.5f, new Vector2(0, 35), new Vector2(480, 55), 30, TextAlignmentOptions.Center);
        Button restart = MakeButton("Restart", "Reiniciar", panel, font, new Vector2(-120, -85));
        Button quit = MakeButton("Quit", "Sair", panel, font, new Vector2(120, -85));
        panel.gameObject.SetActive(false);
        Undo.RecordObject(manager, "Configurar partida");
        Undo.RecordObject(coins, "Integrar pontuacao das moedas");
        manager.Configure(player, view, coins, score, timer, panel.gameObject, final, restart, quit);
        manager.enabled = true;
        manager.gameObject.SetActive(true);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(coins);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene && !EditorSceneManager.SaveScene(scene, scene.path))
            throw new System.InvalidOperationException("HUD: nao foi possivel salvar " + scene.path);
        Selection.activeGameObject = canvas.gameObject;
        if (saveScene)
            Debug.Log("Coin Rush: HUD e cronometro salvos. A partida de 20 segundos comeca ao entrar em Play.", manager);
        return true;
    }

    public static bool FindSingle<T>(Scene scene, out T found) where T : Component
    {
        found = null;
        var matches = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) matches.AddRange(root.GetComponentsInChildren<T>(true));
        if (matches.Count > 1)
        {
            string objects = string.Join(", ", matches.ConvertAll(item => item.name + " (ID " + item.GetInstanceID() + ")"));
            Debug.LogError("[CoinRush] Mais de um " + typeof(T).Name + " em '" + scene.path + "': " + objects);
            return false;
        }
        if (matches.Count == 1) found = matches[0];
        return true;
    }

    private static void ConfigureUIActions(InputSystemUIInputModule module)
    {
        // Save actions and references as assets, so UI clicks also work after reopening/building.
        const string path = "Assets/Settings/CoinRushUIActions.asset";
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        if (actions == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            using (var defaults = new DefaultInputActions())
                actions = InputActionAsset.FromJson(defaults.asset.ToJson());
            actions.name = "CoinRushUIActions";
            AssetDatabase.CreateAsset(actions, AssetDatabase.GenerateUniqueAssetPath(path));
        }
        module.actionsAsset = actions;
        module.point = ActionReference(actions, "Point");
        module.leftClick = ActionReference(actions, "Click");
        module.rightClick = ActionReference(actions, "RightClick");
        module.middleClick = ActionReference(actions, "MiddleClick");
        module.scrollWheel = ActionReference(actions, "ScrollWheel");
        module.move = ActionReference(actions, "Navigate");
        module.submit = ActionReference(actions, "Submit");
        module.cancel = ActionReference(actions, "Cancel");
        AssetDatabase.SaveAssetIfDirty(actions);
        EditorUtility.SetDirty(module);
    }

    private static InputActionReference ActionReference(InputActionAsset asset, string name)
    {
        InputAction action = asset.FindAction("UI/" + name, true);
        foreach (Object item in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)))
            if (item is InputActionReference reference && reference.action == action) return reference;
        InputActionReference created = InputActionReference.Create(action);
        created.name = "UI " + name;
        AssetDatabase.AddObjectToAsset(created, asset);
        EditorUtility.SetDirty(asset);
        return created;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var item = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = min == max ? min : Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(name, parent, anchor, anchor, position, size);
        rect.pivot = pivot;
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(1f, 0.91f, 0.65f);
        text.raycastTarget = false;
        return text;
    }

    public static Button MakeButton(string name, string caption, Transform parent, TMP_FontAsset font, Vector2 position)
    {
        RectTransform rect = Rect(name, parent, Vector2.one * 0.5f, Vector2.one * 0.5f, position, new Vector2(200, 65));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.38f, 0.37f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label("Label", rect, font, caption, Vector2.one * 0.5f, Vector2.one * 0.5f,
            Vector2.zero, new Vector2(190, 60), 28, TextAlignmentOptions.Center);
        return button;
    }

    public static System.Action AfterFontImport;
    public static TMP_FontAsset LoadFont()
    {
        if (importing) return null;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font != null && Resources.Load<TMP_Settings>("TMP Settings") != null) return font;
        PackageInfo package = PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
        string archive = package != null ? Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage") : "";
        if (!File.Exists(archive))
        {
            Debug.LogError("Coin Rush: importe Window > TextMeshPro > Import TMP Essential Resources e execute o gerador novamente.");
            return null;
        }
        importing = true;
        AssetDatabase.importPackageCompleted += ImportCompleted;
        AssetDatabase.importPackageFailed += ImportFailed;
        AssetDatabase.importPackageCancelled += ImportCancelled;
        AssetDatabase.ImportPackage(archive, false);
        Debug.Log("Coin Rush: importando recursos essenciais do TextMeshPro. O gerador continuara automaticamente.");
        return null;
    }

    private static void ClearImportHandlers()
    {
        importing = false;
        AssetDatabase.importPackageCompleted -= ImportCompleted;
        AssetDatabase.importPackageFailed -= ImportFailed;
        AssetDatabase.importPackageCancelled -= ImportCancelled;
    }
    private static void ImportCompleted(string name)
    {
        if (!name.Contains("TMP Essential Resources")) return;
        ClearImportHandlers();
        System.Action continuation = AfterFontImport ?? GenerateHUD;
        AfterFontImport = null;
        EditorApplication.delayCall += () => continuation();
    }
    private static void ImportFailed(string name, string error)
    {
        ClearImportHandlers();
        AfterFontImport = null;
        Debug.LogError("[CoinRush] Falha ao importar TextMeshPro (" + name + "): " + error);
    }
    private static void ImportCancelled(string name)
    {
        ClearImportHandlers();
        AfterFontImport = null;
        Debug.LogError("[CoinRush] Importação TextMeshPro cancelada: " + name + ". A geração não foi concluída.");
    }

    [MenuItem("Coin Rush/Criar HUD e Timer", true)]
    private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode && !importing;
}
