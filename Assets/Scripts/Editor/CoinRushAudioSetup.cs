using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoinRushAudioSetup
{
    private const string MusicPath = "Assets/Audio/CoinRush_MusicaFundo.wav";
    private const string CoinPath = "Assets/Audio/CoinRush_ColetaMoeda.wav";
    private const string VictoryPath = "Assets/Audio/CoinRush_Vitoria.wav";

    [MenuItem("Coin Rush/Configurar Áudio")]
    public static void ConfigureAudioOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        int undoGroup = -1;
        try
        {
            string path = CoinRushCompleteGameGenerator.ResolveScenePath();
            Scene scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            if (!CoinRushHUDGenerator.FindSingle(scene, out CoinRushGameManager game) || game == null)
                throw new InvalidOperationException("GameManager existente nao encontrado em " + path);
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configurar audio Coin Rush");
            Configure(game);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Nao foi possivel salvar " + path);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = game.gameObject;
            Debug.Log("[CoinRush] Audio configurado e salvo, sem regenerar mapa ou modificar os controles.", game);
        }
        catch (Exception error)
        {
            Debug.LogError("[CoinRush] Falha ao configurar audio: " + error.Message);
            Debug.LogException(error);
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
        }
    }

    public static CoinRushAudioManager Configure(CoinRushGameManager game)
    {
        AudioClip background = RequireClip(MusicPath);
        AudioClip coin = RequireClip(CoinPath);
        AudioClip victory = RequireClip(VictoryPath);
        Scene scene = game.gameObject.scene;
        if (!CoinRushHUDGenerator.FindSingle(scene, out CoinRushAudioManager audio))
            throw new InvalidOperationException("Ha gerenciadores de audio duplicados na cena alvo.");
        if (audio == null) audio = Undo.AddComponent<CoinRushAudioManager>(game.gameObject);
        AudioSource music = audio.MusicSource;
        AudioSource effects = audio.EffectsSource;
        AudioSource[] sources = game.GetComponents<AudioSource>();
        if (music == null) music = sources.FirstOrDefault(source => source != effects);
        if (music == null) music = Undo.AddComponent<AudioSource>(game.gameObject);
        if (effects == null || effects == music) effects = sources.FirstOrDefault(source => source != music);
        if (effects == null) effects = Undo.AddComponent<AudioSource>(game.gameObject);
        Undo.RecordObject(audio, "Configurar clips de audio");
        Undo.RecordObject(music, "Configurar musica em loop");
        Undo.RecordObject(effects, "Configurar efeitos one-shot");
        Undo.RecordObject(game, "Integrar audio ao GameManager");
        music.enabled = true;
        effects.enabled = true;
        audio.enabled = true;
        audio.Configure(game, music, effects, background, coin, victory);
        game.ConfigureAudio(audio);
        ConfigureFootsteps(game);
        // Silence only duplicate sources of this exact music in this scene.
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
        {
            if (source == music || source.clip != background) continue;
            Undo.RecordObject(source, "Desativar musica duplicada");
            source.Stop();
            source.playOnAwake = false;
            source.enabled = false;
        }
        EditorUtility.SetDirty(audio);
        EditorUtility.SetDirty(music);
        EditorUtility.SetDirty(effects);
        EditorUtility.SetDirty(game);
        Debug.Log("[CoinRush] Clips vinculados: " + MusicPath + ", " + CoinPath + ", " + VictoryPath, audio);
        return audio;
    }

    private static void ConfigureFootsteps(CoinRushGameManager game)
    {
        const string path = "Assets/Audio/MrKrabs_Footsteps.wav";
        AudioClip original = RequireClip(path);
        if (!CoinRushHUDGenerator.FindSingle(game.gameObject.scene, out CoinRushPlayerController player) || player == null)
            throw new InvalidOperationException("Jogador FPS nao encontrado para configurar os passos.");
        AudioClip loop = CreateFootstepLoop(original);
        AudioSource source = player.FootstepSource;
        if (source == null)
        {
            Transform child = player.transform.Find("Footsteps Audio");
            if (child == null)
            {
                GameObject root = new GameObject("Footsteps Audio");
                root.transform.SetParent(player.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "Criar AudioSource de passos");
                child = root.transform;
            }
            source = child.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(child.gameObject);
        }
        Undo.RecordObject(source, "Configurar loop de passos");
        Undo.RecordObject(player, "Integrar passos ao controlador FPS");
        source.enabled = true;
        source.clip = loop;
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0.4f;
        source.pitch = 1f;
        source.mute = false;
        source.spatialBlend = 0;
        source.ignoreListenerPause = false;
        player.ConfigureFootsteps(source, game);
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(player);
        Debug.Log("[CoinRush] Passos configurados: loop derivado de " + path + "; volume 40%; corrida em 1.35x.", player);
    }

    private static AudioClip CreateFootstepLoop(AudioClip original)
    {
        const string folder = "Assets/CoinRushGenerated/Audio";
        const string output = folder + "/MrKrabs_Footsteps_Loop.wav";
        if (original.loadState != AudioDataLoadState.Loaded) original.LoadAudioData();
        float[] data = new float[original.samples * original.channels];
        if (!original.GetData(data, 0))
            throw new InvalidOperationException("Nao foi possivel ler o WAV de passos para preparar o loop; arquivo original preservado.");
        int first = -1, last = -1;
        for (int frame = 0; frame < original.samples; frame++)
        {
            bool audible = false;
            for (int channel = 0; channel < original.channels; channel++)
                if (Mathf.Abs(data[frame * original.channels + channel]) > 0.002f) { audible = true; break; }
            if (!audible) continue;
            if (first < 0) first = frame;
            last = frame;
        }
        if (first < 0) throw new InvalidOperationException("O arquivo de passos contem apenas silencio.");
        int padding = Mathf.RoundToInt(original.frequency * 0.015f);
        first = Mathf.Max(0, first - padding);
        last = Mathf.Min(original.samples - 1, last + padding);
        int frames = last - first + 1;
        int fade = Mathf.Min(frames / 2, Mathf.RoundToInt(original.frequency * 0.005f));
        if (!AssetDatabase.IsValidFolder("Assets/CoinRushGenerated")) AssetDatabase.CreateFolder("Assets", "CoinRushGenerated");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/CoinRushGenerated", "Audio");
        // Write only a generated copy. The imported WAV and its importer are never modified.
        using (var writer = new BinaryWriter(File.Create(output)))
        {
            int bytes = frames * original.channels * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)original.channels);
            writer.Write(original.frequency);
            writer.Write(original.frequency * original.channels * 2);
            writer.Write((short)(original.channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(bytes);
            for (int frame = 0; frame < frames; frame++)
            {
                float gain = fade > 0 ? Mathf.Min(1f, Mathf.Min((float)frame / fade, (float)(frames - 1 - frame) / fade)) : 1f;
                for (int channel = 0; channel < original.channels; channel++)
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(data[(first + frame) * original.channels + channel] * gain, -1, 1) * 32767));
            }
        }
        AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(output);
        if (clip == null) throw new InvalidOperationException("Falha ao importar o loop gerado: " + output);
        Debug.Log($"[CoinRush] Loop de passos preparado: {original.length:0.00}s -> {clip.length:0.00}s, bordas suavizadas em 5ms.");
        return clip;
    }

    private static AudioClip RequireClip(string path)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new InvalidOperationException("AudioClip nao importado ou ausente: " + path);
        return clip;
    }

    [MenuItem("Coin Rush/Configurar Áudio", true)]
    private static bool CanConfigure() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
