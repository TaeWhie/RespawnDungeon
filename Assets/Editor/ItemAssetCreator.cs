using System.IO;
using UnityEditor;
using UnityEngine;
using TaeWhie.RPG.Inventory;

public class ItemAssetCreator : EditorWindow
{
    [MenuItem("Tools/Inventory/Create Sample Items")]
    public static void CreateItems()
    {
        string spriteDir = "Assets/Sprites/Inventory";
        string dataDir = "Assets/Data/Items";

        if (!AssetDatabase.IsValidFolder(dataDir))
            AssetDatabase.CreateFolder("Assets/Data", "Items");

        // 0. 스프라이트 설정 먼저 수정
        string[] sprites = Directory.GetFiles(spriteDir, "*.png");
        foreach (string path in sprites)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        CreateItem(dataDir + "/Potion.asset", "Health Potion", spriteDir + "/Item_Potion.png", 1, 1);
        CreateItem(dataDir + "/Sword.asset", "Steel Sword", spriteDir + "/Item_Sword.png", 1, 3);
        CreateItem(dataDir + "/Shield.asset", "Wooden Shield", spriteDir + "/Item_Shield.png", 2, 2);
        CreateItem(dataDir + "/Armor.asset", "Plate Armor", spriteDir + "/Item_Armor.png", 2, 2);
        CreateItem(dataDir + "/Ring.asset", "Magic Ring", spriteDir + "/Item_Ring.png", 1, 1);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Sample items created successfully.");
    }

    private static void CreateItem(string path, string name, string iconPath, int w, int h)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.itemName = name;
        
        // 이름에 따라 타입 자동 지정 (한글/영문 지원)
        if (name.Contains("Sword") || name.Contains("무기") || name.Contains("검")) 
        { 
            item.itemType = ItemData.ItemType.Weapon;
            item.width = 1; item.height = 3;
        }
        else if (name.Contains("Shield") || name.Contains("방패")) 
        { 
            item.itemType = ItemData.ItemType.Shield;
            item.width = 2; item.height = 2;
        }
        else if (name.Contains("Armor") || name.Contains("갑옷") || name.Contains("몸통")) 
        { 
            item.itemType = ItemData.ItemType.Armor;
            item.width = 2; item.height = 2;
        }
        else if (name.Contains("Helmet") || name.Contains("헬멧") || name.Contains("머리")) 
        { 
            item.itemType = ItemData.ItemType.Helmet;
            item.width = 1; item.height = 1;
        }
        else if (name.Contains("Glove") || name.Contains("팔") || name.Contains("장갑")) 
        { 
            item.itemType = ItemData.ItemType.Gloves;
            item.width = 1; item.height = 2;
        }
        else if (name.Contains("Boot") || name.Contains("다리") || name.Contains("신발")) 
        { 
            item.itemType = ItemData.ItemType.Boots;
            item.width = 1; item.height = 2;
        }
        else if (name.Contains("Ring") || name.Contains("반지") || name.Contains("장신구") || name.Contains("치장")) 
        { 
            item.itemType = ItemData.ItemType.Accessory;
            item.width = 1; item.height = 1;
        }
        else 
        {
            item.itemType = ItemData.ItemType.Etc;
            item.width = w;
            item.height = h;
        }

        // 기본 스탯 예시 설정
        switch (item.itemType)
        {
            case ItemData.ItemType.Weapon: item.atk = 15; item.str = 2; break;
            case ItemData.ItemType.Shield: item.def = 8; item.str = 1; break;
            case ItemData.ItemType.Armor: item.def = 12; item.dex = -1; break;
            case ItemData.ItemType.Helmet: item.def = 5; item.@int = 1; break;
            case ItemData.ItemType.Gloves: item.atk = 2; item.dex = 1; break;
            case ItemData.ItemType.Boots: item.def = 3; item.dex = 2; break;
            case ItemData.ItemType.Accessory: item.@int = 5; item.dex = 3; break;
        }
        
        item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        
        EditorUtility.SetDirty(item);
    }
}
