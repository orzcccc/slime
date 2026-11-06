using System;
using System.Security.Cryptography;

namespace SlimeBlindBox.Backend.Local
{
    /// <summary>玩家存档的读写。读取不到存档时返回 null；读写失败抛出异常。</summary>
    public interface IPlayerDataStore
    {
        PlayerSaveData Load();
        void Save(PlayerSaveData data);
    }

    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public interface IRandomSource
    {
        /// <summary>返回 [0, maxExclusive) 内的均匀随机整数。</summary>
        int Next(int maxExclusive);
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>基于加密随机数的均匀整数源，使用拒绝采样避免取模偏差。</summary>
    public sealed class CryptoRandomSource : IRandomSource, IDisposable
    {
        private readonly RandomNumberGenerator generator = RandomNumberGenerator.Create();
        private readonly byte[] buffer = new byte[4];

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var range = (uint)maxExclusive;
            var limit = uint.MaxValue - uint.MaxValue % range;
            uint value;
            do
            {
                generator.GetBytes(buffer);
                value = BitConverter.ToUInt32(buffer, 0);
            } while (value >= limit);
            return (int)(value % range);
        }

        public void Dispose() => generator.Dispose();
    }

    /// <summary>内存存档，用于测试和不需要持久化的场景。</summary>
    public sealed class InMemoryPlayerDataStore : IPlayerDataStore
    {
        private PlayerSaveData stored;

        public int SaveCount { get; private set; }

        public PlayerSaveData Load() => stored?.Clone();

        public void Save(PlayerSaveData data)
        {
            stored = data.Clone();
            SaveCount++;
        }
    }
}
