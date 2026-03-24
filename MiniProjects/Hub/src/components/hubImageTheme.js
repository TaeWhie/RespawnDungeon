export const HUB_IMAGE_THEME_ID = 'guildhub-2d-v2-quality';

const BASE_STYLE =
  'cohesive fantasy guild art direction, painterly 2D illustration, soft rim light, dramatic but warm mood, clean composition, no text, no watermark';

function clean(v) {
  return String(v || '').trim();
}

function pickWorldStyleTone(worldSummary, dungeonSystem) {
  const t = `${clean(worldSummary)} ${clean(dungeonSystem)}`.toLowerCase();
  const dark = ['abyss', 'dark', 'hostile', 'oblivion', '심연', '어둠', '잔혹', '붕괴'].some((k) => t.includes(k));
  if (dark) {
    return 'moody lighting, high contrast, deep sapphire blues and purples, hostile crimson accents';
  }
  return 'golden hour sunrise, soft cinematic light, warm greens and golds, hopeful atmosphere';
}

function firstName(list, fallback) {
  if (!Array.isArray(list) || list.length === 0) return fallback;
  const first = list[0];
  return clean(first?.name ?? first?.Name) || fallback;
}

function abyssDungeonName(dungeons) {
  if (!Array.isArray(dungeons) || dungeons.length === 0) return '';
  const abyss = dungeons.find((d) => String(d?.difficulty ?? d?.Difficulty ?? '').toLowerCase().includes('abyss'));
  return clean(abyss?.name ?? abyss?.Name);
}

export function buildWorldPrompt({
  worldName,
  worldSummary,
  guildInfo,
  dungeonSystem,
  baseCamp,
  currencyAndLoot,
  locations,
  dungeons,
  lore,
}) {
  const world = clean(worldName) || 'fantasy world';
  const summary = clean(worldSummary) || 'epic fantasy setting';
  const guild = clean(guildInfo) || 'adventurer guild';
  const camp = clean(baseCamp) || 'frontier base camp';
  const system = clean(dungeonSystem) || 'living dungeon ecosystem';
  const loot = clean(currencyAndLoot) || 'arcane particles and relic fragments';
  const city = firstName(locations, 'capital city');
  const danger = abyssDungeonName(dungeons) || firstName(dungeons, 'colossal abyss dungeon');
  const loreHint = Array.isArray(lore) && lore.length > 0 ? clean(lore[0]) : 'mysterious magical sky phenomena';
  const tone = pickWorldStyleTone(summary, system);

  return `${BASE_STYLE}. Cinematic concept art, masterpiece, high detail, panoramic world-intro key visual. Composition: a single small traveler seen from behind on a high rocky cliff, emphasizing scale of the world. World: ${world}. Theme: ${summary}. Near foreground (safe start): warm lights of ${camp}, linked to guild identity ${guild}. Midground (goal): major settlement or landmark ${city}. Far background (danger): imposing distant dungeon ${danger}, with dramatic threatening silhouette. Atmosphere lore: ${loreHint}. World rules hint: ${system}. Floating magical particles inspired by economy and loot: ${loot}. Color and lighting direction: ${tone}. No text, no watermark, no logo.`;
}

export function buildCharacterPortraitPrompt({
  name,
  role,
  gender,
  age,
  mood,
  background,
  recentEvent,
  topTraits,
  skills,
  equipment,
  stats,
}) {
  const n = clean(name) || 'adventurer';
  const r = clean(role) || 'guild member';
  const g = clean(gender) || 'unspecified';
  const a = clean(age) || 'unknown age';
  const m = clean(mood) || 'focused';
  const b = clean(background) || 'fantasy guild setting';
  const recent = clean(recentEvent) || 'no recent event';
  const traits = clean(topTraits) || 'balanced personality';
  const skillHints = clean(skills) || 'role-consistent abilities';
  const equipHints = clean(equipment) || 'practical fantasy gear';
  const statHints = clean(stats) || 'balanced combat capability';
  return `${BASE_STYLE}. Character profile portrait formula: [style + identity + expression/performance + equipment details + story staging]. (professional fantasy RPG character illustration:1.25), (high detail face and costume:1.2), bust shot. Identity: Name ${n}, Role ${r}, Gender ${g}, Age ${a}. Expression and performance: Mood ${m}, Traits ${traits}, Combat profile ${statHints}, Skills ${skillHints}. Equipment details: ${equipHints}. Story staging: narrative motif ${recent}, background lore ${b}. Keep anatomy clean, readable silhouette, cinematic lighting. Avoid text, logo, watermark, collage, distorted anatomy.`;
}

export function buildDungeonLogPrompt({ summary, dungeonName, eventType }) {
  const dungeon = clean(dungeonName) || 'ancient dungeon';
  const event = clean(eventType) || 'encounter';
  const s = clean(summary) || 'party faces danger in a dark corridor';
  return `${BASE_STYLE}. Dungeon run scene illustration. Dungeon: ${dungeon}. Event type: ${event}. Log summary: ${s}.`;
}
