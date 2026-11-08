using System;
using System.Collections.Generic;

namespace SlimeBlindBox.Backend
{
    public sealed class SlimeRarityDefinition
    {
        public SlimeRarityDefinition(string key, string name, string color, int weight, int rank)
        {
            Key = key;
            Name = name;
            Color = color;
            Weight = weight;
            Rank = rank;
        }

        public string Key { get; }
        public string Name { get; }
        public string Color { get; }
        /// <summary>该品质在百万分制下的基础掉落权重。</summary>
        public int Weight { get; }
        /// <summary>品质阶级，劣质 0 到魔神 11。</summary>
        public int Rank { get; }
    }

    public sealed class BoxRarityDefinition
    {
        public BoxRarityDefinition(string key, string name, string color, int weight, double boost)
        {
            Key = key;
            Name = name;
            Color = color;
            Weight = weight;
            Boost = boost;
        }

        public string Key { get; }
        public string Name { get; }
        public string Color { get; }
        /// <summary>领取时的品质权重，总和为 10000。</summary>
        public int Weight { get; }
        /// <summary>开箱时对高品质史莱姆的加成系数。</summary>
        public double Boost { get; }
    }

    public sealed class SlimeDefinition
    {
        public SlimeDefinition(int id, string name, string rarityKey, int baseCp, int dropWeight, string description)
        {
            Id = id;
            Name = name;
            RarityKey = rarityKey;
            BaseCp = baseCp;
            DropWeight = dropWeight;
            Description = description;
        }

        public int Id { get; }
        public string Name { get; }
        public string RarityKey { get; }
        public int BaseCp { get; }
        /// <summary>具体条目的掉落权重。同品质多条目时需细分该品质的总权重。</summary>
        public int DropWeight { get; }
        /// <summary>图鉴描述文案，最多两行。接入 PlayFab 后随 Catalog 下发并本地化。</summary>
        public string Description { get; }
    }

    /// <summary>
    /// 玩法数值配置。规则详见 Docs/Systems/品质与抽取系统.md 与 盲盒产出与仓库系统.md。
    /// 接入 PlayFab 后，这些数据迁移到 Title Data / Catalog，由云端下发。
    /// </summary>
    public static class GameBalanceConfig
    {
        public static readonly TimeSpan BoxGenerationInterval = TimeSpan.FromHours(3);
        public const int DefaultBoxCapacity = 8;
        public const int InitialPendingBoxCount = 1;
        public const int SlimeWeightTotal = 1000000;
        public const int BoxWeightTotal = 10000;
        /// <summary>开箱加成以“优秀”（阶级 2）为分界。</summary>
        public const int BoostPivotRank = 2;
        public const int LevelExperienceBase = 20;
        public const int LevelExperienceGrowth = 10;
        public const double LevelCpRate = 0.25;
        public const int DebugAddMinimum = 1;
        public const int DebugAddMaximum = 100;
        public const int DebugAdvanceMinimumHours = 1;
        public const int DebugAdvanceMaximumHours = 168;

        public static readonly IReadOnlyList<SlimeRarityDefinition> SlimeRarities = new[]
        {
            new SlimeRarityDefinition("POOR", "劣质", "#9A9A9A", 350000, 0),
            new SlimeRarityDefinition("COMMON", "普通", "#FFFFFF", 300000, 1),
            new SlimeRarityDefinition("UNCOMMON", "优秀", "#55D66B", 180000, 2),
            new SlimeRarityDefinition("RARE", "精良", "#4E9DFF", 100000, 3),
            new SlimeRarityDefinition("EPIC", "极品", "#A970FF", 50000, 4),
            new SlimeRarityDefinition("MYTHIC", "史诗", "#FF9D3D", 15000, 5),
            new SlimeRarityDefinition("LEGENDARY", "传说", "#FF4B4B", 4000, 6),
            new SlimeRarityDefinition("ARTIFACT", "神器", "#FF78C8", 800, 7),
            new SlimeRarityDefinition("DEMON_KING", "魔王", "#8F1838", 150, 8),
            new SlimeRarityDefinition("WAR_GOD", "战神", "#D8A52D", 40, 9),
            new SlimeRarityDefinition("SUPREME", "至尊", "#61E8FF", 9, 10),
            new SlimeRarityDefinition("DEMON_GOD", "魔神", "#FF5CE1", 1, 11),
        };

