using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using TaeWhie.RPG.UI;

namespace TaeWhie.RPG.Inventory
{
    /// <summary>
    /// 게임 내 인벤토리 시스템을 총괄적으로 관리하는 매니저입니다.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("UI 설정")]
        [SerializeField] private UIDocument inventoryUIDocument;
        [SerializeField] private VisualTreeAsset inventoryViewTemplate;

        [Header("데이터")]
        [SerializeField] private CharacterData characterDataTemplate;
        
        // 캐릭터 이름(또는 ID)별 가방 데이터 매핑
        private Dictionary<string, InventoryData> characterInventories = new Dictionary<string, InventoryData>();
        
        private VisualElement root;
        private InventoryUIController uiController;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnEnable()
        {
            if (inventoryUIDocument == null) inventoryUIDocument = GetComponent<UIDocument>();
            
            root = inventoryUIDocument.rootVisualElement;
            root.style.display = DisplayStyle.None; // 초기에는 숨김

            uiController = new InventoryUIController(root);
            uiController.OnCloseRequested += () => CloseInventory();
            uiController.OnItemDroppedOutside += HandleItemDroppedOutside;
        }

        private void HandleItemDroppedOutside(InventoryItem item, Vector2 pointerPos)
        {
            // HUDManager를 통해 해당 위치에 캐릭터 유닛이 있는지 확인
            var hudManager = Object.FindFirstObjectByType<HUDManager>();
            if (hudManager == null) return;

            var targetUnit = hudManager.GetUnitAtPosition(pointerPos);
            if (targetUnit != null && targetUnit.CharacterData != null)
            {
                // 1. 캐릭터 인벤토리 전환 (Follow Drag & Switch)
                OpenInventory(targetUnit.CharacterData);

                // 2. 아이템 전송 시도 (Portrait Drop)
                string targetName = targetUnit.CharacterData.characterName;
                
                // 타겟 인벤토리 데이터 가져오기 (없으면 무시)
                if (characterInventories.TryGetValue(targetName, out var targetInventory))
                {
                    // 현재 잡고 있는 아이템을 타겟 가방의 빈 공간에 넣기
                    if (targetInventory.AddItemToFirstAvailableSlot(item))
                    {
                        Debug.Log($"[InventoryManager] Item {item.data.itemName} transferred to {targetName}");
                        uiController.ClearDraggingItem();
                        uiController.RefreshUI();
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryManager] {targetName}'s inventory is full!");
                        // 실패 시에는 아무 일도 일어나지 않음 (아이템이 여전히 들려있어서 Follow Drag 상태 유지)
                        uiController.RefreshUI(); // 배경은 새 캐릭터로 바뀌었으니 갱신
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (uiController != null)
            {
                uiController.Cleanup();
            }
        }

        /// <summary>
        /// 특정 캐릭터의 인벤토리를 엽니다.
        /// </summary>
        public void OpenInventory(CharacterData memberData)
        {
            if (memberData == null) return;
            
            string characterName = memberData.characterName;
            Debug.Log($"[InventoryManager] Opening inventory for: {characterName}");
            
            // 해당 캐릭터의 데이터가 없으면 새로 생성
            if (!characterInventories.TryGetValue(characterName, out var inventoryData))
            {
                inventoryData = ScriptableObject.CreateInstance<InventoryData>();
                inventoryData.name = $"{characterName}_Inventory";
                inventoryData.gridWidth = 10;
                inventoryData.gridHeight = 8;
                inventoryData.character = memberData; // 캐릭터 데이터 직접 연결
                inventoryData.InitializeOccupancy();
                characterInventories.Add(characterName, inventoryData);
                
                // 테스트용 아이템 몇 개 추가
                AddInitialTestItems(inventoryData);
            }

            uiController.DisplayInventory(characterName, inventoryData);
            root.style.display = DisplayStyle.Flex;
        }

        // 구 버전 호환성을 위한 오버로드 (이름만 받는 경우)
        public void OpenInventory(string characterName)
        {
            // 이 경로는 이제 템플릿을 사용하거나, 기존에 있는 데이터만 열 수 있습니다.
            if (characterInventories.TryGetValue(characterName, out var data))
            {
                uiController.DisplayInventory(characterName, data);
                root.style.display = DisplayStyle.Flex;
            }
            else
            {
                Debug.LogWarning($"[InventoryManager] No existing inventory data for {characterName}. Use OpenInventory(CharacterData) instead.");
            }
        }

        public void CloseInventory()
        {
            root.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// 새로 생성된 인벤토리에 테스트 아이템을 추가합니다.
        /// </summary>
        /// <summary>
        /// 새로 생성된 인벤토리에 테스트 아이템을 추가합니다.
        /// </summary>
        [Header("테스트용 아이템 데이터")]
        [SerializeField] private List<ItemData> testItems;

        /// <summary>
        /// 새로 생성된 인벤토리에 테스트 아이템을 추가합니다.
        /// </summary>
        private void AddInitialTestItems(InventoryData inventoryData)
        {
            if (testItems == null || testItems.Count == 0) return;

            Debug.Log($"[InventoryManager] 테스트 아이템 추가 시작: {inventoryData.name}");

            // 간단하게 리스트에 있는 아이템들을 순서대로 배치 시도
            int currentX = 0;
            int currentY = 0;

            foreach (var item in testItems)
            {
                if (item == null) continue;
                
                // 해당 위치에 배치가 가능하면 추가
                if (inventoryData.CanPlace(item, currentX, currentY))
                {
                    inventoryData.AddItem(item, currentX, currentY);
                    currentX += item.width;
                    if (currentX >= inventoryData.gridWidth)
                    {
                        currentX = 0;
                        currentY += 2; // 대략적인 줄바꿈
                    }
                }
            }
            
            Debug.Log($"[InventoryManager] {inventoryData.name}에 기본 아이템들이 추가되었습니다.");
        }

        [ContextMenu("Add Test Items")]
        public void AddInitialTestItems()
        {
            Debug.Log("[InventoryManager] 테스트 아이템 추가 기능을 직접 호출했습니다. 인스펙터에서 데이터를 확인하세요.");
        }
    }
}
