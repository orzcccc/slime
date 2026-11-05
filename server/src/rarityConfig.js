const rarities = Object.freeze([
  { key: 'POOR', name: '劣质', weight: 350000, color: '#9A9A9A' },
  { key: 'COMMON', name: '普通', weight: 300000, color: '#FFFFFF' },
  { key: 'UNCOMMON', name: '优秀', weight: 180000, color: '#55D66B' },
  { key: 'RARE', name: '精良', weight: 100000, color: '#4E9DFF' },
  { key: 'EPIC', name: '极品', weight: 50000, color: '#A970FF' },
  { key: 'MYTHIC', name: '史诗', weight: 15000, color: '#FF9D3D' },
  { key: 'LEGENDARY', name: '传说', weight: 4000, color: '#FF4B4B' },
  { key: 'ARTIFACT', name: '神器', weight: 800, color: '#FF78C8' },
  { key: 'DEMON_KING', name: '魔王', weight: 150, color: '#8F1838' },
  { key: 'WAR_GOD', name: '战神', weight: 40, color: '#D8A52D' },
  { key: 'SUPREME', name: '至尊', weight: 9, color: '#61E8FF' },
  { key: 'DEMON_GOD', name: '魔神', weight: 1, color: '#FF5CE1' },
]);

const totalWeight = rarities.reduce((sum, rarity) => sum + rarity.weight, 0);
if (totalWeight !== 1_000_000) throw new Error(`品质权重总和必须为 1000000，当前为 ${totalWeight}`);

module.exports = { rarities, totalWeight };
