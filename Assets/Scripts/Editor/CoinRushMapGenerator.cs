using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Creates only the arena, in its own scene, without changing project settings.</summary>
public static class CoinRushMapGenerator
{
    private const string ScenePath = "Assets/Scenes/Coin Rush.unity";
    private const string MaterialFolder = "Assets/Materials/CoinRushMap";

    [MenuItem("Coin Rush/Gerar Mapa")]
    public static void GenerateMap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Coin Rush: saia do Play Mode antes de gerar o mapa.");
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("Coin Rush: shader URP/Lit indisponivel. Verifique o pacote Universal RP.");
            return;
        }

        Scene target = SceneManager.GetSceneByPath(ScenePath);
        GameObject map = null;
        try
        {
            if (!target.IsValid() || !target.isLoaded)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    Debug.LogError("Coin Rush: cena existente nao encontrada em " + ScenePath);
                    return;
                }

                // Single closes the current scenes. Let the user save, discard or cancel first.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (!target.IsValid() || !target.isLoaded || target.path != ScenePath)
                throw new InvalidOperationException("Nao foi possivel carregar " + ScenePath);

            // An already open target may be inactive in a multi-scene setup.
            if (SceneManager.GetActiveScene() != target && !SceneManager.SetActiveScene(target))
                throw new InvalidOperationException("Nao foi possivel ativar " + ScenePath);

            // Never touch a Map in another scene, or replace existing user work.
            foreach (GameObject root in target.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "Map") continue;
                    if (RepairMapColliders(target) == null) return;
                    EditorSceneManager.MarkSceneDirty(target);
                    if (!EditorSceneManager.SaveScene(target, ScenePath))
                        throw new InvalidOperationException("Nao foi possivel salvar " + ScenePath);
                    Selection.activeGameObject = child.gameObject;
                    Debug.Log("Coin Rush: colisoes do Map verificadas e salvas, sem recriar objetos.", child);
                    return;
                }
            }

            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "CoinRushMap");
            Material grass = MaterialFor("Grass", new Color(0.30f, 0.60f, 0.32f), shader);
            Material wall = MaterialFor("Walls", new Color(0.25f, 0.39f, 0.44f), shader);
            Material trim = MaterialFor("Trim", new Color(0.75f, 0.83f, 0.69f), shader);
            Material crate = MaterialFor("Crates", new Color(0.75f, 0.47f, 0.24f), shader);
            Material platform = MaterialFor("Platforms", new Color(0.36f, 0.66f, 0.73f), shader);
            Material block = MaterialFor("Blocks", new Color(0.89f, 0.68f, 0.32f), shader);

            map = new GameObject("Map");
            Transform ground = Group("Ground", map.transform);
            Transform boundaries = Group("Boundaries", map.transform);
            Transform obstacles = Group("Obstacles", map.transform);
            Transform lighting = Group("Lighting", map.transform);

            // Top at y=0; interior extends from -15 to +15 on X and Z.
            Cube("Green Floor", ground, new Vector3(0, -0.25f, 0), new Vector3(31, 0.5f, 31), grass);
            Cube("North Wall", boundaries, new Vector3(0, 0.9f, 15.25f), new Vector3(31, 1.8f, 0.5f), wall);
            Cube("South Wall", boundaries, new Vector3(0, 0.9f, -15.25f), new Vector3(31, 1.8f, 0.5f), wall);
            Cube("East Wall", boundaries, new Vector3(15.25f, 0.9f, 0), new Vector3(0.5f, 1.8f, 30), wall);
            Cube("West Wall", boundaries, new Vector3(-15.25f, 0.9f, 0), new Vector3(0.5f, 1.8f, 30), wall);
            Cube("North Cap", boundaries, new Vector3(0, 1.85f, 15.25f), new Vector3(31, 0.1f, 0.6f), trim);
            Cube("South Cap", boundaries, new Vector3(0, 1.85f, -15.25f), new Vector3(31, 0.1f, 0.6f), trim);
            Cube("East Cap", boundaries, new Vector3(15.25f, 1.85f, 0), new Vector3(0.6f, 0.1f, 30), trim);
            Cube("West Cap", boundaries, new Vector3(-15.25f, 1.85f, 0), new Vector3(0.6f, 0.1f, 30), trim);

            // Fixed placement, wide passages and an open center for later gameplay.
            Cube("Crate 01", obstacles, new Vector3(-8, 0.75f, -7), new Vector3(1.5f, 1.5f, 1.5f), crate);
            Cube("Crate 02", obstacles, new Vector3(-6, 0.6f, -7.5f), new Vector3(1.2f, 1.2f, 1.2f), crate);
            Cube("Crate 03", obstacles, new Vector3(8, 0.75f, 7), new Vector3(1.5f, 1.5f, 1.5f), crate);
            Cube("Crate 04", obstacles, new Vector3(10, 0.6f, 7.5f), new Vector3(1.2f, 1.2f, 1.2f), crate);
            Cube("West Platform", obstacles, new Vector3(-9, 0.45f, 5), new Vector3(4, 0.9f, 3), platform);
            Cube("West Step", obstacles, new Vector3(-9, 0.2f, 2.75f), new Vector3(3, 0.4f, 1.5f), trim);
            Cube("East Platform", obstacles, new Vector3(8, 0.45f, -6), new Vector3(4, 0.9f, 3), platform);
            Cube("East Step", obstacles, new Vector3(8, 0.2f, -3.75f), new Vector3(3, 0.4f, 1.5f), trim);
            Cube("North Block", obstacles, new Vector3(-2, 0.65f, 9), new Vector3(4, 1.3f, 1.5f), block);
            Cube("South Block", obstacles, new Vector3(1, 0.65f, -10), new Vector3(3, 1.3f, 1.5f), block);

            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(lighting, false);
            sunObject.transform.localRotation = Quaternion.Euler(50, -30, 0);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, 0.95f, 0.85f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunObject.AddComponent<UniversalAdditionalLightData>();

            // RenderSettings belong to the active scene, which is explicitly the target.
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.64f, 0.76f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.55f, 0.49f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.35f, 0.29f);
            RenderSettings.fog = false;

            EditorSceneManager.MarkSceneDirty(target);
            if (!EditorSceneManager.SaveScene(target, ScenePath))
                throw new InvalidOperationException("Nao foi possivel salvar " + ScenePath);

            Undo.RegisterCreatedObjectUndo(map, "Gerar mapa Coin Rush");
            Selection.activeGameObject = map;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(Vector3.zero, new Vector3(33, 5, 33)), false);

            Debug.Log("Coin Rush: mapa criado e salvo em " + ScenePath + ".", map);
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                Debug.LogWarning("Coin Rush: ative um URP Pipeline Asset em Project Settings > Graphics " +
                    "(e confira Quality) para visualizar os materiais URP corretamente. " +
                    "O gerador preservou as configuracoes globais do projeto.");
        }
        catch (Exception exception)
        {
            // Keep partial work available for inspection; never delete existing objects.
            Debug.LogException(exception);
        }
    }

    [MenuItem("Coin Rush/Gerar Mapa", true)]
    private static bool CanGenerateMap() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Also used by the player generator to repair the existing arena in place.
    public static BoxCollider RepairMapColliders(Scene scene)
    {
        if (scene.path != ScenePath || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform map in root.GetComponentsInChildren<Transform>(true))
        {
            if (map.name != "Map") continue;
            Transform floor = map.Find("Ground/Green Floor");
            if (floor == null)
            {
                Debug.LogError("Coin Rush: Ground/Green Floor nao encontrado. O mapa nao sera recriado.");
                return null;
            }
            RepairBox(floor);
            foreach (string groupName in new[] { "Boundaries", "Obstacles" })
            {
                Transform group = map.Find(groupName);
                if (group == null) continue;
                foreach (MeshFilter mesh in group.GetComponentsInChildren<MeshFilter>(true))
                    RepairBox(mesh.transform);
            }
            Physics.SyncTransforms();
            return floor.GetComponent<BoxCollider>();
        }
        Debug.LogError("Coin Rush: gere o mapa antes de configurar o jogador.");
        return null;
    }

    private static void RepairBox(Transform item)
    {
        BoxCollider box = item.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(item.gameObject);
        Undo.RecordObject(box, "Corrigir colisao do mapa Coin Rush");
        MeshFilter mesh = item.GetComponent<MeshFilter>();
        Bounds localBounds = mesh != null && mesh.sharedMesh != null
            ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        // Collider size is local: the existing transform supplies the floor's 31 x 0.5 x 31 scale.
        box.center = localBounds.center;
        box.size = localBounds.size;
        box.isTrigger = false;
        box.enabled = true;
        box.excludeLayers = 0;
        EditorUtility.SetDirty(box);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Material MaterialFor(string name, Color color, Shader shader)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", 0.18f);
        // Preserve any unrelated asset that happens to occupy the intended path.
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
        return material;
    }

    private static Transform Group(string name, Transform parent)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        BoxCollider box = cube.GetComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = Vector3.one;
        box.enabled = true;
        box.isTrigger = false;
    }
}