        public static readonly IReadOnlyList<BoxRarityDefinition> BoxRarities = new[]
        {
            new BoxRarityDefinition("WHITE", "白色", "#FFFFFF", 4500, 0.00),
            new BoxRarityDefinition("GREEN", "绿色", "#55D66B", 3000, 0.20),
            new BoxRarityDefinition("BLUE", "蓝色", "#4E9DFF", 1500, 0.45),
            new BoxRarityDefinition("PURPLE", "紫色", "#A970FF", 800, 0.80),
            new BoxRarityDefinition("RED", "红色", "#FF4B4B", 200, 1.25),
        };

        public static readonly IReadOnlyList<SlimeDefinition> Slimes = new[]
        {
            new SlimeDefinition(201, "干瘪史莱姆", "POOR", 5, 350000, "水分不太够的史莱姆，晒太阳会更瘪。"),
            new SlimeDefinition(202, "基础史莱姆", "COMMON", 10, 300000, "最常见的史莱姆，软软的，弹弹的。"),
            new SlimeDefinition(203, "嫩芽史莱姆", "UNCOMMON", 20, 180000, "头顶冒出一片小嫩芽，喜欢待在草地上。"),
            new SlimeDefinition(204, "潮汐史莱姆", "RARE", 40, 100000, "身体随着潮水起伏，摸起来凉凉的。"),
            new SlimeDefinition(205, "紫晶史莱姆", "EPIC", 90, 50000, "体内藏着紫色晶簇，夜里会微微发光。"),
            new SlimeDefinition(206, "烈阳史莱姆", "MYTHIC", 180, 15000, "吸收了正午的阳光，靠近会觉得暖暖的。"),
            new SlimeDefinition(207, "赤龙史莱姆", "LEGENDARY", 400, 4000, "据说有一丝龙族血脉，打喷嚏会冒小火星。"),
            new SlimeDefinition(208, "星辉神器史莱姆", "ARTIFACT", 900, 800, "由星光凝成的史莱姆，周围总有细碎的闪光。"),
            new SlimeDefinition(209, "深渊魔王史莱姆", "DEMON_KING", 1800, 150, "来自深渊的小魔王，其实很怕黑。"),
            new SlimeDefinition(210, "炎狱战神史莱姆", "WAR_GOD", 3500, 40, "戴着战盔的勇士，每天坚持晨练弹跳。"),
            new SlimeDefinition(211, "万象至尊史莱姆", "SUPREME", 7000, 9, "晶莹剔透的至尊，王冠上镶着整片星空。"),
            new SlimeDefinition(212, "混沌魔神史莱姆", "DEMON_GOD", 15000, 1, "体内流转着宇宙的颜色，传说只在梦里见过。"),
        };

        public static SlimeRarityDefinition FindSlimeRarity(string key)
        {
            foreach (var rarity in SlimeRarities) if (rarity.Key == key) return rarity;
            return null;
        }

        public static BoxRarityDefinition FindBoxRarity(string key)
        {
            foreach (var box in BoxRarities) if (box.Key == key) return box;
            return null;
        }

        public static SlimeDefinition FindSlime(int id)
        {
            foreach (var slime in Slimes) if (slime.Id == id) return slime;
            return null;
        }

        /// <summary>稀有盲盒的史莱姆调整权重：max(1, round(基础权重 × (1 + 加成)^(阶级 - 2)))。</summary>
        public static int AdjustedSlimeWeight(int baseWeight, string slimeRarityKey, string boxRarityKey)
        {
            var box = FindBoxRarity(boxRarityKey);
            if (box == null) return 0;
            var rarity = FindSlimeRarity(slimeRarityKey);
            var rank = rarity?.Rank ?? 0;
            var adjusted = baseWeight * Math.Pow(1 + box.Boost, rank - BoostPivotRank);
            return Math.Max(1, (int)Math.Floor(adjusted + 0.5));
        }

        public static int ExperienceForLevel(int level) => LevelExperienceBase + (level - 1) * LevelExperienceGrowth;

