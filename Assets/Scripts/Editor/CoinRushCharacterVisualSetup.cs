using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CoinRushCharacterVisualSetup
{
    private const string AssetFolder = "Assets/Models/MrKrabs";
    private const string VisualName = "CoinRush MrKrabs Visual";
    private const string ClawsName = "CoinRush MrKrabs Claws";
    private const string MaterialsFolder = "Assets/CoinRushGenerated/MrKrabsMaterials";
    // Material/texture associations verified inside the imported FBX.
    private static readonly Dictionary<string, string> Textures = new Dictionary<string, string>
    {
        { "02 - Default", "Mr. Krabs Shirt1.fw.png" }, { "01 - Default", "Mr. Krabs Skin.fw.png" },
        { "07 - Default", "Mr. Krabs Belt1.fw.png" }, { "21 - Default", "Mr. Krabs Belt2.fw.png" },
        { "03 - Default", "Mr. Krabs Pants.fw.png" }, { "20 - Default", "Mr. Krabs Pants2.png" },
        { "19 - Default", "Mr. Krabs Shirt2.fw.png" }, { "15 - Default", "Mr. Krabs Mouth.png" },
        { "13 - Default", "Mr. Krabs Eyes2.fw.png" }, { "09 - Default", "Mr. Krabs Eyes.fw.png" },
        { "08 - Default", "Mr. Krabs Eye Brows.fw.png" }
    };

    [MenuItem("Coin Rush/Atualizar Visual do Jogador")]
    public static void UpdateVisualOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = CoinRushCompleteGameGenerator.ResolveScenePath();
        Scene scene = SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        if (!CoinRushHUDGenerator.FindSingle(scene, out CoinRushPlayerController player)
            || !CoinRushHUDGenerator.FindSingle(scene, out CoinRushCameraFollow view) || player == null || view == null)
        { Debug.LogError("[CoinRush] Jogador FPS/camera existente nao encontrado na cena " + path); return; }
        if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
        { Debug.LogError("[CoinRush] Nao foi possivel ativar " + path); return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        try
        {
            Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Substituir visual por MrKrabs");
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Configurar garras FPS");
            if (!Apply(player, view)) return;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Nao foi possivel salvar " + path);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = player.gameObject;
            Debug.Log("[CoinRush] Visual MrKrabs configurado e salvo. Mapa e sistemas de jogo preservados.", player);
        }
        catch (Exception error)
        {
            Debug.LogError("[CoinRush] Falha ao configurar visual MrKrabs: " + error.Message);
            Debug.LogException(error);
            Undo.RevertAllDownToGroup(group);
        }
    }

    public static bool Apply(CoinRushPlayerController player, CoinRushCameraFollow view)
    {
        string[] models = AssetDatabase.FindAssets("t:Model", new[] { AssetFolder })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetExtension(p).Equals(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (models.Length != 1)
        {
            Debug.LogError("[CoinRush] Esperado um FBX em " + AssetFolder + "; encontrados: " + string.Join(", ", models) + ". Visual anterior preservado.");
            return false;
        }
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(models[0]);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (asset == null || shader == null) { Debug.LogError("[CoinRush] Modelo MrKrabs ou shader URP/Lit indisponivel."); return false; }
        if (!AssetDatabase.IsValidFolder("Assets/CoinRushGenerated")) AssetDatabase.CreateFolder("Assets", "CoinRushGenerated");
        if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/CoinRushGenerated", "MrKrabsMaterials");

        // Construct first; replace the old owned hierarchy only after successful fitting.
        GameObject wrapper = new GameObject(VisualName + " (building)");
        wrapper.transform.SetParent(player.transform, false);
        Undo.RegisterCreatedObjectUndo(wrapper, "Criar visual MrKrabs");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, wrapper.transform);
        StripPhysicsAndAnimation(model);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(material => ConvertMaterial(material, shader)).ToArray();

        Transform rightFoot = Find(model.transform, "Right_foot");
        Transform leftFoot = Find(model.transform, "Left_foot");
        Transform body = Find(model.transform, "Body");
        if (rightFoot != null && leftFoot != null && body != null)
        {
            Vector3 up = wrapper.transform.InverseTransformVector(body.position - (rightFoot.position + leftFoot.position) * 0.5f);
            Vector3 axis = Mathf.Abs(up.y) >= Mathf.Abs(up.x) && Mathf.Abs(up.y) >= Mathf.Abs(up.z)
                ? Vector3.up * Mathf.Sign(up.y) : Mathf.Abs(up.z) >= Mathf.Abs(up.x)
                ? Vector3.forward * Mathf.Sign(up.z) : Vector3.right * Mathf.Sign(up.x);
            model.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.up) * model.transform.localRotation;
        }
        Bounds bounds = BoundsIn(wrapper.transform);
        if (bounds.size.y < 0.0001f) throw new InvalidOperationException("Modelo sem geometria visivel: " + models[0]);
        // Correct the face direction using the actual eye stalk objects in this FBX.
        Transform eye = Find(model.transform, "Cylinder002");
        Transform otherEye = Find(model.transform, "Object002");
        if (eye != null && otherEye != null && body != null)
        {
            Vector3 forward = wrapper.transform.InverseTransformVector((eye.position + otherEye.position) * 0.5f - body.position);
            forward.y = 0;
            if (forward.sqrMagnitude > 0.0001f)
                model.transform.localRotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward) * model.transform.localRotation;
        }
        bounds = BoundsIn(wrapper.transform);
        float scale = 1.95f / bounds.size.y;
        model.transform.localScale *= scale;
        bounds = BoundsIn(wrapper.transform);
        CharacterController controller = player.GetComponent<CharacterController>();
        float baseY = controller.center.y - controller.height * 0.5f;
        model.transform.localPosition += new Vector3(-bounds.center.x, baseY - bounds.min.y, -bounds.center.z);

        Transform old = player.transform.Find(VisualName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        wrapper.name = VisualName;
        Renderer capsule = player.GetComponent<Renderer>();
        if (capsule != null) { Undo.RecordObject(capsule, "Ocultar capsula original"); capsule.enabled = false; }
        Undo.RecordObject(view, "Ocultar corpo MrKrabs na camera FPS");
        view.SetCharacterVisual(wrapper.transform);
        CreateClaws(model.transform, player.transform, view.transform);
        EditorUtility.SetDirty(view);
        Debug.Log("[CoinRush] Modelo utilizado: " + models[0] + ". Pose estatica; altura 1.95m; pes alinhados a base do CharacterController.");
        return true;
    }

    private static void CreateClaws(Transform model, Transform player, Transform camera)
    {
        Transform previous = camera.Find(ClawsName);
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        Transform root = new GameObject(ClawsName).transform;
        root.SetParent(camera, false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Criar garras FPS do modelo");
        int count = 0;
        foreach (string name in new[] { "Left_hand", "Right_hand" })
        {
            Transform hand = Find(model, name);
            if (hand == null) continue;
            GameObject copy = Object.Instantiate(hand.gameObject, root, false);
            copy.name = name + " FPS";
            StripPhysicsAndAnimation(copy);
            copy.transform.localRotation = Quaternion.Inverse(player.rotation) * hand.rotation;
            copy.transform.localPosition = Vector3.zero;
            Bounds bounds = BoundsIn(copy.transform, root);
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size < 0.0001f) { Object.DestroyImmediate(copy); continue; }
            copy.transform.localScale *= 0.38f / size;
            bounds = BoundsIn(copy.transform, root);
            copy.transform.localPosition += new Vector3(name == "Left_hand" ? -0.28f : 0.28f, -0.27f, 0.65f) - bounds.center;
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
            count++;
        }
        if (count != 2) Debug.LogWarning("[CoinRush] Apenas " + count + " garras foram configuradas; nenhuma geometria foi inventada.");
    }

    private static Transform Find(Transform root, string name)
        => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static void StripPhysicsAndAnimation(GameObject model)
    {
        foreach (Collider item in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(item);
        foreach (Rigidbody item in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(item);
        foreach (Animator item in model.GetComponentsInChildren<Animator>(true)) item.enabled = false;
        foreach (Animation item in model.GetComponentsInChildren<Animation>(true)) item.enabled = false;
        foreach (Camera item in model.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(item);
        foreach (Light item in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(item);
    }

    private static Bounds BoundsIn(Transform hierarchy, Transform relativeTo = null)
    {
        if (relativeTo == null) relativeTo = hierarchy;
        Bounds result = new Bounds();
        bool first = true;
        foreach (Renderer renderer in hierarchy.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.localBounds;
            Matrix4x4 matrix = relativeTo.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = matrix.MultiplyPoint3x4(corner);
                if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }

    private static Material ConvertMaterial(Material source, Shader shader)
    {
        string name = source != null ? source.name.Replace(" (Instance)", "") : "Default";
        string path = MaterialsFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader) { name = "MrKrabs " + name };
        Texture texture = source != null ? source.mainTexture : null;
        if (texture == null && Textures.TryGetValue(name, out string filename))
        {
            string texturePath = AssetDatabase.FindAssets("t:Texture", new[] { AssetFolder })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileName(p).Equals(filename, StringComparison.OrdinalIgnoreCase));
            if (texturePath != null) texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.2f);
        if (texture != null) material.SetTexture("_BaseMap", texture);
        else Debug.LogWarning("[CoinRush] Textura nao encontrada para material MrKrabs: " + name);
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
        return material;
    }

    [MenuItem("Coin Rush/Atualizar Visual do Jogador", true)]
    private static bool CanUpdate() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
