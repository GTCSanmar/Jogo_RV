using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoinRushCoinGenerator
{
    private const string ScenePath = "Assets/Scenes/Coin Rush.unity";
    private const string CoinPrefabPath = "Assets/MAR-Low Poly Currency/Prefabs/Cion_02.prefab";

    [MenuItem("Coin Rush/Criar Moedas")]
    public static void GenerateCoins()
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

        CoinRushPlayerController player = null;
        BoxCollider floor = null;
        GameObject coins = null;
        CoinRushCoinSpawner spawner = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item.name == "Map")
            {
                Transform ground = item.Find("Ground/Green Floor");
                if (ground != null) floor = ground.GetComponent<BoxCollider>();
            }
            CoinRushPlayerController candidate = item.GetComponent<CoinRushPlayerController>();
            if (candidate != null)
            {
                if (player != null) { Debug.LogError("Coin Rush: mais de um jogador encontrado."); return; }
                player = candidate;
            }
            if (item.name == "Coins")
            {
                if (coins != null) { Debug.LogError("Coin Rush: mais de um objeto Coins encontrado."); return; }
                coins = item.gameObject;
            }
            CoinRushCoinSpawner system = item.GetComponent<CoinRushCoinSpawner>();
            if (system != null)
            {
                if (spawner != null) { Debug.LogError("Coin Rush: mais de um sistema de moedas encontrado."); return; }
                spawner = system;
            }
        }
        if (floor == null || !floor.enabled || floor.isTrigger || !floor.gameObject.activeInHierarchy || player == null)
        {
            Debug.LogError("Coin Rush: configure o mapa com chao solido e o jogador antes de criar moedas.");
            return;
        }
        if (spawner != null && spawner.gameObject != coins)
        {
            Debug.LogError("Coin Rush: o sistema existente precisa estar no objeto Coins. Nenhum sistema foi duplicado.");
            return;
        }
        if (coins != null && coins.transform.parent != null)
        {
            Debug.LogError("Coin Rush: Coins precisa estar na raiz da cena, fora de Map e do jogador.");
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
        if (prefab == null || prefab.GetComponentInChildren<MeshFilter>(true) == null)
        {
            Debug.LogError("Coin Rush: prefab de moeda MAR nao encontrado ou sem malha em " + CoinPrefabPath);
            return;
        }
        if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene)) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Criar moedas Coin Rush");
        if (coins == null)
        {
            coins = new GameObject("Coins");
            Undo.RegisterCreatedObjectUndo(coins, "Criar Coins");
        }
        else Undo.RegisterFullObjectHierarchyUndo(coins, "Configurar moedas existentes");
        if (spawner == null) spawner = Undo.AddComponent<CoinRushCoinSpawner>(coins);
        Undo.RecordObject(spawner, "Configurar geracao de moedas");
        coins.SetActive(true);
        spawner.enabled = true;
        spawner.Configure(floor, player.transform, prefab);
        spawner.ReplaceCoinVisuals();
        spawner.Populate();
        EditorUtility.SetDirty(spawner);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("Coin Rush: nao foi possivel salvar a cena. As alteracoes continuam abertas.");
            return;
        }
        int count = coins.GetComponentsInChildren<CoinRushCoin>().Length;
        Selection.activeGameObject = coins;
        if (count < CoinRushCoinSpawner.CoinCount)
            Debug.LogWarning("Coin Rush: apenas " + count + " posicoes livres e acessiveis encontradas para moedas.", coins);
        else Debug.Log("Coin Rush: 15 moedas configuradas e cena salva. Cada coleta vale 10 pontos.", coins);
    }

    [MenuItem("Coin Rush/Criar Moedas", true)]
    private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

}