        /// <summary>指定盲盒品质下每个史莱姆的调整后掉落权重，顺序与 <see cref="Slimes"/> 一致。开箱抽取与概率公示共用。</summary>
        public static int[] SlimeWeightsForBox(string boxRarityKey, out long total)
        {
            var weights = new int[Slimes.Count];
            total = 0;
            for (var index = 0; index < Slimes.Count; index++)
            {
                var slime = Slimes[index];
                weights[index] = slime.DropWeight > 0 ? AdjustedSlimeWeight(slime.DropWeight, slime.RarityKey, boxRarityKey) : 0;
                total += weights[index];
            }
            return weights;
        }

        /// <summary>用指定品质的盲盒开出某只史莱姆的概率（0–1）。</summary>
        public static double SlimeProbability(int slimeId, string boxRarityKey)
        {
            var weights = SlimeWeightsForBox(boxRarityKey, out var total);
            if (total <= 0) return 0;
            for (var index = 0; index < Slimes.Count; index++)
                if (Slimes[index].Id == slimeId) return weights[index] / (double)total;
            return 0;
        }

        /// <summary>用指定品质的盲盒开出各史莱姆品质的概率（0–1），顺序与 <see cref="SlimeRarities"/> 一致，用于概率公示。</summary>
        public static double[] RarityProbabilities(string boxRarityKey)
        {
            var weights = SlimeWeightsForBox(boxRarityKey, out var total);
            var result = new double[SlimeRarities.Count];
            if (total <= 0) return result;
            for (var index = 0; index < Slimes.Count; index++)
                for (var rank = 0; rank < SlimeRarities.Count; rank++)
                    if (SlimeRarities[rank].Key == Slimes[index].RarityKey) result[rank] += weights[index] / (double)total;
            return result;
        }

        /// <summary>等级对 CP 的加成倍率：1 + log2(等级) × 0.25。</summary>
        public static double LevelCpMultiplier(int level) => 1 + Math.Log(Math.Max(1, level), 2) * LevelCpRate;

        /// <summary>单只有效 CP = floor(基础 CP × 等级加成倍率)。</summary>
        public static int UnitEffectiveCp(int baseCp, int level) => (int)Math.Floor(baseCp * LevelCpMultiplier(level));

        /// <summary>有效 CP = 单只有效 CP × 数量。</summary>
        public static int EffectiveCp(int baseCp, int level, int quantity) => UnitEffectiveCp(baseCp, level) * quantity;

        /// <summary>校验配置完整性，返回全部问题；空列表表示配置有效。</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            var rarityTotal = 0;
            var rarityKeys = new HashSet<string>();
            foreach (var rarity in SlimeRarities)
            {
                rarityTotal += rarity.Weight;
                if (!rarityKeys.Add(rarity.Key)) problems.Add("品质键重复：" + rarity.Key);
                if (rarity.Weight < 0) problems.Add("品质权重为负：" + rarity.Key);
            }
            if (rarityTotal != SlimeWeightTotal) problems.Add("史莱姆品质权重总和应为 " + SlimeWeightTotal + "，当前为 " + rarityTotal);

            var boxTotal = 0;
            foreach (var box in BoxRarities) boxTotal += box.Weight;
            if (boxTotal != BoxWeightTotal) problems.Add("盲盒品质权重总和应为 " + BoxWeightTotal + "，当前为 " + boxTotal);

            var slimeIds = new HashSet<int>();
            var weightByRarity = new Dictionary<string, int>();
            foreach (var slime in Slimes)
            {
                if (!slimeIds.Add(slime.Id)) problems.Add("史莱姆 ID 重复：" + slime.Id);
                if (FindSlimeRarity(slime.RarityKey) == null) problems.Add("史莱姆 " + slime.Id + " 的品质不存在：" + slime.RarityKey);
                if (slime.DropWeight < 0) problems.Add("史莱姆 " + slime.Id + " 的掉落权重为负");
                weightByRarity.TryGetValue(slime.RarityKey, out var sum);
                weightByRarity[slime.RarityKey] = sum + slime.DropWeight;
            }
            foreach (var rarity in SlimeRarities)
            {
                weightByRarity.TryGetValue(rarity.Key, out var sum);
                if (sum != rarity.Weight) problems.Add("品质 " + rarity.Key + " 下史莱姆权重之和应为 " + rarity.Weight + "，当前为 " + sum);
            }
            return problems;
        }
    }
}
