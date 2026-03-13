using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaeWhie.RPG.UI
{
    /// <summary>
    /// 전체 HUD 시스템을 총괄하는 매니저 클래스입니다.
    /// 파티 멤버 데이터를 기반으로 동적으로 UI를 생성하고 이벤트를 관리합니다.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;          // 씬의 UIDocument 참조
        [SerializeField] private VisualTreeAsset characterUnitTemplate;  // 개별 캐릭터 UXML 템플릿

        [Header("파티 데이터")]
        [SerializeField] private List<CharacterData> partyMembers; // 파티 멤버 리스트

        private VisualElement container;
        private List<UI_CharacterUnit> unitLogics = new List<UI_CharacterUnit>();

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            
            // HUD 컨테이너 찾기
            var root = uiDocument.rootVisualElement;
            container = root.Q<VisualElement>("HUDContainer");

            // 초기 HUD 생성
            InitializeHUD();
        }

        /// <summary>
        /// 파티 데이터를 기반으로 HUD를 초기화하고 캐릭터 유닛들을 생성합니다.
        /// </summary>
        [ContextMenu("Initialize HUD")]
        public void InitializeHUD()
        {
            if (container == null)
            {
                var root = uiDocument.rootVisualElement;
                container = root.Q<VisualElement>("HUDContainer");
            }

            if (container == null || characterUnitTemplate == null) return;

            // 기존 요소 제거
            container.Clear();
            unitLogics.Clear();

            foreach (var member in partyMembers)
            {
                if (member == null) continue;

                // 템플릿 인스턴스화
                VisualElement unitElement = characterUnitTemplate.Instantiate();
                container.Add(unitElement);

                // 로직 클래스 연결
                UI_CharacterUnit logic = new UI_CharacterUnit(unitElement, member);
                
                // 인벤토리 오픈 이벤트 구독
                logic.OnInventoryOpenRequested += HandleInventoryOpen;
                
                unitLogics.Add(logic);
            }
        }

        /// <summary>
        /// 인벤토리 열기 요청을 처리합니다 (기획 요구사항: Debug.Log 구현).
        /// </summary>
        /// <param name="characterName">인벤토리를 열 캐릭터 이름</param>
        /// <summary>
        /// 인벤토리 열기 요청을 처리합니다.
        /// </summary>
        /// <param name="memberData">인벤토리를 열 캐릭터 데이터</param>
        private void HandleInventoryOpen(CharacterData memberData)
        {
            if (memberData == null) return;
            
            Debug.Log($"[HUDManager] {memberData.characterName}의 전용 인벤토리를 엽니다.");
            
            // 인벤토리 매니저를 통해 해당 캐릭터의 가방을 엽니다.
            if (TaeWhie.RPG.Inventory.InventoryManager.Instance != null)
            {
                TaeWhie.RPG.Inventory.InventoryManager.Instance.OpenInventory(memberData);
            }
        }

        /// <summary>
        /// 특정 캐릭터의 UI를 강제로 갱신하고자 할 때 호출합니다.
        /// </summary>
        public void RefreshHUD()
        {
            foreach (var logic in unitLogics)
            {
                logic.UpdateUI();
            }
        }

        /// <summary>
        /// 화면 좌표를 기준으로 해당 위치에 있는 캐릭터 유닛 로직을 반환합니다.
        /// </summary>
        public UI_CharacterUnit GetUnitAtPosition(Vector2 screenPos)
        {
            foreach (var unit in unitLogics)
            {
                if (unit.ContainsPoint(screenPos)) return unit;
            }
            return null;
        }
    }
}
