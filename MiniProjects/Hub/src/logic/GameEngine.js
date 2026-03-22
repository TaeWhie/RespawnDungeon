/**
 * GameEngine.js
 * Manages the state and logic for the GuildMaster Mini-Game.
 */

export const INITIAL_GAME_STATE = {
  currentBuddy: 'rina',
  messages: [
    { id: 1, sender: 'rina', text: '길드장님, 오늘 원정 준비는 다 되셨나요?', type: 'npc' }
  ],
  characters: {}, // Loaded from JSON
  inventory: [],
  logs: []
};

export const NPC_RESPONSES = {
  rina: [
    "어머, 정말인가요? 믿음직스럽네요!",
    "치유의 기도가 필요하시면 언제든 말씀해 주세요.",
    "오늘따라 길드 식당의 스프가 아주 따뜻하네요.",
    "휴식이 필요한 시점인 것 같아요. 잠시 쉬어가는 건 어떨까요?"
  ],
  kyle: [
    "전열은 내가 맡겠다. 걱정 말게.",
    "젠장... 이번 임무는 만만하지 않겠군.",
    "단호한 결단이 필요한 때다.",
    "훈련은 실전처럼 해야 하는 법이지."
  ],
  bram: [
    "흠, 이 물건... 꽤나 값이 나가겠는데?",
    "거래는 공평해야지, 안 그래?",
    "나쁘지 않은 제안이군. 하지만 내 몫은 챙겨줘야겠어.",
    "정보야말로 가장 비싼 상품이지."
  ]
};

export function getNPCResponse(npcId, userMessage) {
  const responses = NPC_RESPONSES[npcId] || ["..."];
  return responses[Math.floor(Math.random() * responses.length)];
}
