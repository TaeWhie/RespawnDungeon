using UnityEngine;

namespace TaeWhie.RPG.UI
{
    /// <summary>
    /// 캐릭터의 상태 정보를 담고 있는 데이터 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "RPG/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("기본 정보")]
        public string characterName;      // 캐릭터 이름
        public Sprite portrait;           // 캐릭터 초상화

        [Header("상태 수치")]
        public int currentHP = 100;
        public int maxHP = 100;
        public int currentMP = 50;
        public int maxMP = 50;

        [Header("기본 스탯")]
        public int atk = 10;
        public int def = 5;
        public int str = 8;
        public int dex = 12;
        public int @int = 6;

        [Header("투자 기능")]
        public int statPoints = 5; // 남은 스탯 포인트
    }
}
