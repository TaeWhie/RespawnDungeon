using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaeWhie.RPG.UI
{
    /// <summary>
    /// 개별 캐릭터 유닛 UI의 로직을 담당하는 클래스입니다.
    /// UI Toolkit의 VisualElement를 래핑하여 데이터를 바인딩합니다.
    /// </summary>
    public class UI_CharacterUnit
    {
        private VisualElement root;
        private VisualElement portrait;
        private VisualElement hpFill;
        private VisualElement mpFill;
        private Label hpText;
        private Label mpText;
        private Button inventoryButton;

        private CharacterData data;

        // 인벤토리 열기 요청 이벤트 (캐릭터 데이터 전달)
        public event Action<CharacterData> OnInventoryOpenRequested;

        public CharacterData CharacterData => data;
        public bool ContainsPoint(Vector2 worldPos) => root != null && root.worldBound.Contains(worldPos);

        public UI_CharacterUnit(VisualElement element, CharacterData characterData)
        {
            root = element;
            data = characterData;

            // 계층 구조에서 요소 참조 찾기
            portrait = root.Q<VisualElement>("Portrait");
            hpFill = root.Q<VisualElement>("HPFill");
            mpFill = root.Q<VisualElement>("MPFill");
            hpText = root.Q<Label>("HPText");
            mpText = root.Q<Label>("MPText");
            inventoryButton = root.Q<Button>("InventoryButton");

            // 버튼 클릭 이벤트 연결
            inventoryButton.clicked += () => OnInventoryOpenRequested?.Invoke(data);

            // 초기 데이터 반영
            UpdateUI();
        }

        /// <summary>
        /// 캐릭터 데이터를 기반으로 UI 요소를 갱신합니다.
        /// </summary>
        public void UpdateUI()
        {
            if (data == null) return;

            // 초상화 설정
            if (data.portrait != null)
            {
                portrait.style.backgroundImage = new StyleBackground(data.portrait);
            }

            // HP 바 갱신 (비율 계산)
            float hpRatio = (float)data.currentHP / data.maxHP;
            hpFill.style.width = Length.Percent(hpRatio * 100);
            hpText.text = $"{data.currentHP}/{data.maxHP}";

            // MP 바 갱신 (비율 계산)
            float mpRatio = (float)data.currentMP / data.maxMP;
            mpFill.style.width = Length.Percent(mpRatio * 100);
            mpText.text = $"{data.currentMP}/{data.maxMP}";
        }
    }
}
