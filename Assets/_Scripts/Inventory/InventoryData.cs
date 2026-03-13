using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaeWhie.RPG.Inventory
{
    /// <summary>
    /// 실제 가방 안에 들어있는 아이템 인스턴스 정보입니다.
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        public ItemData data;
        public int positionX; // 그리드 X 좌표
        public int positionY; // 그리드 Y 좌표
        public bool isRotated; // 회전 여부 (가로 세로 뒤집힘)

        public int CurrentWidth => isRotated ? data.height : data.width;
        public int CurrentHeight => isRotated ? data.width : data.height;

        public InventoryItem(ItemData data, int x, int y)
        {
            this.data = data;
            this.positionX = x;
            this.positionY = y;
            this.isRotated = false;
        }

        public void Rotate()
        {
            isRotated = !isRotated;
        }
    }

    /// <summary>
    /// 캐릭터별 가방 데이터를 관리하는 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewInventory", menuName = "RPG/Inventory/Inventory Data")]
    public class InventoryData : ScriptableObject
    {
        [Header("캐릭터 정보")]
        public TaeWhie.RPG.UI.CharacterData character;

        [Header("가방 설정")]
        public int gridWidth = 10;
        public int gridHeight = 8;

        [Header("담긴 아이템들")]
                public List<InventoryItem> items = new List<InventoryItem>();
        public CharacterEquipment equipment = new CharacterEquipment();

        // 그리드 점유 여부 (런타임용)
        private bool[,] occupancyGrid;

        /// <summary>
        /// 그리드 점유 데이터를 초기화하고 동기화합니다.
        /// </summary>
        public void InitializeOccupancy()
        {
            occupancyGrid = new bool[gridWidth, gridHeight];
            foreach (var item in items)
            {
                MarkOccupancy(item, true);
            }
        }

        /// <summary>
        /// 특정 위치에 특정 크기의 아이템을 놓을 수 있는지 확인합니다.
        /// </summary>
        public bool CanPlace(int width, int height, int x, int y, InventoryItem ignoreItem = null)
        {
            // 범위를 벗어나는지 체크
            if (x < 0 || y < 0 || x + width > gridWidth || y + height > gridHeight)
                return false;

            // 이미 점유된 칸이 있는지 체크
            for (int i = x; i < x + width; i++)
            {
                for (int j = y; j < y + height; j++)
                {
                    if (occupancyGrid == null) InitializeOccupancy();
                    if (occupancyGrid[i, j])
                    {
                        // 자기 자신(이동 중인 아이템)의 자리는 무시하도록 처리 가능
                        if (ignoreItem != null && IsItemAt(ignoreItem, i, j))
                            continue;
                        
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 특정 위치에 아이템(InventoryItem 인스턴스)을 놓을 수 있는지 확인합니다.
        /// </summary>
        public bool CanPlace(InventoryItem item, int x, int y, bool ignoreSelf = true)
        {
            return CanPlace(item.CurrentWidth, item.CurrentHeight, x, y, ignoreSelf ? item : null);
        }

        /// <summary>
        /// 특정 위치에 아이템 데이터를 기반으로 놓을 수 있는지 확인합니다. (신규 생성 시)
        /// </summary>
        public bool CanPlace(ItemData itemData, int x, int y)
        {
            return CanPlace(itemData.width, itemData.height, x, y);
        }

        private bool IsItemAt(InventoryItem item, int x, int y)
        {
            return x >= item.positionX && x < item.positionX + item.CurrentWidth &&
                   y >= item.positionY && y < item.positionY + item.CurrentHeight;
        }

        /// <summary>
        /// 아이템을 가방에 추가합니다.
        /// </summary>
        /// <summary>
        /// 아이템 데이터를 기반으로 새 아이템을 생성하여 추가합니다.
        /// </summary>
        public bool AddItem(ItemData itemData, int x, int y)
        {
            if (CanPlace(itemData, x, y))
            {
                InventoryItem newItem = new InventoryItem(itemData, x, y);
                items.Add(newItem);
                MarkOccupancy(newItem, true);
                return true;
            }
            return false;
        }

        public bool PlaceItem(InventoryItem item, int x, int y)
        {
            if (CanPlace(item, x, y, true))
            {
                item.positionX = x;
                item.positionY = y;
                items.Add(item);
                MarkOccupancy(item, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 아이템을 가방에서 제거합니다.
        /// </summary>
        public void RemoveItem(InventoryItem item)
        {
            if (items.Remove(item))
            {
                MarkOccupancy(item, false);
            }
        }

        public void MarkOccupancy(InventoryItem item, bool isOccupied)
        {
            if (occupancyGrid == null) InitializeOccupancy();
            for (int x = item.positionX; x < item.positionX + item.CurrentWidth; x++)
            {
                for (int y = item.positionY; y < item.positionY + item.CurrentHeight; y++)
                {
                    occupancyGrid[x, y] = isOccupied;
                }
            }
        }
    

        public bool AddItemToFirstAvailableSlot(InventoryItem item)
        {
            for (int y = 0; y <= gridHeight - item.CurrentHeight; y++)
            {
                for (int x = 0; x <= gridWidth - item.CurrentWidth; x++)
                {
                    if (CanPlace(item, x, y, false))
                    {
                        return PlaceItem(item, x, y);
                    }
                }
            }
            return false;
        }
}
}
