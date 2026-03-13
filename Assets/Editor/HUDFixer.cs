using UnityEditor;
using UnityEngine;
using System.IO;
using TaeWhie.RPG.UI;

public class HUDFixer : EditorWindow
{
    [MenuItem("Tools/HUD/Fix Assets")]
    public static void FixAssets()
    {
        string spriteDir = "Assets/Sprites/UI";
        string dataDir = "Assets/Data/Characters";

        // 1. 스프라이트 설정 변경
        string[] sprites = Directory.GetFiles(spriteDir, "*.png");
        foreach (string path in sprites)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Debug.Log($"Fixed Sprite: {path}");
            }
        }

        // 2. CharacterData 에 초상화 할당
        FixCharacterData(dataDir + "/KnightData.asset", spriteDir + "/Portrait_Knight.png");
        FixCharacterData(dataDir + "/MageData.asset", spriteDir + "/Portrait_Mage.png");
        FixCharacterData(dataDir + "/RogueData.asset", spriteDir + "/Portrait_Rogue.png");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("HUD Fix Completed.");
    }

    private static void FixCharacterData(string dataPath, string spritePath)
    {
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (data != null && sprite != null)
        {
            data.portrait = sprite;
            EditorUtility.SetDirty(data);
            Debug.Log($"Assigned {spritePath} to {dataPath}");
        }
        else
        {
            Debug.LogError($"Failed to find Data: {dataPath} or Sprite: {spritePath}");
        }
    }
}
