using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaeWhie.RPG.Inventory
{
    /// <summary>
    /// UI Toolkit을 이용한 인벤토리 화면 제어 및 드래그 앤 드롭 로직을 담당합니다.
    /// </summary>
    public class InventoryUIController
    {
        private VisualElement root;
        private Label characterNameLabel;
        private VisualElement gridContainer;
        private VisualElement gridBackground;
                private VisualElement itemContainer;
        private VisualElement placementGuide;
        private VisualElement dragOverlay;
        private Button closeButton;
        private VisualElement equipmentSection;
        
        // 툴팁 관련 요소
        private VisualElement tooltipRoot;
        private Label tooltipNameLabel;
        private Label tooltipStatsLabel;
        private Label tooltipDescLabel;
        
        // 장비 슬롯 관리를 위한 정보
        private struct EquipmentSlotInfo
        {
            public ItemData.ItemType type;
            public int index;
            public VisualElement element;
        }
        private List<EquipmentSlotInfo> equipmentSlots = new List<EquipmentSlotInfo>();

        private InventoryData currentData;
        private float slotSize = 50f; // USS의 --slot-size와 동일하게 설정

        // 현재 드래그 중인 아이템 정보
        private InventoryItem draggingItem;
        private InventoryData dragOriginData; // 아이템이 처음 집어진 곳 (취소 시 복구용)
        private VisualElement draggingElement;
        private Vector2 dragOffset;
        private int activePointerId = -1;

        public event Action OnCloseRequested;
        public event Action<InventoryItem, Vector2> OnItemDroppedOutside; // 인벤토리 외부(HUD 등)에 드롭했을 때

        public InventoryUIController(VisualElement rootElement)
        {
            root = rootElement;
            characterNameLabel = root.Q<Label>("CharacterNameLabel");
            gridContainer = root.Q<VisualElement>("GridContainer");
            gridBackground = root.Q<VisualElement>("GridBackground");

                        itemContainer = root.Q<VisualElement>("ItemContainer");
            placementGuide = root.Q<VisualElement>("PlacementGuide");
                        dragOverlay = root.Q<VisualElement>("DragOverlay");
            dragOverlay.pickingMode = PickingMode.Ignore; // 명시적으로 클릭 통과하도록 설정
            closeButton = root.Q<Button>("CloseButton");
            equipmentSection = root.Q<VisualElement>("EquipmentSection");

            // 툴팁 요소 초기화
            tooltipRoot = root.Q<VisualElement>("Tooltip");
            tooltipNameLabel = root.Q<Label>("Tooltip_Name");
            tooltipStatsLabel = root.Q<Label>("Tooltip_Stats");
            tooltipDescLabel = root.Q<Label>("Tooltip_Description");

            // 장비 슬롯 초기화 및 이벤트 등록
            SetupEquipmentSlot(ItemData.ItemType.Helmet, "Slot_Helmet");
            SetupEquipmentSlot(ItemData.ItemType.Weapon, "Slot_Weapon");
            SetupEquipmentSlot(ItemData.ItemType.Armor, "Slot_Armor");
            SetupEquipmentSlot(ItemData.ItemType.Shield, "Slot_Shield");
            
            SetupEquipmentSlot(ItemData.ItemType.Gloves, "Slot_Gloves_0", 0);
            SetupEquipmentSlot(ItemData.ItemType.Gloves, "Slot_Gloves_1", 1);
            SetupEquipmentSlot(ItemData.ItemType.Boots, "Slot_Boots");
            SetupEquipmentSlot(ItemData.ItemType.Accessory, "Slot_Accessory_0", 0);
            SetupEquipmentSlot(ItemData.ItemType.Accessory, "Slot_Accessory_1", 1);

            closeButton.clicked += () => OnCloseRequested?.Invoke();
            
            // 전체 루트에 클릭 이벤트 등록 (토글 방식의 '내려놓기'를 위해)
            root.RegisterCallback<PointerDownEvent>(OnGlobalPointerDown);

            // 전역 포인터 이벤트 등록 (드래그 처리)
            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);

            // 스탯 버튼 이벤트 등록
            RegisterStatButtons();
        }

        public void DisplayInventory(string characterName, InventoryData data)
        {
            // 다른 캐릭터 인벤토리로 전환 시, 'Follow Drag'를 위해 들고 있는 아이템은 반환하지 않고 유지합니다.
            // 단, 현재 창의 배경 데이터(CurrentData)만 교체합니다.
            
            currentData = data;
            
            // CharacterData가 있으면 그 이름을 우선 사용
            string finalName = (currentData.character != null && !string.IsNullOrEmpty(currentData.character.characterName)) 
                ? currentData.character.characterName 
                : characterName;

            characterNameLabel.text = $"{finalName}'s Inventory";
            
            // 그리드 컨테이너 크기 조절
            gridContainer.style.width = currentData.gridWidth * slotSize;
            gridContainer.style.height = currentData.gridHeight * slotSize;

            // 1. 슬롯 생성 (배경 격자)
            gridBackground.Clear();
            int totalSlots = currentData.gridWidth * currentData.gridHeight;
            for (int i = 0; i < totalSlots; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("inventory-slot");
                gridBackground.Add(slot);
            }

                        RefreshUI();
        }

        public void Cleanup()
        {
            if (root == null) return;
            
            closeButton.clicked -= () => OnCloseRequested?.Invoke();
            root.UnregisterCallback<PointerDownEvent>(OnGlobalPointerDown);
            root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);

            // 중요: 인벤토리 파괴(또는 씬 종료) 시 잡고 있던 아이템이 있다면 원본 주인에게 돌려줌
            ReturnHeldItemToOrigin();
            
            root.Q<Button>("Btn_Add_ATK").clicked -= () => AddStatPoint("atk");
            root.Q<Button>("Btn_Add_DEF").clicked -= () => AddStatPoint("def");
            root.Q<Button>("Btn_Add_STR").clicked -= () => AddStatPoint("str");
            root.Q<Button>("Btn_Add_DEX").clicked -= () => AddStatPoint("dex");
            root.Q<Button>("Btn_Add_INT").clicked -= () => AddStatPoint("int");
        }

        private void RegisterStatButtons()
        {
            root.Q<Button>("Btn_Add_ATK").clicked += () => AddStatPoint("atk");
            root.Q<Button>("Btn_Add_DEF").clicked += () => AddStatPoint("def");
            root.Q<Button>("Btn_Add_STR").clicked += () => AddStatPoint("str");
            root.Q<Button>("Btn_Add_DEX").clicked += () => AddStatPoint("dex");
            root.Q<Button>("Btn_Add_INT").clicked += () => AddStatPoint("int");
        }

        private void AddStatPoint(string statName)
        {
            if (currentData == null || currentData.character == null) return;
            if (currentData.character.statPoints <= 0) return;

            currentData.character.statPoints--;
            
            switch (statName)
            {
                case "atk": currentData.character.atk++; break;
                case "def": currentData.character.def++; break;
                case "str": currentData.character.str++; break;
                case "dex": currentData.character.dex++; break;
                case "int": currentData.character.@int++; break;
            }

            RefreshStatsUI();
        }
        public void RefreshUI()
        {
            itemContainer.Clear();
            if (currentData == null) return;

            foreach (var item in currentData.items)
            {
                CreateItemElement(item);
            }

            RefreshEquipmentUI();
            RefreshStatsUI();
        }

        private VisualElement CreateItemElement(InventoryItem item)
        {
            var element = new VisualElement();
            element.AddToClassList("inventory-item-ui");
            element.pickingMode = PickingMode.Position;

            element.style.width = item.CurrentWidth * slotSize;
            element.style.height = item.CurrentHeight * slotSize;
            element.style.left = item.positionX * slotSize;
            element.style.top = item.positionY * slotSize;

            var icon = new VisualElement();
            icon.name = "Icon"; // 이름 부여하여 나중에 찾기 쉽게 함
            icon.AddToClassList("inventory-item-icon");
            icon.pickingMode = PickingMode.Ignore;

            if (item.data.icon != null)
                icon.style.backgroundImage = new StyleBackground(item.data.icon);
            
            // 회전 상태 반영 (아이콘 레이어 회전)
            icon.style.rotate = new Rotate(Angle.Degrees(item.isRotated ? 90 : 0));
            
            element.Add(icon);
            itemContainer.Add(element);

            // 클릭 이벤트 등록
            element.RegisterCallback<PointerDownEvent>(evt => 
            {
                if (evt.button == 0) // 왼쪽 클릭: 토글 집기
                {
                    OnItemPointerDown(evt, item, element);
                }
                else if (evt.button == 1) // 오른쪽 클릭: 빠른 장착
                {
                    QuickEquip(item);
                    evt.StopPropagation();
                }
            });

            // 마우스 오버(툴팁) 이벤트 등록
            element.RegisterCallback<PointerEnterEvent>(evt => ShowTooltip(item, evt.position));
            element.RegisterCallback<PointerLeaveEvent>(evt => HideTooltip());

            return element;
}

        private void OnItemPointerDown(PointerDownEvent evt, InventoryItem item, VisualElement element)
        {
            draggingItem = item;
            draggingElement = element;

            // 드래그 시작 시 툴팁 숨김
            HideTooltip();

            // 가방 데이터에서 일시 제거 (그리드 점유 해제)
            currentData.RemoveItem(item);
            
            // 드래그 레이어로 이동하여 다른 패널 위에 보이게 함
            Vector2 worldPos = element.worldBound.position;
            dragOverlay.Add(element);
            
            // 드래그 레이어 기준의 로컬 위치로 재설정
            Vector2 localInOverlay = dragOverlay.WorldToLocal(worldPos);
            element.style.left = localInOverlay.x;
            element.style.top = localInOverlay.y;

            element.AddToClassList("dragging");

            // 드래그 오프셋 설정: 아이템의 정중앙을 잡도록 변경 (사용자 요청)
            dragOffset = new Vector2((item.CurrentWidth * slotSize) / 2f, (item.CurrentHeight * slotSize) / 2f);

            // 원래 주인 기록 (다른 창으로 가더라도 안전한 반환을 위해)
            dragOriginData = currentData;

            // 배치 가이드 초기화 및 즉시 갱신
            placementGuide.style.display = DisplayStyle.Flex;
            placementGuide.style.width = item.CurrentWidth * slotSize;
            placementGuide.style.height = item.CurrentHeight * slotSize;
            
            UpdateVisualPositions(evt.position);

            activePointerId = evt.pointerId;
            root.CapturePointer(evt.pointerId);
            evt.StopPropagation();
            
            Debug.Log($"[InventoryUIController] Picked up: {item.data.itemName} (Rotated: {item.isRotated})");
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (draggingItem != null && draggingElement != null)
            {
                UpdateVisualPositions(evt.position);
            }
            
            // 툴팁 위치 업데이트
            if (tooltipRoot.resolvedStyle.display == DisplayStyle.Flex)
            {
                UpdateTooltipPosition(evt.position);
            }
        }

        private void UpdateVisualPositions(Vector2 mousePos)
        {
            if (draggingItem == null || draggingElement == null) return;

            // 1. 아이템 이미지 위치 업데이트 (DragOverlay 기준)
            Vector2 localInOverlay = dragOverlay.WorldToLocal(mousePos);
            draggingElement.style.left = localInOverlay.x - dragOffset.x;
            draggingElement.style.top = localInOverlay.y - dragOffset.y;

            // 2. 가이드 박스 위치 업데이트 (GridContainer 기준)
            Vector2 mouseGridPos = gridContainer.WorldToLocal(mousePos);
            Vector2 targetPos = mouseGridPos - dragOffset;
            
            int gridX = Mathf.RoundToInt(targetPos.x / slotSize);
            int gridY = Mathf.RoundToInt(targetPos.y / slotSize);

            placementGuide.style.left = gridX * slotSize;
            placementGuide.style.top = gridY * slotSize;

            // 3. 배치 가능 여부 시각화
            if (currentData.CanPlace(draggingItem, gridX, gridY, true))
            {
                placementGuide.RemoveFromClassList("placement-invalid");
            }
            else
            {
                placementGuide.AddToClassList("placement-invalid");
            }
        }

        private void RotateHeldItem(Vector2 mousePos)
        {
            if (draggingItem == null || draggingElement == null) return;

            // 회전 상태 변경
            draggingItem.Rotate();

            // 비주얼 크기 업데이트
            draggingElement.style.width = draggingItem.CurrentWidth * slotSize;
            draggingElement.style.height = draggingItem.CurrentHeight * slotSize;
            
            // 시각적 회전 적용 (Unity 2021.2+ style.rotate 속성 사용)
            // 기준점을 중심으로 회전시키려면 transform-origin 설정이 필요할 수 있으나,
            // 이미지를 직접 회전시키거나 아이콘 레이어만 회전시키는 것이 더 깔끔할 수 있습니다.
            var icon = draggingElement.Q<VisualElement>("Icon");
            if (icon != null)
            {
                icon.style.rotate = new Rotate(Angle.Degrees(draggingItem.isRotated ? 90 : 0));
            }

            // 가이드 크기 업데이트
            placementGuide.style.width = draggingItem.CurrentWidth * slotSize;
            placementGuide.style.height = draggingItem.CurrentHeight * slotSize;

            // 회전 보정 (중심 기준으로 회전하는 느낌을 주기 위해 오프셋 조정)
            float oldX = dragOffset.x;
            dragOffset.x = dragOffset.y;
            dragOffset.y = oldX;

            // 비주얼 위치 즉시 갱신
            UpdateVisualPositions(mousePos);
            
            Debug.Log($"[InventoryUIController] Visual Rotation Applied: {draggingItem.isRotated}");
        }

        private void ShowTooltip(InventoryItem item, Vector2 position)
        {
            if (draggingItem != null) return; // 드래그 중에는 툴팁 안띄움

            tooltipNameLabel.text = item.data.itemName;
            tooltipDescLabel.text = item.data.description;

            // 스탯 텍스트 구성
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (item.data.atk != 0) sb.AppendLine($"공격력: +{item.data.atk}");
            if (item.data.def != 0) sb.AppendLine($"방어력: +{item.data.def}");
            if (item.data.str != 0) sb.AppendLine($"힘(STR): +{item.data.str}");
            if (item.data.dex != 0) sb.AppendLine($"민첩(DEX): +{item.data.dex}");
            if (item.data.@int != 0) sb.AppendLine($"지능(INT): +{item.data.@int}");

            tooltipStatsLabel.text = sb.ToString();
            tooltipStatsLabel.style.display = sb.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            tooltipRoot.style.display = DisplayStyle.Flex;
            UpdateTooltipPosition(position);
        }

        private void HideTooltip()
        {
            tooltipRoot.style.display = DisplayStyle.None;
        }

        private void UpdateTooltipPosition(Vector2 mousePos)
        {
            // 루트 윈도우 기준 상대 좌표로 변환
            Vector2 localPos = root.WorldToLocal(mousePos);
            
            // 아직 레이아웃이 계산되지 않았을 경우를 대비한 최소값/기본값 잡기
            float tooltipWidth = tooltipRoot.resolvedStyle.width;
            float tooltipHeight = tooltipRoot.resolvedStyle.height;
            
            // 0이거나 NaN이면 대략적인 예상 크기로 대체
            if (float.IsNaN(tooltipWidth) || tooltipWidth <= 0) tooltipWidth = 200;
            if (float.IsNaN(tooltipHeight) || tooltipHeight <= 0) tooltipHeight = 100;
            
            // 기본값: 마우스 왼쪽 (사용자 요청에 따라 변경)
            float targetX = localPos.x - tooltipWidth - 20;
            float targetY = localPos.y + 20;

            // 1. 왼쪽 경계 체크 -> 잘리면 오른쪽으로 플립
            if (targetX < 5)
            {
                targetX = localPos.x + 20;
            }

            // 2. 하단 경계 체크 -> 잘리면 위쪽으로 플립
            if (targetY + tooltipHeight > root.layout.height)
            {
                targetY = localPos.y - tooltipHeight - 10;
            }

            // 3. 오른쪽 경계 최종 보정 (플립했는데도 오른쪽으로 나가버리는 경우)
            if (targetX + tooltipWidth > root.layout.width)
            {
                targetX = root.layout.width - tooltipWidth - 5;
            }

            // 4. 상단 경계 최종 보정
            if (targetY < 5) targetY = 5;

            tooltipRoot.style.left = targetX;
            tooltipRoot.style.top = targetY;
        }


        private void OnGlobalPointerDown(PointerDownEvent evt)
        {
            if (draggingItem == null || draggingElement == null) return;

            // 오른쪽 클릭 시: 회전
            if (evt.button == 1)
            {
                RotateHeldItem(evt.position);
                evt.StopPropagation();
                return;
            }

            // 왼쪽 클릭 시: 배치 시도
            if (evt.button == 0)
            {
                // 1. 장비 슬롯 확인
                foreach (var slotInfo in equipmentSlots)
                {
                    if (slotInfo.element.worldBound.Contains(evt.position))
                    {
                        OnEquipmentSlotPointerDown(evt, slotInfo.type, slotInfo.index);
                        return;
                    }
                }

                // 2. 그리드 영역 확인
                Vector2 mouseGridPos = gridContainer.WorldToLocal(evt.position);
                
                if (mouseGridPos.x >= 0 && mouseGridPos.y >= 0 && 
                    mouseGridPos.x <= gridContainer.layout.width && mouseGridPos.y <= gridContainer.layout.height)
                {
                    Vector2 targetPos = mouseGridPos - dragOffset;
                    int gridX = Mathf.RoundToInt(targetPos.x / slotSize);
                    int gridY = Mathf.RoundToInt(targetPos.y / slotSize);

                    if (currentData.CanPlace(draggingItem, gridX, gridY, true))
                    {
                        currentData.PlaceItem(draggingItem, gridX, gridY);
                        
                        if (draggingElement != null)
                            draggingElement.RemoveFromHierarchy();

                        if (activePointerId != -1 && root.HasPointerCapture(activePointerId))
                            root.ReleasePointer(activePointerId);

                        draggingItem = null;
                        draggingElement = null;
                        activePointerId = -1;
                        placementGuide.style.display = DisplayStyle.None;

                        RefreshUI();
                        evt.StopPropagation();
                    }
                    else
                    {
                        // 배치 불가능한 위치거나 그리드 밖인 경우 -> 외부 드롭 이벤트 알림 (Portrait Drop 등을 위해)
                        OnItemDroppedOutside?.Invoke(draggingItem, evt.position);
                        evt.StopPropagation();
                    }
                }
                else
                {
                    // 그리드 영역 밖에서 클릭됨 -> 외부 드롭 이벤트 알림
                    OnItemDroppedOutside?.Invoke(draggingItem, evt.position);
                    evt.StopPropagation();
                }
            }
        }
    

        private void SetupEquipmentSlot(ItemData.ItemType type, string elementName, int index = 0)
        {
            var slot = root.Q<VisualElement>(elementName);
            if (slot != null)
            {
                equipmentSlots.Add(new EquipmentSlotInfo { type = type, index = index, element = slot });
                slot.RegisterCallback<PointerDownEvent>(evt => 
                {
                    if (evt.button == 0) // 왼쪽 클릭: 집기/장착/교체
                    {
                        OnEquipmentSlotPointerDown(evt, type, index);
                    }
                    else if (evt.button == 1) // 오른쪽 클릭: 빠른 해제
                    {
                        QuickUnequip(type, index);
                        evt.StopPropagation();
                    }
                });

                // 장착 슬롯 툴팁 등록
                slot.RegisterCallback<PointerEnterEvent>(evt => 
                {
                    InventoryItem equipped = currentData.equipment.GetEquippedItem(type, index);
                    if (equipped != null) ShowTooltip(equipped, evt.position);
                });
                slot.RegisterCallback<PointerLeaveEvent>(evt => HideTooltip());
            }
        }

        private void QuickUnequip(ItemData.ItemType type, int index = 0)
        {
            if (currentData == null) return;

            InventoryItem equipped = currentData.equipment.GetEquippedItem(type, index);
            if (equipped != null)
            {
                if (currentData.AddItemToFirstAvailableSlot(equipped))
                {
                    currentData.equipment.Unequip(type, index);
                    HideTooltip(); // 장착 해제 시 툴팁 숨김
                    RefreshUI();
                }
                else
                {
                    Debug.LogWarning("Inventory full! Cannot quick unequip.");
                }
            }
        }

        private void OnEquipmentSlotPointerDown(PointerDownEvent evt, ItemData.ItemType slotType, int index = 0)
        {
            if (currentData == null) return;

            // 1. 아이템을 들고 있는 경우: 장착/교체 시도
            if (draggingItem != null)
            {
                if (draggingItem.data.itemType == slotType)
                {
                    Debug.Log($"[InventoryUIController] Equipping/Swapping {draggingItem.data.itemName} to {slotType} ({index})");
                    
                    InventoryItem itemToEquip = draggingItem;
                    
                    if (draggingElement != null)
                        draggingElement.RemoveFromHierarchy();
                    
                    // 장착 시도
                    InventoryItem oldItem = currentData.equipment.Equip(itemToEquip, index);
                    
                    HideTooltip(); // 장착 시에도 툴팁 숨김 (아이템이 마우스 밑으로 들어가므로)
                    
                    draggingItem = null;
                    draggingElement = null;
                    placementGuide.style.display = DisplayStyle.None;

                    if (oldItem != null)
                    {
                        RefreshUI();
                        SetAsHeldItem(oldItem, evt.pointerId, evt.position);
                    }
                    else
                    {
                        if (root.HasPointerCapture(evt.pointerId))
                            root.ReleasePointer(evt.pointerId);
                        RefreshUI();
                    }
                    evt.StopPropagation();
                }
            }
            // 2. 아이템을 들고 있지 않은 경우: 해제 시도
            else
            {
                InventoryItem equipped = currentData.equipment.GetEquippedItem(slotType, index);
                if (equipped != null)
                {
                    currentData.equipment.Unequip(slotType, index);
                    RefreshUI();
                    
                    HideTooltip(); // 장착 슬롯에서 뺄 때 툴팁 숨김
                    SetAsHeldItem(equipped, evt.pointerId, evt.position);
                    
                    activePointerId = evt.pointerId;
                    
                    // 원래 주인 기록
                    dragOriginData = currentData;
                    
                    evt.StopPropagation();
                }
            }
        }

        /// <summary>
        /// 특정 아이템을 즉시 마우스에 들린 상태(Holding/Dragging)로 만듭니다.
        /// </summary>
        private void SetAsHeldItem(InventoryItem item, int pointerId, Vector2 pointerPosition)
        {
            // 1. 임시 비주얼 엘리먼트 생성
            VisualElement element = CreateItemElement(item);
            element.AddToClassList("dragging");
            
            // 인벤토리 밖(슬롯 등)에서 생성될 때는 원본 크기 반영
            element.style.width = item.CurrentWidth * slotSize;
            element.style.height = item.CurrentHeight * slotSize;
            
            dragOverlay.Add(element);

            draggingItem = item;
            draggingElement = element;
            
            // 부위별 장착 슬롯에서 뺄 때는 아이템의 정중앙을 잡도록 설정
            dragOffset = new Vector2((item.CurrentWidth * slotSize) / 2f, (item.CurrentHeight * slotSize) / 2f);

            placementGuide.style.display = DisplayStyle.Flex;
            placementGuide.style.width = item.CurrentWidth * slotSize;
            placementGuide.style.height = item.CurrentHeight * slotSize;

            UpdateVisualPositions(pointerPosition);
            
            root.CapturePointer(pointerId);
        }

        private void RefreshEquipmentUI()
        {
            if (currentData == null) return;

            foreach (var slotInfo in equipmentSlots)
            {
                VisualElement iconLayer = slotInfo.element.Q<VisualElement>("Icon");
                InventoryItem equipped = currentData.equipment.GetEquippedItem(slotInfo.type, slotInfo.index);
                
                if (equipped != null && equipped.data.icon != null)
                {
                    iconLayer.style.backgroundImage = new StyleBackground(equipped.data.icon);
                    iconLayer.style.opacity = 1.0f;
                }
                else
                {
                    iconLayer.style.backgroundImage = null;
                    iconLayer.style.opacity = 0.3f;
                }
            }
        }

        private void RefreshStatsUI()
        {
            var atkLabel = root.Q<Label>("Stat_ATK");
            var defLabel = root.Q<Label>("Stat_DEF");
            var strLabel = root.Q<Label>("Stat_STR");
            var dexLabel = root.Q<Label>("Stat_DEX");
            var intLabel = root.Q<Label>("Stat_INT");

            // 캐릭터 데이터에서 베이스 스탯 가져오기 (없으면 0)
            int totalAtk = currentData.character != null ? currentData.character.atk : 0;
            int totalDef = currentData.character != null ? currentData.character.def : 0;
            int totalStr = currentData.character != null ? currentData.character.str : 0;
            int totalDex = currentData.character != null ? currentData.character.dex : 0;
            int totalInt = currentData.character != null ? currentData.character.@int : 0;

            // 모든 장착 슬롯 순회하며 스탯 합산
            void AddStats(InventoryItem item)
            {
                if (item != null && item.data != null)
                {
                    totalAtk += item.data.atk;
                    totalDef += item.data.def;
                    totalStr += item.data.str;
                    totalDex += item.data.dex;
                    totalInt += item.data.@int;
                }
            }

            AddStats(currentData.equipment.helmet);
            AddStats(currentData.equipment.armor);
            AddStats(currentData.equipment.weapon);
            AddStats(currentData.equipment.shield);
            foreach (var g in currentData.equipment.gloves) AddStats(g);
            foreach (var b in currentData.equipment.boots) AddStats(b);
            foreach (var a in currentData.equipment.accessories) AddStats(a);

            if (atkLabel != null) atkLabel.text = totalAtk.ToString();
            if (defLabel != null) defLabel.text = totalDef.ToString();
            if (strLabel != null) strLabel.text = totalStr.ToString();
            if (dexLabel != null) dexLabel.text = totalDex.ToString();
            if (intLabel != null) intLabel.text = totalInt.ToString();

            // 포인트 및 버튼 상태 업데이트
            var pointsLabel = root.Q<Label>("Stat_Points");
            bool hasPoints = currentData.character != null && currentData.character.statPoints > 0;
            
            if (pointsLabel != null) 
                pointsLabel.text = currentData.character != null ? currentData.character.statPoints.ToString() : "0";

            root.Query<Button>(className: "add-button").ForEach(btn => {
                btn.style.display = hasPoints ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }


        /// <summary>
        /// 아이템을 즉시 알맞은 부위에 장착합니다. (오른쪽 클릭)
        /// </summary>
        private void QuickEquip(InventoryItem item)
        {
            if (item.data.itemType == ItemData.ItemType.Etc) return;

            Debug.Log($"[InventoryUIController] QuickEquip: {item.data.itemName}");

            // 현재 가방에서 제거
            currentData.RemoveItem(item);

            // 장착 시도 (이전 아이템이 있으면 교체되어 나옴)
            InventoryItem previous = currentData.equipment.Equip(item);

            // 이전 아이템이 있었다면 다시 가방의 빈 공간에 넣기
            if (previous != null)
            {
                if (!currentData.AddItemToFirstAvailableSlot(previous))
                {
                    // 자리가 없으면 에러 (나중에 바닥에 떨어뜨리는 로직 등으로 확장 가능)
                    Debug.LogWarning("Inventory full! Could not swap item back.");
                }
            }

            HideTooltip(); // 장착 시 툴팁 숨김
            RefreshUI();
        }

        public void ClearDraggingItem()
        {
            if (activePointerId != -1 && root.HasPointerCapture(activePointerId))
                root.ReleasePointer(activePointerId);

            if (draggingElement != null)
                draggingElement.RemoveFromHierarchy();

            draggingItem = null;
            draggingElement = null;
            dragOriginData = null;
            activePointerId = -1;
            placementGuide.style.display = DisplayStyle.None;
        }

        private void ReturnHeldItemToOrigin()
        {
            if (draggingItem != null && dragOriginData != null)
            {
                // 원래 위치로 복구 시도
                if (!dragOriginData.PlaceItem(draggingItem, draggingItem.positionX, draggingItem.positionY))
                {
                    // 불가능하면 첫 빈자리
                    dragOriginData.AddItemToFirstAvailableSlot(draggingItem);
                }

                if (draggingElement != null && draggingElement.parent != null)
                    draggingElement.RemoveFromHierarchy();

                if (activePointerId != -1 && root.HasPointerCapture(activePointerId))
                    root.ReleasePointer(activePointerId);

                draggingItem = null;
                draggingElement = null;
                dragOriginData = null;
                activePointerId = -1;
                if (placementGuide != null) placementGuide.style.display = DisplayStyle.None;
            }
        }
    }
}
