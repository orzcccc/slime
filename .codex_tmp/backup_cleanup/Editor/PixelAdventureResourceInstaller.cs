#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlimeBlindBox.Editor
{
    [InitializeOnLoad]
    internal static class PixelAdventureResourceInstaller
    {
        private const string Source = "Assets/Pixel Adventure 2/Assets/Enemies/Slime/Idle-Run (44x30).png";
        private const string DestinationFolder = "Assets/Resources/PixelAdventure";
        private const string Destination = DestinationFolder + "/SlimeIdle.png";
        private const string ParticleSource = "Assets/Pixel Adventure 2/Assets/Enemies/Slime/Particles (16x16).png";
        private const string ParticleDestination = DestinationFolder + "/SlimeParticles.png";
        private const string StarSource = "Assets/Pixel Adventure 2/Assets/Enemies/Skull/Orange Particle.png";
        private const string StarDestination = DestinationFolder + "/OrangeParticle.png";

        static PixelAdventureResourceInstaller()
        {
            EditorApplication.delayCall += EnsureResources;
        }

        private static void EnsureResources()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(Source) == null)
            {
                Debug.LogWarning("未找到 Pixel Adventure 2 史莱姆素材，像素主题将使用纯色占位。");
                return;
            }

            Directory.CreateDirectory(DestinationFolder);
            CopyIfMissing(Source, Destination);
            CopyIfMissing(ParticleSource, ParticleDestination);
            CopyIfMissing(StarSource, StarDestination);
        }

        private static void CopyIfMissing(string source, string destination)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(destination) != null) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(source) == null) return;
            if (AssetDatabase.CopyAsset(source, destination))
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
