using UnityEngine;

namespace TaeWhie.RPG.Inventory
{
    /// <summary>
    /// 아이템의 원형 데이터를 정의하는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "RPG/Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        public enum ItemType
        {
            Etc,        // 기타 (포션 등)
            Helmet,     // 투구
            Armor,      // 갑옷
            Weapon,     // 무기
            Shield,     // 방패
            Gloves,     // 팔/장갑
            Boots,      // 다리/신발
            Accessory   // 장신구
        }

        [Header("기본 정보")]
        public string itemName;            // 아이템 이름
        public ItemType itemType;          // 아이템 타입
        public Sprite icon;                // 아이템 아이콘

        [Header("그리드 크기")]
        public int width = 1;              // 가로 차지 칸 수
        public int height = 1;             // 세로 차지 칸 수

        [Header("기타 정보")]
        [TextArea] public string description; // 아이템 설명

        [Header("아이템 스탯")]
        public int atk;
        public int def;
        public int str;
        public int dex;
        public int @int; // int is a keyword
    }
}
