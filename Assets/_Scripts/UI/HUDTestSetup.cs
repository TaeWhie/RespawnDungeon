using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaeWhie.RPG.UI
{
    /// <summary>
    /// HUD 시스템을 테스트하기 위한 초기 설정 스크립트입니다.
    /// </summary>
    public class HUDTestSetup : MonoBehaviour
    {
        [SerializeField] private HUDManager hudManager;
        
        // 테스트용 데이터 (인스펙터에서 할당하거나 코드로 생성 가능)
        public List<CharacterData> testMembers;

        private void Start()
        {
            if (hudManager == null) hudManager = GetComponent<HUDManager>();
            
            // 만약 인스펙터에 데이터가 없다면 기본 샘플 데이터 생성 시도
            if (testMembers == null || testMembers.Count == 0)
            {
                Debug.LogWarning("테스트용 CharacterData가 비어 있습니다. 인스펙터에서 CharacterData를 생성하여 할당해주세요.");
            }
        }

        [ContextMenu("Refresh HUD")]
        public void TestRefresh()
        {
            hudManager.InitializeHUD();
        }
    }
}
