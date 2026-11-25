using System.Collections.Generic;
using YutArena.Managers;

namespace YutArena.InGame
{
    /// <summary>실제 말 묶음의 소지품. 분신 수와 플레이어의 방패 효과와는 별개입니다.</summary>
    public sealed class KtkItemInventory
    {
        private readonly List<KtkSword> swords = new List<KtkSword>();
        private readonly List<KtkShield> shields = new List<KtkShield>();
        public IReadOnlyList<KtkSword> Swords => swords;
        public IReadOnlyList<KtkShield> Shields => shields;
        public int Count => swords.Count + shields.Count;

        public void Add(KtkSword sword) => swords.Add(sword);
        public void Add(KtkShield shield) => shields.Add(shield);

        public KtkItemInventory Copy()
        {
            var copy = new KtkItemInventory();
            copy.swords.AddRange(swords);
            copy.shields.AddRange(shields);
            return copy;
        }

        public int TransferTo(KtkItemInventory target)
        {
            if (ReferenceEquals(this, target)) return 0;
            int count = Count;
            target.swords.AddRange(swords);
            target.shields.AddRange(shields);
            Clear();
            return count;
        }

        public void ConvertSwordsToDemon()
        {
            for (int i = 0; i < swords.Count; i++) swords[i] = KtkSword.Demon;
        }

        public void Clear()
        {
            swords.Clear();
            shields.Clear();
        }
    }
}
