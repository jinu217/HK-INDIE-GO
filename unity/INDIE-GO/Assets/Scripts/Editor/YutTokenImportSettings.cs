using UnityEditor;

// 윷 결과 토큰 그림(Sprites/인게임창/윷토큰)을 넣으면 자동으로 UI용 Sprite로 가져오게 함
// (기본 설정이면 296px 그림이 256px로 줄어들어 흐려지고, Image에 바로 못 씀)
public class YutTokenImportSettings : AssetPostprocessor
{
    public const string TokenFolder = "Assets/Sprites/인게임창/윷토큰";

    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(TokenFolder + "/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
