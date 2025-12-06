using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using YGO_CMC_Modding_tool.Services;

namespace YGO_CMC_Modding_tool.ViewModels
{
    public class AttributeBuffsViewModel
    {

        [Flags]
        public enum StatBuffs : byte
        {
            None = 0,
            AtUp = 1 << 0,   // 0x01
            AtDown = 1 << 1,   // 0x02
            DfUp = 1 << 2,   // 0x04
            DfDown = 1 << 3,   // 0x08
            PpUp = 1 << 6,   // 0x40
            PpDown = 1 << 7    // 0x80
        }
        [Flags]
        public enum SpaBuffs : byte
        {
            None = 0,
            SpaUp = 1 << 0,   // 0x01
            SpaDown = 1 << 1    // 0x02
        }

        public class BuffInfo
        {
            public ushort RawValue => (ushort)((byte)Special << 8 | (byte)Stats);
            public StatBuffs Stats;
            public SpaBuffs Special;

            public override string ToString()
                => $"Stats: {Stats}, Special: {Special}";

            public BuffInfo Clone()
            {
                return new BuffInfo
                {
                    Stats = this.Stats,
                    Special = this.Special
                };
            }

            public static BuffInfo ParseBuffs(ushort value)
            {
                byte physicalByte = (byte)(value & 0xFF);
                byte specialByte = (byte)((value >> 8) & 0xFF);

                return new BuffInfo
                {
                    Stats = (StatBuffs)physicalByte,
                    Special = (SpaBuffs)specialByte
                };
            }
        }

        public static List<BuffInfo> LastLoaded { get; private set; }
        private static List<BuffInfo> _original;
        public static IReadOnlyList<BuffInfo> OriginalShops => _original;

        public void Load(string isoPath, AttributeBuffsRepository repository)
        {
            if (string.IsNullOrWhiteSpace(isoPath)) return;

            var attributeBuffs = repository.ReadAll(isoPath);
            var list = attributeBuffs.Select(ab => BuffInfo.ParseBuffs(ab)).ToList();
            if (_original == null)
            {
                _original = list.Select(s => s.Clone()).ToList();
            }
            LastLoaded = list.Select(s => s.Clone()).ToList();
        }
    }
}