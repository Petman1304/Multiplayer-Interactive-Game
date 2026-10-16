using UnityEditor;
using UnityEngine;

/// <summary>Settings apply only to the integration's original generated audio.</summary>
public sealed class GetawayAudioImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/GetawayAudio/";
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;
        var importer = (AudioImporter)assetImporter;
        Configure(importer, assetPath.EndsWith("/downtown.wav", System.StringComparison.Ordinal));
    }
    public static void Configure(AudioImporter importer, bool music)
    {
        importer.forceToMono = true;
        importer.loadInBackground = music;
        var settings = importer.defaultSampleSettings;
        settings.preloadAudioData = !music;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.quality = .65f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
    }
}
