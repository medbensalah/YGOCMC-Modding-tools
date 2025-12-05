using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using YGO_CMC_Modding_tool.Services;

namespace YGO_CMC_Modding_tool.ViewModels
{
    public class ShopItem
    {
        public ushort Value { get; set; }
    }

    public class Shop
    {
        public string Name { get; set; }
        public ObservableCollection<ShopItem> CardIDs { get; set; } = new ObservableCollection<ShopItem>();

        public Shop Clone()
        {
            var clone = (Shop)this.MemberwiseClone();
            clone.CardIDs = new ObservableCollection<ShopItem>(this.CardIDs.Select(c => new ShopItem { Value = c.Value }));
            return clone;
        }
    }

    public class ShopsViewModel
    {
        public class MonsterSelection
        {
            public ushort Id { get; set; }
            public string Name { get; set; }
            public string Display => $"{Id:D3} {Name}";
        }

        public static List<MonsterSelection> MonsterList { get; } = MonstersListViewModel.MonsterNames
            .Select((name, index) => new MonsterSelection { Id = (ushort)index, Name = name })
            .ToList();

        public static List<Shop> LastLoaded { get; private set; }
        public static List<Shop> AttributeShops { get; private set; }
        public static List<Shop> AreaShops { get; private set; }

        private static List<Shop> _original;
        public static IReadOnlyList<Shop> OriginalShops => _original;

        public void Load(string isoPath, ShopsRepoitory repository)
        {
            if (string.IsNullOrWhiteSpace(isoPath)) return;

            var shopsDictionary = repository.ReadAll(isoPath);
            var list = shopsDictionary.Select(kvp => new Shop
            {
                Name = kvp.Key,
                CardIDs = new ObservableCollection<ShopItem>(kvp.Value.Select(id => new ShopItem { Value = id }))
            }).ToList();

            if (_original == null)
            {
                _original = new List<Shop>(list.Select(s => s.Clone()));
            }
            LastLoaded = new List<Shop>(list.Select(s => s.Clone()));

            // Split the loaded shops into two separate lists for the UI
            AttributeShops = LastLoaded.Take(8).ToList();
            AreaShops = LastLoaded.Skip(8).ToList();
        }
    }
}