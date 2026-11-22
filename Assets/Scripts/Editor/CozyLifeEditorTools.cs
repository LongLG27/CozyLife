#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CozyLife.EditorTools
{
    /// <summary>
    /// Tự động thiết lập chuẩn Sprite Isometric cho mọi hình ảnh trong Resources/CozyLifeArt
    /// ngay khi Unity Editor import file hoặc khi người dùng thêm ảnh mới vào.
    /// </summary>
    public class CozyLifeAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/CozyLifeArt"))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
        }
    }

    public static class CozyLifeEditorMenu
    {
        [InitializeOnLoadMethod]
        private static void EnsureArtImportedOnce()
        {
            if (SessionState.GetBool("CozyLife_ArtChecked", false))
                return;

            SessionState.SetBool("CozyLife_ArtChecked", true);
            EditorApplication.delayCall += () =>
            {
                TextureImporter sample = AssetImporter.GetAtPath("Assets/Resources/CozyLifeArt/Tiles/iso_grass.png") as TextureImporter;
                if (sample != null && (!sample.isReadable || sample.textureType != TextureImporterType.Sprite))
                {
                    ReimportCozyLifeArt();
                }
            };
        }

        [MenuItem("CozyLife/Đồng bộ lại hình ảnh trong Resources/CozyLifeArt")]
        public static void ReimportCozyLifeArt()
        {
            AssetDatabase.ImportAsset("Assets/Resources/CozyLifeArt", ImportAssetOptions.ImportRecursive);
            Debug.Log("[CozyLife] Đã đồng bộ toàn bộ hình ảnh Sprite trong Assets/Resources/CozyLifeArt!");
        }
    }
}
#endif
