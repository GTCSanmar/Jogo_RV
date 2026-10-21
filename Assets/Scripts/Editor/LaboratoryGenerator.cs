using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace EscapeDoLaboratorio.Editor
{
    public static class LaboratoryGenerator
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string Generated = "Assets/GeneratedLaboratory";
        static Material white, gray, blue, dark, glow;
        static Transform root;

        [MenuItem("Escape do Laboratório/Gerar laboratório jogável")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Saia do Play Mode antes de gerar o laboratório."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GenerateScene();
        }

        // Entry point also usable through Unity -executeMethod.
        public static void GenerateBatch() { GenerateScene(); }

        static void GenerateScene()
        {
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
                throw new InvalidOperationException("Instale e configure o Universal Render Pipeline antes de gerar.");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(Generated + "/Materials");
            Directory.CreateDirectory(Generated + "/Prefabs");
            AssetDatabase.Refresh();
            if (File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes/Backups");
                AssetDatabase.Refresh();
                string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Backups/Game_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
                if (!AssetDatabase.CopyAsset(ScenePath, backup)) throw new IOException("Não foi possível salvar a cópia de segurança da cena.");
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("Laboratório gerado").transform;
            white = Material("Branco", new Color(.79f, .84f, .88f));
            gray = Material("Cinza", new Color(.30f, .36f, .42f));
            blue = Material("Azul", new Color(.035f, .27f, .52f));
            dark = Material("Grafite", new Color(.035f, .06f, .09f));
            glow = Material("Ciano", new Color(.15f, .85f, 1));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(.1f, .6f, 1) * 2);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .53f, .61f);
            RenderSettings.skybox = null;

            // Four 10 x 10 rooms, separated by six metre corridors in a loop.
            Room(new Vector3(0, 0, 0), "01 / RECEPÇÃO", true, true, true, false);
            Room(new Vector3(16, 0, 0), "02 / ANÁLISES", true, false, false, true);
            Room(new Vector3(0, 0, 16), "03 / PESQUISA", false, true, true, false);
            Room(new Vector3(16, 0, 16), "04 / CONTENÇÃO", false, false, true, true);
            Corridor(new Vector3(8, 0, 0), false);
            Corridor(new Vector3(8, 0, 16), false);
            Corridor(new Vector3(0, 0, 8), true);
            Corridor(new Vector3(16, 0, 8), true);
            // An enclosed vestibule beyond the locked south entrance.
            Box("Piso saída", new Vector3(0, -.15f, -7), new Vector3(3, .3f, 4), gray);
            Box("Teto saída", new Vector3(0, 3.65f, -7), new Vector3(3, .3f, 4), white);
            Box("Parede saída", new Vector3(-1.65f, 1.75f, -7), new Vector3(.3f, 3.5f, 4), white);
            Box("Parede saída", new Vector3(1.65f, 1.75f, -7), new Vector3(.3f, 3.5f, 4), white);
            Box("Fundo saída", new Vector3(0, 1.75f, -9), new Vector3(3.6f, 3.5f, .3f), blue);
            Label("SAÍDA / ACESSO RESTRITO", new Vector3(0, 3.12f, -4.78f));

            var table = new GameObject("Bancada");
            table.transform.SetParent(root);
            Box("Tampo", new Vector3(0, 1, 0), new Vector3(2.6f, .16f, 1.2f), white, table.transform);
            foreach (float x in new[] { -1.05f, 1.05f })
                Box("Base", new Vector3(x, .46f, 0), new Vector3(.18f, .92f, .85f), gray, table.transform);
            var tablePrefab = SavePrefab(table, "Bancada");
            UnityEngine.Object.DestroyImmediate(table);
            foreach (var center in new[] { Vector3.zero, new Vector3(16, 0, 0), new Vector3(0, 0, 16), new Vector3(16, 0, 16) })
            {
                for (int i = 0; i < 2; i++)
                {
                    var pos = center + new Vector3(i == 0 ? -3 : 3, 0, 2.7f);
                    var bench = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab, root);
                    bench.transform.position = pos;
                    Box("Computador", pos + new Vector3(0, 1.48f, .22f), new Vector3(.9f, .65f, .12f), dark);
                    Box("Tela azul", pos + new Vector3(0, 1.48f, .145f), new Vector3(.78f, .53f, .02f), glow);
                    Box("Suporte monitor", pos + new Vector3(0, 1.16f, .22f), new Vector3(.16f, .3f, .18f), gray);
                    Box("Teclado", pos + new Vector3(0, 1.11f, -.3f), new Vector3(.75f, .06f, .25f), dark);
                    var flask = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    flask.name = "Amostra científica";
                    flask.transform.SetParent(root);
                    flask.transform.position = pos + new Vector3(.95f, 1.3f, 0);
                    flask.transform.localScale = new Vector3(.23f, .22f, .23f);
                    flask.GetComponent<Renderer>().sharedMaterial = blue;
                }
                Box("Equipamento de análise", center + new Vector3(-3.6f, 1, -2.8f), new Vector3(1.3f, 2, 1.4f), gray);
                Box("Painel do equipamento", center + new Vector3(-3.6f, 1.45f, -3.52f), new Vector3(.8f, .45f, .04f), glow);
            }
            var card = Box("Cartão de acesso", Vector3.zero, new Vector3(.38f, .035f, .24f), glow);
            card.AddComponent<AccessCard>();
            var cardPrefab = SavePrefab(card, "CartaoDeAcesso");
            UnityEngine.Object.DestroyImmediate(card);
            card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, root);
            card.transform.position = new Vector3(19.85f, 1.11f, 18.45f);

            var doorObject = Box("Porta de saída", Vector3.zero, new Vector3(2.8f, 2.8f, .22f), blue);
            doorObject.AddComponent<ExitDoor>();
            var doorPrefab = SavePrefab(doorObject, "PortaDeSaida");
            UnityEngine.Object.DestroyImmediate(doorObject);
            doorObject = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, root);
            doorObject.transform.position = new Vector3(0, 1.4f, -5);
            var door = doorObject.GetComponent<ExitDoor>();
            var zone = new GameObject("Zona de fuga");
            zone.transform.SetParent(root);
            zone.transform.position = new Vector3(0, 1.4f, -7.2f);
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.size = new Vector3(2.8f, 2.8f, 1);
            trigger.isTrigger = true;
            var body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            zone.AddComponent<EscapeZone>().door = door;

            var game = new GameObject("Estado e interface").AddComponent<LabGame>();
            var player = new GameObject("Jogador");
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .3f; cc.center = new Vector3(0, .9f, 0);
            cc.stepOffset = .25f;
            var cameraObject = new GameObject("Câmera primeira pessoa", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = new Vector3(0, 1.65f, 0);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .05f; camera.farClipPlane = 90; camera.fieldOfView = 75;
            var movement = player.AddComponent<LabPlayer>();
            movement.view = camera;
            var playerPrefab = SavePrefab(player, "Jogador");
            UnityEngine.Object.DestroyImmediate(player);
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0, .05f, -2.5f);
            player.GetComponent<LabPlayer>().game = game;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<LabPlayer>());

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Falha ao salvar Game.unity.");
            var builds = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            builds.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Laboratório gerado em Assets/Scenes/Game.unity. Pressione Play para jogar.");
        }

        static Material Material(string name, Color color)
        {
            string path = Generated + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .35f);
            EditorUtility.SetDirty(material);
            return material;
        }
        static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent = null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent != null ? parent : root);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }
        static GameObject SavePrefab(GameObject obj, string name) => PrefabUtility.SaveAsPrefabAsset(obj, Generated + "/Prefabs/" + name + ".prefab");

        static void Room(Vector3 center, string title, bool north, bool east, bool south, bool west)
        {
            Box(title + " / piso", center + Vector3.down * .15f, new Vector3(10.3f, .3f, 10.3f), gray);
            Box(title + " / teto", center + Vector3.up * 3.65f, new Vector3(10.3f, .3f, 10.3f), white);
            Wall(center + new Vector3(0, 0, 5), false, north);
            Wall(center + new Vector3(0, 0, -5), false, south);
            Wall(center + new Vector3(5, 0, 0), true, east);
            Wall(center + new Vector3(-5, 0, 0), true, west);
            Label(title, center + new Vector3(0, 3.15f, 4.78f));
            LightAt(center);
        }
        static void Wall(Vector3 center, bool alongZ, bool opening)
        {
            Vector3 Size(float width, float height) => alongZ ? new Vector3(.3f, height, width) : new Vector3(width, height, .3f);
            Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
            if (!opening) Box("Parede", center + Vector3.up * 1.75f, Size(10, 3.5f), white);
            else
            {
                foreach (int side in new[] { -1, 1 })
                    Box("Parede lateral do vão", center + axis * (3.25f * side) + Vector3.up * 1.75f, Size(3.5f, 3.5f), white);
                Box("Verga azul", center + Vector3.up * 3.2f, Size(3, .6f), blue);
                foreach (int side in new[] { -1, 1 })
                    Box("Batente azul", center + axis * (1.48f * side) + Vector3.up * 1.4f, Size(.08f, 2.8f), blue);
            }
        }
        static void Corridor(Vector3 center, bool alongZ)
        {
            Box("Corredor / piso", center + Vector3.down * .15f, alongZ ? new Vector3(3, .3f, 6) : new Vector3(6, .3f, 3), gray);
            Box("Corredor / teto", center + Vector3.up * 3.65f, alongZ ? new Vector3(3.3f, .3f, 6) : new Vector3(6, .3f, 3.3f), white);
            foreach (int side in new[] { -1, 1 })
                Box("Parede corredor", center + (alongZ ? Vector3.right : Vector3.forward) * (1.65f * side) + Vector3.up * 1.75f,
                    alongZ ? new Vector3(.3f, 3.5f, 6) : new Vector3(6, 3.5f, .3f), white);
            LightAt(center);
        }
        static void LightAt(Vector3 position)
        {
            Box("Luminária", position + Vector3.up * 3.46f, new Vector3(1.8f, .06f, .6f), glow);
            var obj = new GameObject("Iluminação LED", typeof(Light));
            obj.transform.SetParent(root);
            obj.transform.position = position + Vector3.up * 3.15f;
            var light = obj.GetComponent<Light>();
            light.type = LightType.Point; light.range = 9; light.intensity = 3;
            light.color = new Color(.77f, .88f, 1); light.shadows = LightShadows.None;
        }
        static void Label(string value, Vector3 position)
        {
            var obj = new GameObject(value, typeof(TextMesh));
            obj.transform.SetParent(root);
            obj.transform.position = position;
            var text = obj.GetComponent<TextMesh>();
            text.text = value; text.anchor = TextAnchor.MiddleCenter;
            text.characterSize = .09f; text.fontSize = 48; text.color = new Color(.05f, .35f, .65f);
        }
    }
}
