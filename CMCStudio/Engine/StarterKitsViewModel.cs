using System.Collections.Generic;
using System.Linq;
using CMCStudio.Data;
using static CMCStudio.Models.StarterKitstListViewModel;

namespace CMCStudio.Models
{
    public class StarterKitsViewModel
    {

        public class MonsterSelection
        {
            public ushort Id { get; set; }
            public string Name { get; set; }
        }

        public static List<MonsterSelection> MonsterList { get; } = MonstersListViewModel.MonsterNames
            .Select((name, index) => new MonsterSelection { Id = (ushort)index, Name = string.Format("{0:D3} {1}", index, name) })
            .ToList();


        public static List<StarterKitDisplayItem> LastLoaded { get; private set; }
        private static List<StarterKitDisplayItem> _original; // first loaded immutable snapshot
        public static IReadOnlyList<StarterKitDisplayItem> OriginalKits => _original;

        public void Load(string isoPath, StarterKitsRepository repository)
        {
            if (string.IsNullOrWhiteSpace(isoPath)) return;
            var kitDictionary = repository.ReadAll(isoPath);
            var list = kitDictionary.Values.ToList();

            _original = new List<StarterKitDisplayItem>(list.Select(k => k.Clone()).ToList());
            LastLoaded = new List<StarterKitDisplayItem>(list.Select(k => k.Clone()).ToList());
        }
    }
}