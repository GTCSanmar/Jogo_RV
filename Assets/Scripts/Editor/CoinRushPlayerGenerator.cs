using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoinRushPlayerGenerator
{
    private const string ScenePath = "Assets/Scenes/Coin Rush.unity";

    [MenuItem("Coin Rush/Criar Jogador")]
    public static void GeneratePlayer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("Coin Rush: cena nao encontrada em " + ScenePath);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        if (!scene.IsValid() || !scene.isLoaded) return;

        Camera mainCamera = null;
        CoinRushPlayerController player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
            {
                if (!candidate.CompareTag("MainCamera")) continue;
                if (mainCamera != null)
                {
                    Debug.LogError("Coin Rush: mais de uma Main Camera encontrada. Selecione uma unica camera principal.");
                    return;
                }
                mainCamera = candidate;
            }
            foreach (CoinRushPlayerController candidate in root.GetComponentsInChildren<CoinRushPlayerController>(true))
            {
                if (player != null)
                {
                    Debug.LogError("Coin Rush: mais de um controlador de jogador encontrado.");
                    return;
                }
                player = candidate;
            }
        }
        if (mainCamera == null)
        {
            Debug.LogError("Coin Rush: a cena precisa da Main Camera existente com a tag MainCamera. Nenhuma camera foi criada.");
            return;
        }
        if (IsInsideMap(mainCamera.transform) || (player != null && IsInsideMap(player.transform)))
        {
            Debug.LogError("Coin Rush: mantenha Main Camera e jogador fora de Map para preservar o mapa.");
            return;
        }

        BoxCollider floor = CoinRushMapGenerator.RepairMapColliders(scene);
        if (floor == null || !floor.gameObject.activeInHierarchy)
        {
            Debug.LogError("Coin Rush: o chao precisa estar ativo dentro de Map.");
            return;
        }
        Vector3 spawn;
        if (!TryFindSpawn(scene, floor, player != null ? player.transform : null, out spawn))
        {
            Debug.LogError("Coin Rush: nenhum local livre encontrado no chao da arena. O jogador nao foi movido.");
            return;
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (player == null && shader == null)
        {
            Debug.LogError("Coin Rush: shader URP/Lit indisponivel.");
            return;
        }
        if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene)) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Criar jogador Coin Rush");
        if (player == null)
        {
            // Build the capsule while inactive, removing its primitive collider before activation.
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "CoinRush Player";
            capsule.SetActive(false);
            Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
            capsule.transform.position = spawn;
            capsule.AddComponent<CharacterController>();
            capsule.GetComponent<MeshRenderer>().sharedMaterial = PlayerMaterial(shader);
            player = capsule.AddComponent<CoinRushPlayerController>();
            capsule.SetActive(true);
            Undo.RegisterCreatedObjectUndo(capsule, "Criar capsula do jogador");
        }

        // Repair the same player on every run, including one already saved in the scene.
        Undo.RecordObject(player, "Ativar controlador Coin Rush");
        Undo.RecordObject(player.gameObject, "Ativar jogador Coin Rush");
        Undo.RecordObject(player.transform, "Corrigir posicao inicial Coin Rush");
        CharacterController character = player.GetComponent<CharacterController>();
        if (character == null) character = Undo.AddComponent<CharacterController>(player.gameObject);
        Undo.RecordObject(character, "Corrigir CharacterController Coin Rush");
        character.enabled = false;
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null) Undo.DestroyObjectImmediate(body);
        foreach (Collider extra in player.GetComponents<Collider>())
            if (extra != character) Undo.DestroyObjectImmediate(extra);
        Undo.SetTransformParent(player.transform, null, "Normalizar escala do jogador");
        player.transform.localScale = Vector3.one;
        player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
        character.center = Vector3.zero;
        character.height = 2f;
        character.radius = 0.5f;
        character.skinWidth = 0.05f;
        character.stepOffset = 0.3f;
        character.slopeLimit = 45f;
        character.minMoveDistance = 0f;
        character.includeLayers = ~0;
        character.excludeLayers = 0;
        character.detectCollisions = true;
        character.enableOverlapRecovery = true;
        character.enabled = true;
        player.enabled = true;
        player.gameObject.SetActive(true);
        Physics.SyncTransforms();
        EditorUtility.SetDirty(character);

        Undo.RecordObject(mainCamera, "Configurar Main Camera");
        Undo.RecordObject(mainCamera.gameObject, "Ativar Main Camera");
        Undo.RecordObject(mainCamera.transform, "Posicionar Main Camera");
        mainCamera.gameObject.SetActive(true);
        mainCamera.enabled = true;
        mainCamera.orthographic = false;
        mainCamera.fieldOfView = 60f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = Mathf.Max(100f, mainCamera.farClipPlane);
        CoinRushCameraFollow follow = mainCamera.GetComponent<CoinRushCameraFollow>();
        if (follow == null) follow = Undo.AddComponent<CoinRushCameraFollow>(mainCamera.gameObject);
        Undo.RecordObject(follow, "Configurar acompanhamento");
        follow.enabled = true;
        follow.SetTarget(player.transform);
        EditorUtility.SetDirty(player);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("Coin Rush: nao foi possivel salvar a cena. As alteracoes continuam abertas.");
            return;
        }
        Selection.activeGameObject = player.gameObject;
        Debug.Log("Coin Rush: jogador FPS e Main Camera configurados e salvos. Teste mouse, WASD, Shift e Espaco no Play Mode.", player);
    }

    [MenuItem("Coin Rush/Criar Jogador", true)]
    private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static bool IsInsideMap(Transform item)
    {
        for (Transform current = item; current != null; current = current.parent)
            if (current.name == "Map") return true;
        return false;
    }

    private static bool TryFindSpawn(Scene scene, BoxCollider floor, Transform ignoredPlayer, out Vector3 spawn)
    {
        Physics.SyncTransforms();
        PhysicsScene physics = scene.GetPhysicsScene();
        var hits = new Collider[64];
        int[] coordinates = { 0, 3, -3, 6, -6, 9, -9, 12, -12 };
        foreach (int x in coordinates)
        foreach (int z in coordinates)
        {
            if (!floor.Raycast(new Ray(new Vector3(x, floor.bounds.max.y + 4f, z), Vector3.down),
                    out RaycastHit hit, 5f) || hit.normal.y < 0.95f) continue;
            Vector3 center = hit.point + Vector3.up * 1.1f;
            int count = physics.OverlapCapsule(center - Vector3.up * 0.5f, center + Vector3.up * 0.5f,
                0.55f, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            bool blocked = count == hits.Length;
            for (int i = 0; i < count; i++)
            {
                if (ignoredPlayer != null && hits[i].transform.IsChildOf(ignoredPlayer)) continue;
                blocked = true;
            }
            if (blocked) continue;
            spawn = center;
            return true;
        }
        spawn = Vector3.zero;
        return false;
    }

    private static Material PlayerMaterial(Shader shader)
    {
        const string folder = "Assets/Materials/CoinRushPlayer";
        const string path = folder + "/Player.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "CoinRushPlayer");
        material = new Material(shader) { name = "CoinRush Player" };
        material.SetColor("_BaseColor", new Color(0.95f, 0.36f, 0.16f));
        material.SetFloat("_Smoothness", 0.25f);
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
        return material;
    }
}
