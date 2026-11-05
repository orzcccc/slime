const boxRarities = Object.freeze([
  { key: 'WHITE', name: '白色', color: '#FFFFFF', weight: 4500, boost: 0.00 },
  { key: 'GREEN', name: '绿色', color: '#55D66B', weight: 3000, boost: 0.20 },
  { key: 'BLUE', name: '蓝色', color: '#4E9DFF', weight: 1500, boost: 0.45 },
  { key: 'PURPLE', name: '紫色', color: '#A970FF', weight: 800, boost: 0.80 },
  { key: 'RED', name: '红色', color: '#FF4B4B', weight: 200, boost: 1.25 },
]);

const slimeRarityRank = Object.freeze({
  POOR: 0, COMMON: 1, UNCOMMON: 2, RARE: 3, EPIC: 4, MYTHIC: 5,
  LEGENDARY: 6, ARTIFACT: 7, DEMON_KING: 8, WAR_GOD: 9, SUPREME: 10, DEMON_GOD: 11,
});

function adjustedSlimeWeight(baseWeight, slimeRarityKey, boxRarityKey) {
  const box = boxRarities.find((item) => item.key === boxRarityKey);
  if (!box) return 0;
  const rank = slimeRarityRank[slimeRarityKey] ?? 0;
  // 以优秀为分界：稀有盲盒温和压低低品质，并按阶级指数提高高品质权重。
  const exponent = rank - 2;
  return Math.max(1, Math.round(baseWeight * Math.pow(1 + box.boost, exponent)));
}

module.exports = { boxRarities, slimeRarityRank, adjustedSlimeWeight };
