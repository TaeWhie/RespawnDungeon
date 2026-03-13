using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaeWhie.RPG.Inventory
{
    /// <summary>
    /// 캐릭터의 장착 정보를 관리하는 클래스입니다.
    /// </summary>
    [Serializable]
    public class CharacterEquipment
    {
        // 단일 슬롯 부위
        public InventoryItem helmet;
        public InventoryItem armor;
        public InventoryItem weapon;
        public InventoryItem shield;

        // 다중 슬롯 부위 (각 2개씩)
        public InventoryItem[] gloves = new InventoryItem[2];
        public InventoryItem[] boots = new InventoryItem[2];
        public InventoryItem[] accessories = new InventoryItem[2];

        public event Action OnEquipmentChanged;

        /// <summary>
        /// 특정 타입의 슬롯에 아이템을 장착합니다.
        /// </summary>
        public InventoryItem Equip(InventoryItem item, int index = 0)
        {
            InventoryItem oldItem = null;

            switch (item.data.itemType)
            {
                case ItemData.ItemType.Helmet: oldItem = helmet; helmet = item; break;
                case ItemData.ItemType.Armor: oldItem = armor; armor = item; break;
                case ItemData.ItemType.Weapon: oldItem = weapon; weapon = item; break;
                case ItemData.ItemType.Shield: oldItem = shield; shield = item; break;
                case ItemData.ItemType.Gloves: 
                    // 스마트 장착: 인덱스가 0이고 0번 자리에 이미 있다면 1번 확인
                    if (index == 0 && gloves[0] != null && gloves[1] == null) index = 1;
                    if (index >= 0 && index < 2) { oldItem = gloves[index]; gloves[index] = item; }
                    break;
                case ItemData.ItemType.Boots: 
                    if (index == 0 && boots[0] != null && boots[1] == null) index = 1;
                    if (index >= 0 && index < 2) { oldItem = boots[index]; boots[index] = item; }
                    break;
                case ItemData.ItemType.Accessory: 
                    if (index == 0 && accessories[0] != null && accessories[1] == null) index = 1;
                    if (index >= 0 && index < 2) { oldItem = accessories[index]; accessories[index] = item; }
                    break;
            }

            OnEquipmentChanged?.Invoke();
            return oldItem; 
        }

        /// <summary>
        /// 특정 타입의 슬롯에서 아이템을 해제합니다.
        /// </summary>
        public InventoryItem Unequip(ItemData.ItemType type, int index = 0)
        {
            InventoryItem unequipped = null;

            switch (type)
            {
                case ItemData.ItemType.Helmet: unequipped = helmet; helmet = null; break;
                case ItemData.ItemType.Armor: unequipped = armor; armor = null; break;
                case ItemData.ItemType.Weapon: unequipped = weapon; weapon = null; break;
                case ItemData.ItemType.Shield: unequipped = shield; shield = null; break;
                case ItemData.ItemType.Gloves: 
                    if (index >= 0 && index < 2) { unequipped = gloves[index]; gloves[index] = null; }
                    break;
                case ItemData.ItemType.Boots: 
                    if (index >= 0 && index < 2) { unequipped = boots[index]; boots[index] = null; }
                    break;
                case ItemData.ItemType.Accessory: 
                    if (index >= 0 && index < 2) { unequipped = accessories[index]; accessories[index] = null; }
                    break;
            }

            if (unequipped != null)
                OnEquipmentChanged?.Invoke();

            return unequipped;
        }

        public InventoryItem GetEquippedItem(ItemData.ItemType type, int index = 0)
        {
            switch (type)
            {
                case ItemData.ItemType.Helmet: return helmet;
                case ItemData.ItemType.Armor: return armor;
                case ItemData.ItemType.Weapon: return weapon;
                case ItemData.ItemType.Shield: return shield;
                case ItemData.ItemType.Gloves: return (index >= 0 && index < 2) ? gloves[index] : null;
                case ItemData.ItemType.Boots: return (index >= 0 && index < 2) ? boots[index] : null;
                case ItemData.ItemType.Accessory: return (index >= 0 && index < 2) ? accessories[index] : null;
                default: return null;
            }
        }
    }
}
