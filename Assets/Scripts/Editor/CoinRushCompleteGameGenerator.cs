using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CoinRushCompleteGameGenerator
{
    private const string GeneratedFolder = "Assets/CoinRushGenerated";
    public const int SectionCount = 10;
    public const float PreviousCourseLength = 77f;
    public const float SectionLength = 44f;
    public const float SafeLength = 5f;
    public const float CourseLength = SectionCount * SectionLength + (SectionCount - 1) * SafeLength;
    private static string operation;

    [MenuItem("Coin Rush/Gerar Jogo Completo")]
    public static void Generate()
    {
        Debug.Log("[CoinRush] Iniciando geração completa...");
        operation = "verificar estado do Editor e dependencias";
        try { GenerateCore(); }
        catch (Exception error)
        {
            Debug.LogError("[CoinRush] Falha na operação '" + operation + "': " + error.Message);
            Debug.LogException(error);
        }
    }

    private static void GenerateCore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Saia do Play Mode antes de gerar o jogo.");
        string scenePath = ResolveScenePath();
        GameObject lowBarrier = FindImportedPrefab("WoodenPlankBarrier_14_v1", "LowPolyBarriers");
        GameObject movingBarrier = FindImportedPrefab("ConcreteBarrier_05_v1", "LowPolyBarriers");
        GameObject coinPrefab = FindImportedPrefab("Cion_02", "MAR");
        Texture barrierTexture = FindImportedTexture("BarriersMainTexture");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Shader Universal Render Pipeline/Lit nao encontrado.");
        operation = "carregar fonte TextMeshPro";
        CoinRushHUDGenerator.AfterFontImport = Generate;
        TMP_FontAsset font = CoinRushHUDGenerator.LoadFont();
        if (font == null)
        {
            if (!CoinRushHUDGenerator.IsImporting)
                throw new InvalidOperationException("Fonte TMP indisponivel e a importacao nao foi iniciada. Consulte o erro de recursos acima.");
            Debug.LogWarning("[CoinRush] Geração aguardando recursos TextMeshPro. A importação continuará automaticamente; se falhar, veja o erro da importação no Console.");
            return;
        }
        CoinRushHUDGenerator.AfterFontImport = null;

        operation = "abrir a cena " + scenePath;
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            { Debug.LogWarning("[CoinRush] Geração cancelada no diálogo de alterações não salvas."); return; }
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        if (!scene.IsValid() || !scene.isLoaded || scene.path != scenePath)
            throw new InvalidOperationException("Cena alvo invalida ou nao carregada: " + scenePath);
        Debug.Log($"[CoinRush] Cena alvo: {scene.path}; aberta={scene.isLoaded}; alterações não salvas={scene.isDirty}; ativa={SceneManager.GetActiveScene() == scene}.");
        operation = "verificar sistemas existentes em " + scenePath;
        if (!CoinRushHUDGenerator.FindSingle(scene, out CoinRushPlayerController player)
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushCameraFollow view)
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushCoinSpawner coins)
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushGameManager manager)
            || !CoinRushHUDGenerator.FindSingle(scene, out Canvas existingCanvas)
            || !CoinRushHUDGenerator.FindSingle(scene, out UnityEngine.EventSystems.EventSystem existingEvents)
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushGeneratedCourse oldCourse))
            throw new InvalidOperationException("Sistemas duplicados na cena alvo. Consulte os objetos informados acima.");
        Camera camera = null;
        var sceneCameras = new List<Camera>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
        {
            sceneCameras.Add(candidate);
            if (!candidate.CompareTag("MainCamera")) continue;
            if (camera != null) throw new InvalidOperationException("Mais de uma Main Camera em " + scenePath);
            camera = candidate;
        }
        if (camera == null)
        {
            if (view != null) camera = view.GetComponent<Camera>();
            else if (sceneCameras.Count == 1) camera = sceneCameras[0];
            else if (sceneCameras.Count > 1)
                throw new InvalidOperationException("Ha varias cameras sem uma Main Camera identificavel em " + scenePath);
        }
        if (view != null && camera != null && view.gameObject != camera.gameObject)
            throw new InvalidOperationException("O controlador FPS nao esta na Main Camera existente.");
        // SetActiveScene need not be called when the correct scene is already active.
        if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
            throw new InvalidOperationException("Nao foi possivel ativar " + scenePath);

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Gerar jogo completo Coin Rush");
        try
        {
            operation = "preparar materiais e preservar sistemas existentes";
            foreach (Component existing in new Component[] { player, camera, coins, manager, existingCanvas, existingEvents })
                if (existing != null) Undo.RegisterFullObjectHierarchyUndo(existing.gameObject, "Preservar configuracao existente");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets", "CoinRushGenerated");
            if (coinPrefab == null) coinPrefab = CreateFallbackCoinPrefab(shader);
            Material ground = Material("CourseFloor", new Color(0.22f, 0.36f, 0.39f), shader);
            Material wall = Material("CourseWall", new Color(0.13f, 0.20f, 0.29f), shader);
            Material safe = Material("Safezone", new Color(0.15f, 0.7f, 0.48f), shader);
            Material platform = Material("Platforms", new Color(0.2f, 0.55f, 0.8f), shader);
            Material gold = Material("Finish", new Color(1f, 0.72f, 0.08f), shader);
            Material barrier = Material("BarriersURP", Color.white, shader, barrierTexture);
            Material lethal = Material("LethalBarriersURP", new Color(1f, 0.3f, 0.25f), shader, barrierTexture);

            EnsurePlayer(ref player, ref camera, ref view, scene);
            foreach (Component system in new Component[] { player, camera, coins, manager, existingCanvas, existingEvents })
            {
                if (system == null || system.transform.parent == null) continue;
                if ((oldCourse != null && system.transform.IsChildOf(oldCourse.transform)) || system.transform.root.name == "Map")
                    Undo.SetTransformParent(system.transform, null, "Preservar sistemas antes de substituir mapa");
            }
            operation = "remover objetos gerados do mapa antigo";
            Debug.Log("[CoinRush] Removendo mapa antigo...");
            RemoveLegacyMap(scene);
            if (coins != null)
                foreach (CoinRushCoin coin in coins.GetComponentsInChildren<CoinRushCoin>(true))
                    Undo.DestroyObjectImmediate(coin.gameObject);
            if (oldCourse != null) Undo.DestroyObjectImmediate(oldCourse.gameObject);
            operation = "criar geometria do percurso linear";
            Debug.Log("[CoinRush] Criando percurso linear...");
            var course = new GameObject("CoinRush Linear Course");
            course.AddComponent<CoinRushGeneratedCourse>();
            Undo.RegisterCreatedObjectUndo(course, "Criar percurso linear");
            Transform rootCourse = course.transform;
            Transform geometry = Group("Geometry", rootCourse);
            Transform obstacles = Group("Obstacles", rootCourse);
            Transform zones = Group("Safezones", rootCourse);
            Transform slots = Group("Coin Positions", rootCourse);

            if (coins == null)
            {
                var root = new GameObject("Coins");
                Undo.RegisterCreatedObjectUndo(root, "Criar sistema de moedas");
                coins = Undo.AddComponent<CoinRushCoinSpawner>(root);
            }
            if (coins.transform.parent != null)
                Undo.SetTransformParent(coins.transform, null, "Preservar sistema de moedas");
            coins.gameObject.name = "Coins";
            coins.gameObject.SetActive(true);

            operation = "criar dez trechos, nove checkpoints e oito tipos de armadilhas";
            Debug.Log("[CoinRush] Criando armadilhas e safezones...");
            BuildExpandedCourse(geometry, obstacles, zones, slots, lowBarrier, movingBarrier,
                ground, wall, safe, platform, gold, barrier, lethal, font,
                out Transform[] spawnPoints, out GameObject[] gates, out List<Transform> coinPoints);
            Debug.Log("[CoinRush] Configurando jogador, moedas e interfaces...");
            Undo.RecordObject(coins, "Distribuir moedas pelo percurso");
            operation = "configurar e distribuir moedas MAR";
            coins.ConfigureLinear(player.transform, coinPrefab, coinPoints.ToArray());
            coins.Populate();
            Undo.RecordObject(player.transform, "Posicionar jogador no inicio");
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(spawnPoints[0].position, Quaternion.identity);
            controller.enabled = true;
            player.enabled = true;
            view.enabled = true;
            view.SetTarget(player.transform);

            // Reuse the existing setup code for the single Canvas/EventSystem and persistent UI input actions.
            operation = "criar Canvas, EventSystem e HUD na cena " + scenePath;
            if (!CoinRushHUDGenerator.TryGenerateHUD(scene, font, false))
                throw new InvalidOperationException("O gerador de HUD nao concluiu a configuracao na cena " + scenePath);
            CoinRushHUDGenerator.FindSingle(scene, out manager);
            CoinRushHUDGenerator.FindSingle(scene, out Canvas canvas);
            if (manager == null || canvas == null) throw new InvalidOperationException("Falha ao montar HUD/GameManager.");
            operation = "montar menus e configurar GameManager";
            BuildMenus(canvas, manager, rootCourse, spawnPoints, gates, font);
            foreach (CoinRushCourseElement element in rootCourse.GetComponentsInChildren<CoinRushCourseElement>(true))
                element.manager = manager;
            ConfigureLighting(scene, rootCourse);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.76f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.52f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.3f, 0.33f);
            Physics.SyncTransforms();
            operation = "validar colliders, triggers e referencias da cena " + scenePath;
            Validate(rootCourse, spawnPoints, gates, coinPoints, scene);
            EditorUtility.SetDirty(coins);
            EditorUtility.SetDirty(manager);
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = course;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0, 2, CourseLength * 0.5f), new Vector3(9, 9, CourseLength + 3)), false);
            operation = "salvar a cena " + scenePath;
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Nao foi possivel salvar " + scenePath);
            Debug.Log("[CoinRush] Geração concluída com sucesso!", course);
            Debug.Log("[CoinRush] Cena salva: " + scene.path + ". Pressione Play e clique em Jogar.", course);
        }
        catch (Exception error)
        {
            Debug.LogError("[CoinRush] Falha na operação '" + operation + "', cena '" + scenePath + "': " + error.Message);
            Debug.LogException(error);
            try { Undo.RevertAllDownToGroup(undo); }
            catch (Exception rollbackError)
            {
                Debug.LogError("[CoinRush] Também falhou a reversão da geração: " + rollbackError.Message);
                Debug.LogException(rollbackError);
            }
        }
    }

    private static void BuildExpandedCourse(Transform geometry, Transform obstacles, Transform zones, Transform slots,
        GameObject lowPrefab, GameObject highPrefab, Material ground, Material wall, Material safe,
        Material platform, Material gold, Material barrier, Material lethal, TMP_FontAsset font,
        out Transform[] spawnPoints, out GameObject[] gates, out List<Transform> coinPoints)
    {
        spawnPoints = new Transform[SectionCount];
        gates = new GameObject[SectionCount - 1];
        coinPoints = new List<Transform>();
        spawnPoints[0] = Group("Start Spawn", zones);
        spawnPoints[0].position = new Vector3(0, 1.1f, 2);
        string[] titles = { "PULE AS BARREIRAS", "DESVIE EM ZIGUE-ZAGUE", "ATRAVESSE OS BURACOS",
            "AS PLATAFORMAS SOMEM", "BARRAS GIRATÓRIAS", "MARTELOS PENDULARES", "BARREIRAS MÓVEIS",
            "ESPERE ABRIR E CORRA", "SALTOS E DESVIOS", "DESAFIO FINAL" };
        Cube("Left Wall", geometry, new Vector3(-3.25f, 3, CourseLength / 2), new Vector3(0.5f, 6, CourseLength), wall);
        Cube("Right Wall", geometry, new Vector3(3.25f, 3, CourseLength / 2), new Vector3(0.5f, 6, CourseLength), wall);
        Cube("Start Wall", geometry, new Vector3(0, 3, -0.25f), new Vector3(7, 6, 0.5f), wall);
        Cube("End Wall", geometry, new Vector3(0, 3, CourseLength + 0.25f), new Vector3(7, 6, 0.5f), wall);
        GameObject roof = Cube("Invisible Ceiling", geometry, new Vector3(0, 6.1f, CourseLength / 2), new Vector3(7, 0.2f, CourseLength + 1), wall);
        roof.GetComponent<Renderer>().enabled = false;
        for (int section = 0; section < SectionCount; section++)
        {
            float start = section * (SectionLength + SafeLength);
            Transform sectionRoot = Group("Section " + (section + 1) + " - " + titles[section], obstacles);
            bool pit = section == 2 || section == 3 || section == 8;
            if (pit)
            {
                CourseFloor("Approach " + section, geometry, start, start + 10, ground);
                CourseFloor("Landing " + section, geometry, start + 27, start + SectionLength, ground);
                foreach (float local in new[] { 11.5f, 16f, 20.5f, 25f })
                {
                    GameObject tile = Cube("Crossing Platform", sectionRoot, new Vector3(0, -0.2f, start + local), new Vector3(6, 0.4f, 3), platform);
                    if (section != 2)
                    {
                        var element = tile.AddComponent<CoinRushCourseElement>();
                        element.kind = CoinRushCourseElement.ElementKind.DisappearingPlatform;
                        element.collapseDelay = 1.1f;
                    }
                }
            }
            else CourseFloor("Floor " + (section + 1), geometry, start, start + SectionLength, ground);

            if (section == 0)
                foreach (float local in new[] { 8f, 16f, 24f, 32f })
                    Barrier("Low Jump Barrier", lowPrefab, sectionRoot, new Vector3(0, 0.425f, start + local), new Vector3(6, 0.85f, 0.5f), barrier);
            if (section == 1)
                for (int i = 0; i < 4; i++)
                    Barrier("High Zigzag Barrier", highPrefab, sectionRoot, new Vector3(i % 2 == 0 ? 1.2f : -1.2f, 1.75f, start + 8 + i * 8),
                        new Vector3(3.6f, 3.5f, 0.6f), barrier);
            if (section == 4 || section == 9)
                foreach (float local in section == 4 ? new[] { 10f, 24f, 36f } : new[] { 24f })
                {
                    GameObject bar = Cube("Lethal Rotating Bar", sectionRoot, new Vector3(0, 1.05f, start + local), new Vector3(1, 1, 1), lethal);
                    bar.transform.localScale = Vector3.one;
                    Object.DestroyImmediate(bar.GetComponent<Renderer>());
                    Object.DestroyImmediate(bar.GetComponent<MeshFilter>());
                    bar.GetComponent<BoxCollider>().size = new Vector3(4.2f, 0.35f, 0.45f);
                    GameObject visual = Cube("Rotating Beam", bar.transform, bar.transform.position, new Vector3(4.2f, 0.35f, 0.45f), lethal);
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                    AddMechanism(bar, CoinRushCourseElement.MotionKind.Rotate, 4f);
                }
            if (section == 5)
                foreach (float local in new[] { 10f, 24f, 36f })
                {
                    Transform pivot = Group("Swinging Hammer", sectionRoot);
                    pivot.position = new Vector3(0, 3.6f, start + local);
                    GameObject shaft = Cube("Hammer Shaft", pivot, pivot.position + Vector3.down * 1.4f, new Vector3(0.18f, 2.8f, 0.18f), barrier);
                    GameObject head = Cube("Lethal Hammer Head", pivot, pivot.position + Vector3.down * 2.8f, new Vector3(1.1f, 1, 0.8f), lethal);
                    shaft.GetComponent<BoxCollider>().isTrigger = true;
                    head.GetComponent<BoxCollider>().isTrigger = true;
                    Rigidbody body = pivot.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    var element = pivot.gameObject.AddComponent<CoinRushCourseElement>();
                    element.kind = CoinRushCourseElement.ElementKind.Hazard;
                    element.motion = CoinRushCourseElement.MotionKind.Swing;
                    element.period = 3.2f;
                    element.swingAngle = 60;
                }
            if (section == 6 || section == 8)
                foreach (float local in section == 6 ? new[] { 8f, 16f, 24f, 32f, 36f } : new[] { 34f })
                {
                    GameObject moving = Barrier("Lateral Lethal Barrier", highPrefab, sectionRoot,
                        new Vector3(0, 1.4f, start + local), new Vector3(1.4f, 2.8f, 0.65f), lethal);
                    var element = AddMechanism(moving, CoinRushCourseElement.MotionKind.Lateral, 3f);
                    element.lateralTravel = 2.1f;
                }
            if (section == 7 || section == 9)
                foreach (float local in section == 7 ? new[] { 10f, 24f, 36f } : new[] { 36f })
                {
                    Cube("Timed Gate Warning Stripe", geometry, new Vector3(0, 0.015f, start + local), new Vector3(6, 0.03f, 0.7f), gold);
                    GameObject timed = Cube("Timed Lethal Gate", sectionRoot, new Vector3(0, 1.6f, start + local), new Vector3(6, 3.2f, 0.45f), lethal);
                    var element = AddMechanism(timed, CoinRushCourseElement.MotionKind.Timed, 4f);
                    element.activeFraction = 0.25f; // 1 second active, 3 seconds clear.
                }
            if (section == 8 || section == 9)
                Barrier("Combination Jump", lowPrefab, sectionRoot, new Vector3(0, 0.4f, start + (section == 8 ? 6.5f : 8f)), new Vector3(6, 0.8f, 0.5f), barrier);
            if (section == 9)
                Barrier("Final High Detour", highPrefab, sectionRoot, new Vector3(1.2f, 1.75f, start + 16), new Vector3(3.6f, 3.5f, 0.6f), barrier);

            float[] localCoins = pit ? new[] { 5f, 11.5f, 16f, 20.5f, 39f } : new[] { 5f, 13f, 21f, 29f, 41f };
            foreach (float local in localCoins)
            {
                Transform slot = Group("Coin Slot " + coinPoints.Count, slots);
                slot.position = new Vector3(0, 1.05f, start + local);
                coinPoints.Add(slot);
            }
            Sign("TRECHO " + (section + 1) + "/10\n" + titles[section], zones, font, new Vector3(0, 4.8f, start + 3));
            if (section >= SectionCount - 1) continue;
            float center = start + SectionLength + SafeLength / 2;
            CourseFloor("Safe Floor " + (section + 1), geometry, start + SectionLength, start + SectionLength + SafeLength, safe);
            GameObject zone = Cube("Safezone " + (section + 1), zones, new Vector3(0, 3, center), new Vector3(6, 6, 3), safe);
            zone.GetComponent<Renderer>().enabled = false;
            MakeTrigger(zone);
            var sensor = zone.AddComponent<CoinRushCourseElement>();
            sensor.kind = CoinRushCourseElement.ElementKind.Safezone;
            sensor.checkpointIndex = section + 1;
            spawnPoints[section + 1] = Group("Checkpoint Spawn " + (section + 1), zones);
            spawnPoints[section + 1].position = new Vector3(0, 1.1f, center);
            gates[section] = Cube("Checkpoint Back Gate " + (section + 1), zones, new Vector3(0, 3, center - 2.7f), new Vector3(6, 6, 0.3f), wall);
            gates[section].SetActive(false);
            Sign("ÁREA SEGURA\nCHECKPOINT " + (section + 1), zones, font, new Vector3(0, 3.8f, center + 1.2f));
        }
        GameObject finish = Cube("Finish Trigger", zones, new Vector3(0, 3, CourseLength - 1.5f), new Vector3(6, 6, 1), gold);
        finish.GetComponent<Renderer>().enabled = false;
        MakeTrigger(finish);
        finish.AddComponent<CoinRushCourseElement>().kind = CoinRushCourseElement.ElementKind.Finish;
        Cube("Finish Line", geometry, new Vector3(0, 0.015f, CourseLength - 2), new Vector3(6, 0.03f, 0.6f), gold);
        Sign("CHEGADA", zones, font, new Vector3(0, 4, CourseLength - 0.5f));
    }

    private static void CourseFloor(string name, Transform parent, float start, float end, Material material)
        => Cube(name, parent, new Vector3(0, -0.25f, (start + end) / 2), new Vector3(6, 0.5f, end - start), material);

    private static CoinRushCourseElement AddMechanism(GameObject root, CoinRushCourseElement.MotionKind motion, float period)
    {
        MakeTrigger(root);
        var element = root.AddComponent<CoinRushCourseElement>();
        element.kind = CoinRushCourseElement.ElementKind.Hazard;
        element.motion = motion;
        element.period = period;
        return element;
    }

    public static string ResolveScenePath()
    {
        string[] candidates = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path).Replace(" ", "").Replace("_", "").Replace("-", "");
                return name.Equals("CoinRush", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("CoinRush3D", StringComparison.OrdinalIgnoreCase);
            }).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 1) return candidates[0];
        string active = SceneManager.GetActiveScene().path;
        if (candidates.Contains(active))
        {
            Debug.Log("[CoinRush] Mais de uma cena compatível encontrada; utilizando a cena ativa: " + active);
            return active;
        }
        if (candidates.Length == 0)
            throw new FileNotFoundException("Nenhuma cena Coin Rush encontrada dentro de Assets/Scenes. Cenas encontradas: "
                + string.Join(", ", AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath)));
        throw new InvalidOperationException("Cena Coin Rush ambigua. Abra a cena desejada antes de gerar: " + string.Join(", ", candidates));
    }

    private static GameObject FindImportedPrefab(string name, string packageHint)
    {
        string[] matches = AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path).Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => path.IndexOf(packageHint, StringComparison.OrdinalIgnoreCase) >= 0)
            .ThenBy(path => path, StringComparer.Ordinal).ToArray();
        if (matches.Length == 0)
        {
            Debug.LogError("[CoinRush] Prefab ausente: '" + name + "', pacote esperado '" + packageHint
                + "'. Busca realizada dentro de Assets. Será utilizado um modelo básico provisório; a geração continuará.");
            return null;
        }
        Debug.Log("[CoinRush] Prefab localizado: " + matches[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(matches[0]);
        MeshFilter mesh = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
        if (prefab == null || mesh == null || mesh.sharedMesh == null)
        {
            Debug.LogError("[CoinRush] Prefab sem malha utilizável: " + matches[0] + ". Será utilizado um modelo básico provisório.");
            return null;
        }
        return prefab;
    }

    private static Texture FindImportedTexture(string name)
    {
        string path = AssetDatabase.FindAssets(name + " t:Texture", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(item => Path.GetFileNameWithoutExtension(item).Equals(name, StringComparison.OrdinalIgnoreCase));
        if (path == null)
        {
            Debug.LogWarning("[CoinRush] Textura ausente: " + name + ". Será utilizado material URP de cor sólida.");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<Texture>(path);
    }

    private static GameObject CreateFallbackCoinPrefab(Shader shader)
    {
        const string path = GeneratedFolder + "/FallbackCoinVisual.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject temporary = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        try
        {
            temporary.name = "Fallback Coin Visual";
            temporary.transform.localScale = new Vector3(0.8f, 0.075f, 0.8f);
            Object.DestroyImmediate(temporary.GetComponent<Collider>());
            temporary.GetComponent<Renderer>().sharedMaterial = Material("FallbackGold", new Color(1f, 0.7f, 0.05f), shader);
            GameObject result = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            if (result == null) throw new InvalidOperationException("Nao foi possivel salvar o prefab provisorio de moeda: " + path);
            return result;
        }
        finally { Object.DestroyImmediate(temporary); }
    }

    private static void RemoveLegacyMap(Scene scene)
    {
        string[] ownedPaths =
        {
            "Ground/Green Floor", "Lighting/Sun",
            "Boundaries/North Wall", "Boundaries/South Wall", "Boundaries/East Wall", "Boundaries/West Wall",
            "Boundaries/North Cap", "Boundaries/South Cap", "Boundaries/East Cap", "Boundaries/West Cap",
            "Obstacles/Crate 01", "Obstacles/Crate 02", "Obstacles/Crate 03", "Obstacles/Crate 04",
            "Obstacles/West Platform", "Obstacles/West Step", "Obstacles/East Platform", "Obstacles/East Step",
            "Obstacles/North Block", "Obstacles/South Block"
        };
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Map" || root.transform.Find("Ground/Green Floor") == null) continue;
            foreach (string path in ownedPaths)
            {
                Transform item = root.transform.Find(path);
                if (item == null) continue;
                // Custom scripts/children are not owned by the previous map generator.
                if (item.GetComponents<MonoBehaviour>().Any(script => script == null || !(script is UniversalAdditionalLightData)))
                {
                    Debug.LogWarning("[CoinRush] Objeto antigo com scripts personalizados preservado: Map/" + path, item);
                    continue;
                }
                foreach (Transform child in item.Cast<Transform>().ToArray())
                    Undo.SetTransformParent(child, root.transform, "Preservar adicao personalizada ao mapa");
                Undo.DestroyObjectImmediate(item.gameObject);
            }
            foreach (string group in new[] { "Ground", "Boundaries", "Obstacles", "Lighting" })
            {
                Transform child = root.transform.Find(group);
                if (child != null && child.childCount == 0 && child.GetComponents<Component>().Length == 1)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
            if (root.transform.childCount == 0 && root.GetComponents<Component>().Length == 1)
                Undo.DestroyObjectImmediate(root);
            else Debug.Log("[CoinRush] Conteúdo personalizado de Map foi preservado.", root);
        }
    }

    private static void EnsurePlayer(ref CoinRushPlayerController player, ref Camera camera, ref CoinRushCameraFollow view, Scene scene)
    {
        if (player == null)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "CoinRush Player";
            Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
            player = capsule.AddComponent<CoinRushPlayerController>();
            Undo.RegisterCreatedObjectUndo(capsule, "Criar jogador FPS");
        }
        Undo.RecordObject(player.transform, "Configurar jogador FPS");
        if (player.transform.parent != null) Undo.SetTransformParent(player.transform, null, "Preservar jogador FPS");
        player.gameObject.SetActive(true);
        player.transform.localScale = Vector3.one;
        CharacterController controller = player.GetComponent<CharacterController>();
        Undo.RecordObject(controller, "Configurar CharacterController");
        controller.height = 2;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;
        controller.skinWidth = 0.05f;
        controller.stepOffset = 0.3f;
        controller.minMoveDistance = 0;
        controller.detectCollisions = true;
        controller.enabled = true;
        if (camera == null)
        {
            GameObject root = new GameObject("Main Camera");
            root.tag = "MainCamera";
            camera = root.AddComponent<Camera>();
            root.AddComponent<AudioListener>();
            Undo.RegisterCreatedObjectUndo(root, "Criar Main Camera");
        }
        if (camera.transform.parent != null) Undo.SetTransformParent(camera.transform, null, "Preservar Main Camera");
        camera.gameObject.SetActive(true);
        camera.tag = "MainCamera";
        camera.enabled = true;
        camera.nearClipPlane = 0.1f;
        if (view == null) view = Undo.AddComponent<CoinRushCameraFollow>(camera.gameObject);
        CoinRushCharacterVisualSetup.Apply(player, view);
    }

    private static void BuildMenus(Canvas canvas, CoinRushGameManager manager, Transform course,
        Transform[] checkpoints, GameObject[] gates, TMP_FontAsset font)
    {
        Transform hud = canvas.transform.Find("CoinRushHUD");
        TMP_Text stage = CoinRushHUDGenerator.Label("Stage", hud, font, "Trecho 1/10 | Checkpoint 0/9", new Vector2(0.5f, 1),
            new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(520, 85), 23, TextAlignmentOptions.Center);
        RectTransform menu = MenuPanel("MainMenu", hud, font, "Coin Rush 3D");
        CoinRushHUDGenerator.Label("Instructions", menu, font, "WASD · Shift para correr · Espaço para pular\nMouse para olhar · ESC para pausar\nConclua cada trecho em 20 segundos.", Vector2.one * 0.5f,
            Vector2.one * 0.5f, new Vector2(0, 25), new Vector2(600, 100), 22, TextAlignmentOptions.Center);
        Button play = CoinRushHUDGenerator.MakeButton("Play", "Jogar", menu, font, new Vector2(-120, -100));
        Button quit = CoinRushHUDGenerator.MakeButton("Quit", "Sair", menu, font, new Vector2(120, -100));
        RectTransform pause = MenuPanel("PauseMenu", hud, font, "Pausa");
        Button resume = CoinRushHUDGenerator.MakeButton("Continue", "Continuar", pause, font, new Vector2(0, 40));
        Button restart = CoinRushHUDGenerator.MakeButton("Restart", "Reiniciar", pause, font, new Vector2(0, -40));
        Button back = CoinRushHUDGenerator.MakeButton("MainMenu", "Menu principal", pause, font, new Vector2(0, -120));
        Transform outcome = hud.Find("EndPanel");
        ((RectTransform)outcome).sizeDelta = new Vector2(680, 400);
        TMP_Text final = outcome.Find("FinalScore").GetComponent<TMP_Text>();
        ((RectTransform)final.transform).sizeDelta = new Vector2(640, 100);
        final.fontSize = 25;
        TMP_Text retry = outcome.Find("Restart/Label").GetComponent<TMP_Text>();
        retry.fontSize = 22;
        outcome.Find("Quit/Label").GetComponent<TMP_Text>().text = "Menu principal";
        outcome.Find("Quit/Label").GetComponent<TMP_Text>().fontSize = 22;
        CoinRushAudioManager audio = CoinRushAudioSetup.Configure(manager);
        AudioSource music = audio.MusicSource;
        manager.ConfigureCourse(course, checkpoints, gates, menu.gameObject, pause.gameObject, stage,
            outcome.Find("Title").GetComponent<TMP_Text>(), play, quit, resume, restart, back, music);
        pause.gameObject.SetActive(false);
        outcome.gameObject.SetActive(false);
        menu.gameObject.SetActive(true);
    }

    private static RectTransform MenuPanel(string name, Transform hud, TMP_FontAsset font, string title)
    {
        RectTransform rect = CoinRushHUDGenerator.Rect(name, hud, Vector2.one * 0.5f, Vector2.one * 0.5f,
            Vector2.zero, new Vector2(680, 420));
        rect.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.13f, 0.98f);
        CoinRushHUDGenerator.Label("Title", rect, font, title, Vector2.one * 0.5f, Vector2.one * 0.5f,
            new Vector2(0, 140), new Vector2(640, 65), 42, TextAlignmentOptions.Center);
        return rect;
    }

    private static Transform Group(string name, Transform parent)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static GameObject Barrier(string name, GameObject prefab, Transform parent, Vector3 center, Vector3 size, Material material)
    {
        if (prefab == null) return Cube(name + " (provisório)", parent, center, size, material);
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = center;
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = renderer.bounds; first = false; }
            else bounds.Encapsulate(renderer.bounds);
            renderer.sharedMaterial = material;
        }
        if (first)
        {
            Debug.LogError("[CoinRush] Prefab sem Renderer: " + AssetDatabase.GetAssetPath(prefab) + ". Usando obstáculo básico provisório.");
            Object.DestroyImmediate(root);
            return Cube(name + " (provisório)", parent, center, size, material);
        }
        Vector3 scale = new Vector3(size.x / Mathf.Max(0.001f, bounds.size.x),
            size.y / Mathf.Max(0.001f, bounds.size.y), size.z / Mathf.Max(0.001f, bounds.size.z));
        model.transform.localPosition = Vector3.Scale(center - bounds.center, scale);
        model.transform.localScale = Vector3.Scale(model.transform.localScale, scale);
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        root.AddComponent<BoxCollider>().size = size;
        return root;
    }

    private static void MakeTrigger(GameObject item)
    {
        item.GetComponent<BoxCollider>().isTrigger = true;
        Rigidbody body = item.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private static Material Material(string name, Color color, Shader shader, Texture texture = null)
    {
        string path = GeneratedFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.25f);
        if (texture != null) material.SetTexture("_BaseMap", texture);
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
        return material;
    }

    private static void Sign(string caption, Transform parent, TMP_FontAsset font, Vector3 position)
    {
        GameObject root = new GameObject(caption);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        TextMeshPro text = root.AddComponent<TextMeshPro>();
        text.font = font;
        text.text = caption;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 3.5f;
        text.rectTransform.sizeDelta = new Vector2(3.8f, 1.2f);
        text.color = Color.white;
    }

    private static void ConfigureLighting(Scene scene, Transform parent)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Light light in root.GetComponentsInChildren<Light>())
            if (light.type == LightType.Directional && light.enabled && light.gameObject.activeInHierarchy) return;
        var sun = new GameObject("Course Sun");
        sun.transform.SetParent(parent, false);
        sun.transform.rotation = Quaternion.Euler(50, -25, 0);
        Light source = sun.AddComponent<Light>();
        source.type = LightType.Directional;
        source.intensity = 1.2f;
        source.shadows = LightShadows.Soft;
    }

    private static void Validate(Transform course, Transform[] checkpoints, GameObject[] gates, List<Transform> slots, Scene scene)
    {
        foreach (Transform item in course.GetComponentsInChildren<Transform>(true))
            if (item.gameObject.scene != scene)
                throw new InvalidOperationException("Objeto criado na cena errada: " + item.name + " em " + item.gameObject.scene.path);
        if (!CoinRushHUDGenerator.FindSingle(scene, out CoinRushPlayerController player) || player == null
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushCameraFollow camera) || camera == null
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushCoinSpawner spawner) || spawner == null
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushGameManager manager) || manager == null
            || !CoinRushHUDGenerator.FindSingle(scene, out Canvas canvas) || canvas == null
            || !CoinRushHUDGenerator.FindSingle(scene, out UnityEngine.EventSystems.EventSystem events) || events == null)
            throw new InvalidOperationException("A validacao final encontrou sistemas ausentes ou duplicados em " + scene.path);
        if (spawner.GetComponentsInChildren<CoinRushCoin>().Length != slots.Count)
            throw new InvalidOperationException("Quantidade de moedas criada difere dos pontos configurados em " + scene.path);
        if (checkpoints.Length != SectionCount || gates.Length != SectionCount - 1 || slots.Count != SectionCount * 5)
            throw new InvalidOperationException("Quantidade incorreta de checkpoints, portoes ou moedas.");
        for (int i = 1; i < checkpoints.Length; i++)
            if (checkpoints[i].position.z <= checkpoints[i - 1].position.z)
                throw new InvalidOperationException("Checkpoints fora de ordem.");
        foreach (CoinRushCourseElement element in course.GetComponentsInChildren<CoinRushCourseElement>(true))
        {
            if (element.manager == null) throw new InvalidOperationException("Elemento sem GameManager.");
            BoxCollider[] colliders = element.GetComponentsInChildren<BoxCollider>(true);
            if (colliders.Length == 0 || colliders.Any(collider => !collider.enabled))
                throw new InvalidOperationException("Elemento sem BoxCollider ativo: " + element.name);
            if (element.kind != CoinRushCourseElement.ElementKind.DisappearingPlatform && colliders.Any(collider => !collider.isTrigger))
                throw new InvalidOperationException("Sensor sem Is Trigger: " + element.name);
            if (element.motion != CoinRushCourseElement.MotionKind.None && element.GetComponent<Rigidbody>() == null)
                throw new InvalidOperationException("Mecanismo movel sem Rigidbody cinematico: " + element.name);
        }
        Physics.SyncTransforms();
        foreach (Transform slot in slots)
            foreach (Collider hit in Physics.OverlapSphere(slot.position, 0.42f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (hit.gameObject.scene == scene && hit.transform.IsChildOf(course))
                    throw new InvalidOperationException("Moeda dentro de obstaculo: " + slot.name);
        foreach (Transform checkpoint in checkpoints)
        {
            Vector3 feet = checkpoint.position - Vector3.up;
            if (!Physics.Raycast(feet + Vector3.up * 0.2f, Vector3.down, out RaycastHit support, 0.5f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore) || !support.transform.IsChildOf(course))
                throw new InvalidOperationException("Checkpoint sem piso seguro: " + checkpoint.name);
            foreach (Collider hit in Physics.OverlapCapsule(checkpoint.position - Vector3.up * 0.5f,
                checkpoint.position + Vector3.up * 0.5f, 0.5f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (hit.transform.IsChildOf(course)) throw new InvalidOperationException("Checkpoint bloqueado: " + checkpoint.name);
        }
        if (CourseLength < PreviousCourseLength * 5f) throw new InvalidOperationException("Percurso menor que cinco vezes o anterior.");
        Debug.Log($"[CoinRush] Validados {SectionCount} trechos, {gates.Length} checkpoints, {slots.Count} moedas. Comprimento anterior: {PreviousCourseLength}m; novo: {CourseLength}m ({CourseLength / PreviousCourseLength:0.00}x).");
    }

    [MenuItem("Coin Rush/Gerar Jogo Completo", true)]
    private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
