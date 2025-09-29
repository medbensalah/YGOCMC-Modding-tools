using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static YGO_CMC_Editor.MainWindow;

namespace YGO_CMC_Editor
{



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<string, byte> _attributeMap = new Dictionary<string, byte>()
        {
            { "LIGHT",      0 },
            { "DARK",       1 },
            { "FIRE",       2 },
            { "WATER",      3 },
            { "EARTH",      4 },
            { "WIND",       5 },
            { "WOOD",       6 },
            { "THUNDER",    7 },
        };


        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseIsoFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "ISO files (*.iso)|*.iso"; // Only allow .iso files
            openFileDialog.Title = "Select an ISO file";

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                // You can now use `selectedFilePath` as needed\
                IsoName.Text = selectedFilePath;
            }
        }

        #region Monsters Patch
        public struct Monster
        {
            public byte Type;
            public byte Attribute;
            public byte MovementPattern;
            public byte AttackPattern;
            public UInt16 PP;
            public UInt16 AT;
            public UInt16 DF;
            public UInt16 AP;
            public UInt16 pAP;
            public UInt16 MP;
            public UInt16 _0;
            public byte AESymbol;
            public byte AELand;
            public byte AEMap;
            public UInt16 _1;
            public UInt32 _2;
            public byte _3;

            public Monster(byte type, byte attribute, byte movementPattern, byte attackPattern, ushort pP, ushort aT, ushort dF, ushort aP, ushort pAP, ushort mP, byte aESymbol, byte aELand, byte aEMap) : this()
            {
                Type = type;
                Attribute = attribute;
                MovementPattern = movementPattern;
                AttackPattern = attackPattern;
                PP = pP;
                AT = aT;
                DF = dF;
                AP = aP;
                this.pAP = pAP;
                MP = mP;
                AESymbol = aESymbol;
                AELand = aELand;
                AEMap = aEMap;
            }
        }

        private Dictionary<string, byte> _typeMap = new Dictionary<string, byte>()
        {
            { "Symbol",             0 },
            { "Fairy",              1 },
            { "Fiend",              2 },
            { "Zombie",             3 },
            { "Spellcaster",        4 },
            { "Warrior",            5 },
            { "Beast-Warrior",      6 },
            { "Dragon",             7 },
            { "Sea Serpent",        8 },
            { "Dinosaur",           9 },
            { "Machine",           10 },
            { "Rock",              11 },
            { "Reptile",           12 },
            { "Beast",             13 },
            { "Winged Beast",      14 },
            { "Fish",              15 },
            { "Insect",            16 },
            { "Plant",             17 },
            { "Thunder",           18 },
            { "Aqua",              19 },
            { "Pyro",              20 },
        };

        private Dictionary<string, byte> _patternMap = new Dictionary<string, byte>()
        {
            {"p1",         0xE0 },
            {"p2",         0xE1 },
            {"p3",         0xE2 },
            {"p4",         0xE3 },
            {"s1+p3",      0xE4 },
            {"s2+x3",      0xE9 },
            {"s2",         0xED },
            {"d2",         0xEF },
            {"d3",         0xF0 },
            {"o2",         0xF1 },
            {"o3",         0xF2 },
            {"p3+x3",      0xF3 },
            {"x1",         0xE5 },
            {"x2",         0xE6 },
            {"x3",         0xE7 },
            {"x4",         0xE8 },
            {"l2",         0xEA },
            {"s1",         0xEC },
        };


        private Dictionary<long, Monster> _monsterMap = new Dictionary<long, Monster>();
        private List<ushort> _usedmonsters = new List<ushort>();

        private void BrowseMonstersTsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Tsv files (*.tsv)|*.tsv"; // Only allow .tsv files
            openFileDialog.Title = "Select a TSV file";

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                // You can now use `selectedFilePath` as needed
                MonstersTsv.Text = selectedFilePath;
            }
        }

        private void PatchMonsters_Click(object sender, RoutedEventArgs e)
        {
            string isoPath = IsoName.Text;
            string tsvPath = MonstersTsv.Text;

            if (string.IsNullOrWhiteSpace(isoPath) || string.IsNullOrWhiteSpace(tsvPath))
            {
                MessageBox.Show("Please select both an ISO file and a TSV file before patching.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(isoPath))
            {
                MessageBox.Show($"ISO file not found:\n{isoPath}", "File Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!File.Exists(tsvPath))
            {
                MessageBox.Show($"TSV file not found:\n{tsvPath}", "File Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _monsterMap.Clear(); // Always clear it before refilling
                using (StreamReader tsvReader = new StreamReader(tsvPath))
                {
                    string headerLine = tsvReader.ReadLine(); // Skip the header

                    string line;
                    while ((line = tsvReader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');

                        if (parts.Length < 15)
                            continue; // Not enough columns

                        // Clean address string
                        string addressStr = parts[0].Trim();

                        // Remove 0x if present
                        if (addressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            addressStr = addressStr.Substring(2);

                        // Parse as hex
                        if (!long.TryParse(addressStr, System.Globalization.NumberStyles.HexNumber, null, out long address))
                            continue; // Skip if invalid

                        string typeStr = parts[4];
                        string attrStr = parts[3];
                        string moveStr = parts[5];
                        string atkStr = parts[6];

                        // Lookups
                        if (!_typeMap.TryGetValue(typeStr, out byte type))
                            continue;
                        if (!_attributeMap.TryGetValue(attrStr, out byte attribute))
                            continue;
                        if (!_patternMap.TryGetValue(moveStr.ToLower(), out byte movePattern))
                            continue;
                        if (!_patternMap.TryGetValue(atkStr.ToLower(), out byte atkPattern))
                            continue;

                        // Parse numeric fields
                        ushort.TryParse(parts[7], out ushort pp);
                        ushort.TryParse(parts[8], out ushort at);
                        ushort.TryParse(parts[9], out ushort df);
                        ushort.TryParse(parts[10], out ushort ap);
                        ushort.TryParse(parts[11], out ushort pap);
                        ushort.TryParse(parts[12], out ushort mp);

                        byte.TryParse(parts[13], out byte aeSymbol);
                        byte.TryParse(parts[14], out byte aeLand);
                        byte.TryParse(parts[15], out byte aeMap);

                        bool used = parts[17].ToLower() == "true";
                        if (used)
                        {
                            ushort id = ushort.Parse(parts[1]);
                            _usedmonsters.Add(id);
                        }

                        // Create Monster struct
                        Monster monster = new Monster(type, attribute, movePattern, atkPattern, pp, at, df, ap, pap, mp, aeSymbol, aeLand, aeMap);

                        // Add to dictionary
                        _monsterMap[address] = monster;
                    }
                }

                if (_usedmonsters.Count > 200)
                {
                    var result = MessageBox.Show($"An error occurred:\nA maximum of 200 monsters has to be used to prevent problems\nContinue anyway?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (result == MessageBoxResult.No)
                    {
                        MessageBox.Show($"Cancelled", "Error", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                using (FileStream isoStream = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite))
                using (BinaryWriter writer = new BinaryWriter(isoStream))
                {
                    foreach (var kvp in _monsterMap)
                    {
                        long address = kvp.Key;
                        Monster monster = kvp.Value;

                        isoStream.Seek(address, SeekOrigin.Begin);
                        WriteMonster(isoStream, writer, monster);
                    }

                    isoStream.Seek(0x004D829E, SeekOrigin.Begin);
                    foreach (ushort id in _usedmonsters)
                    {
                        writer.Write(id);
                    }
                }



                MessageBox.Show($"Successfully patched {_monsterMap.Count} monsters into the ISO!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);



            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WriteMonster(FileStream isoStream, BinaryWriter writer, Monster monster)
        {
            // Write fields before _0
            writer.Write(monster.Type);
            writer.Write(monster.Attribute);
            writer.Write(monster.MovementPattern);
            writer.Write(monster.AttackPattern);

            writer.Write(monster.PP);
            writer.Write(monster.AT);
            writer.Write(monster.DF);
            writer.Write(monster.AP);
            writer.Write(monster.pAP);
            writer.Write(monster.MP);

            // --- Skip over _0 ---
            isoStream.Seek(2, SeekOrigin.Current); // _0 is a ushort = 2 bytes

            // Write fields after _0
            writer.Write(monster.AESymbol);
            writer.Write(monster.AELand);
            writer.Write(monster.AEMap);

            // --- Skip over _1, _2, _3 ---
            isoStream.Seek(2 + 4 + 1, SeekOrigin.Current); // _1 (2 bytes) + _2 (4 bytes) + _3 (1 byte) = 7 bytes
        }
        #endregion

        #region Opponent Unit Patch
        public class EnemyUnit
        {
            public ushort AP;
            public ushort MP;
            public byte MonsterCap;
            public List<EnemyMonsterEntry> Monsters = new();
        }

        public class EnemyMonsterEntry
        {
            public ushort IDRed;
            public long AddrRed;
            public ushort IDBlue;
            public long AddrBlue;
            public byte LevelRed;
            public long LevelAddrRed;
            public byte LevelBlue;
            public long LevelAddrBlue;
        }
        public struct Symbol
        {
            public byte Type;
            public byte DF;
            public byte _0;
            public UInt16 PP;
            public UInt16 pAP;
            public byte Attribute;
            public byte _1;
            public byte Rate;
        }



        private void BrowseOpponentTsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "TSV files (*.tsv)|*.tsv";
            openFileDialog.Title = "Select Opponent Units TSV";

            if (openFileDialog.ShowDialog() == true)
            {
                OpponentTsv.Text = openFileDialog.FileName;
            }
        }

        private void PatchOpponents_Click(object sender, RoutedEventArgs e)
        {
            string isoPath = IsoName.Text;
            string tsvPath = OpponentTsv.Text;

            if (string.IsNullOrWhiteSpace(isoPath) || string.IsNullOrWhiteSpace(tsvPath))
            {
                MessageBox.Show("Please select both an ISO file and an Opponent TSV file before patching.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(isoPath) || !File.Exists(tsvPath))
            {
                MessageBox.Show("One or both files do not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Prepare a list of opponent data blocks to write
                var opponents = new List<(long mapAddr, byte monsterCap, ushort ap, ushort mp, long symbolAddr, Symbol symbol, List<(ushort idRed, ushort idBlue, byte lvRed, byte lvBlue)> monsters, long baseRedID, long baseBlueID, long baseRedLV, long baseBlueLV)>();

                using StreamReader reader = new StreamReader(tsvPath);

                string? line;
                long mapAddress = 0;
                byte monsterCap = 0;
                ushort ap = 0;
                ushort mp = 0;
                long symbolAddress = 0;
                string symbolAttribute = "";
                ushort baseAP = 0;
                byte rate = 0;
                ushort pp = 0;
                long baseRedID = 0, baseBlueID = 0, baseRedLV = 0, baseBlueLV = 0;
                var monsterList = new List<(ushort, ushort, byte, byte)>();

                const int MONSTER_ID_STRIDE = 0x63;
                const int MONSTER_LV_STRIDE = 0x05;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 1 && !parts[0].StartsWith("Monster") && !parts[0].StartsWith("Map"))
                    {
                        // New opponent block → flush previous one if filled
                        if (mapAddress > 0)
                        {
                            if (_attributeMap.TryGetValue(symbolAttribute, out byte attr))
                            {
                                var sym = new Symbol
                                {
                                    Type = 0,
                                    DF = 30,
                                    _0 = 0,
                                    PP = pp,
                                    pAP = baseAP,
                                    Attribute = attr,
                                    _1 = 0,
                                    Rate = rate
                                };

                                opponents.Add((mapAddress, monsterCap, ap, mp, symbolAddress, sym, new List<(ushort, ushort, byte, byte)>(monsterList), baseRedID, baseBlueID, baseRedLV, baseBlueLV));
                            }
                        }

                        // Reset for next opponent
                        mapAddress = 0;
                        monsterCap = 0;
                        ap = mp = pp = baseAP = 0;
                        rate = 0;
                        symbolAddress = 0;
                        symbolAttribute = "";
                        baseRedID = baseBlueID = baseRedLV = baseBlueLV = 0;
                        monsterList.Clear();
                        continue;
                    }

                    switch (parts[0])
                    {
                        case "Map":
                            TryParseHex(parts[1], out mapAddress);
                            break;
                        case "Monster Cap":
                            byte.TryParse(parts[1], out monsterCap);
                            break;
                        case "AP":
                            ushort.TryParse(parts[1], out ap);
                            break;
                        case "MP":
                            ushort.TryParse(parts[1], out mp);
                            break;
                        case "Address":
                            TryParseHex(parts[1], out symbolAddress);
                            break;
                        case "Symbol":
                            symbolAttribute = parts[1].Trim().ToUpper();
                            break;
                        case "Base AP":
                            ushort.TryParse(parts[1], out baseAP);
                            break;
                        case "Rate":
                            byte.TryParse(parts[1], out rate);
                            break;
                        case "PP":
                            ushort.TryParse(parts[1], out pp);
                            break;
                        case "Monsters":
                            TryParseHex(parts[1], out baseRedID);
                            TryParseHex(parts[2], out baseBlueID);
                            TryParseHex(parts[3], out baseRedLV);
                            TryParseHex(parts[4], out baseBlueLV);
                            break;
                        default:
                            if (parts[0].StartsWith("Monster") && parts.Length >= 5)
                            {
                                ushort.TryParse(parts[1], out ushort idRed);
                                ushort.TryParse(parts[2], out ushort idBlue);
                                byte.TryParse(parts[3], out byte lvRed);
                                byte.TryParse(parts[4], out byte lvBlue);

                                monsterList.Add((idRed, idBlue, lvRed, lvBlue));
                            }
                            break;
                    }
                }

                // Final block flush
                if (mapAddress > 0 && _attributeMap.TryGetValue(symbolAttribute, out byte finalAttr))
                {
                    var sym = new Symbol
                    {
                        Type = 0,
                        DF = 30,
                        _0 = 0,
                        PP = pp,
                        pAP = baseAP,
                        Attribute = finalAttr,
                        _1 = 0,
                        Rate = rate
                    };

                    opponents.Add((mapAddress, monsterCap, ap, mp, symbolAddress, sym, monsterList, baseRedID, baseBlueID, baseRedLV, baseBlueLV));
                }

                // Write phase
                using FileStream isoStream = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite);
                using BinaryWriter writer = new BinaryWriter(isoStream);

                foreach (var opponent in opponents)
                {
                    // Write Monster Cap, AP, MP using relative offsets
                    isoStream.Seek(opponent.mapAddr, SeekOrigin.Begin); writer.Write(opponent.monsterCap);
                    isoStream.Seek(opponent.mapAddr + 9, SeekOrigin.Begin); writer.Write(opponent.ap);
                    isoStream.Seek(opponent.mapAddr + 17, SeekOrigin.Begin); writer.Write(opponent.mp);

                    // Write Symbol
                    isoStream.Seek(opponent.symbolAddr, SeekOrigin.Begin);
                    WriteSymbolLittleEndian(isoStream, writer, opponent.symbol);

                    // Write monsters
                    for (int i = 0; i < opponent.monsters.Count; i++)
                    {
                        var (idRed, idBlue, lvRed, lvBlue) = opponent.monsters[i];

                        isoStream.Seek(opponent.baseRedID + i * MONSTER_ID_STRIDE, SeekOrigin.Begin); writer.Write(idRed);
                        isoStream.Seek(opponent.baseBlueID + i * MONSTER_ID_STRIDE, SeekOrigin.Begin); writer.Write(idBlue);
                        isoStream.Seek(opponent.baseRedLV + i * MONSTER_LV_STRIDE, SeekOrigin.Begin); writer.Write(lvRed);
                        isoStream.Seek(opponent.baseBlueLV + i * MONSTER_LV_STRIDE, SeekOrigin.Begin); writer.Write(lvBlue);
                    }
                }

                MessageBox.Show("All opponents patched successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during opponent patching:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private bool TryParseHex(string hex, out long result)
        {
            hex = hex.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex[2..];
            return long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out result);
        }

        private long HexToLong(string hex)
        {
            TryParseHex(hex, out long result);
            return result;
        }


        private void WriteSymbolLittleEndian(FileStream isoStream, BinaryWriter writer, Symbol symbol)
        {
            writer.Write(symbol.Type);
            writer.Write(symbol.DF);

            // Skip _0 (1 byte)
            isoStream.Seek(1, SeekOrigin.Current);

            writer.Write(symbol.PP);
            writer.Write(symbol.pAP);

            writer.Write(symbol.Attribute);

            // Skip _1 (1 byte)
            isoStream.Seek(1, SeekOrigin.Current);

            writer.Write(symbol.Rate);
        }

        #endregion

        #region Starter Kit Patch

        private void BrowseStarterTsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "TSV files (*.tsv)|*.tsv";
            openFileDialog.Title = "Select Starter Kits TSV";

            if (openFileDialog.ShowDialog() == true)
            {
                StarterTsv.Text = openFileDialog.FileName;
            }
        }

        private void PatchStarterKits_Click(object sender, RoutedEventArgs e)
        {
            string isoPath = IsoName.Text;
            string tsvPath = StarterTsv.Text;

            if (string.IsNullOrWhiteSpace(isoPath) || string.IsNullOrWhiteSpace(tsvPath))
            {
                MessageBox.Show("Please select both an ISO file and a Starter Kits TSV file before patching.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(isoPath) || !File.Exists(tsvPath))
            {
                MessageBox.Show("One or both files do not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Dictionary<long, List<ushort>> addressDataMap = new();

                using StreamReader reader = new StreamReader(tsvPath);

                // Step 1: Read address headers (first non-empty line)
                string? addressLine = null;
                int c = 0;
                while ((addressLine = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(addressLine))
                        c++;

                    if (c == 2)
                        break;
                }

                if (addressLine == null)
                    throw new Exception("Starter TSV has no address header.");

                string[] addressParts = addressLine.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                List<long> addresses = new();

                foreach (string part in addressParts)
                {
                    if (TryParseHex(part.Trim(), out long addr))
                    {
                        addresses.Add(addr);
                        addressDataMap[addr] = new List<ushort>();
                    }
                }

                // Step 2: Fill each address column with vertical values
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] values = line.Split('\t');

                    for (int col = 0; col < values.Length && col < addresses.Count; col++)
                    {
                        string val = values[col].Trim();

                        if (ushort.TryParse(val, out ushort cardId))
                        {
                            addressDataMap[addresses[col]].Add(cardId);
                        }
                    }
                }

                // Step 3: Write all values to ISO
                using FileStream isoStream = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite);
                using BinaryWriter writer = new BinaryWriter(isoStream);

                foreach (var entry in addressDataMap)
                {
                    long baseAddr = entry.Key;
                    List<ushort> cards = entry.Value;

                    for (int i = 0; i < cards.Count; i++)
                    {
                        long writeAddr = baseAddr + (i * 2); // 2 bytes per ushort
                        isoStream.Seek(writeAddr, SeekOrigin.Begin);
                        writer.Write(cards[i]);
                    }
                }

                MessageBox.Show($"Successfully patched {addresses.Count} starter kit columns into the ISO.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during starter kit patching:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Shops

        private void BrowseShopsTsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "TSV files (*.tsv)|*.tsv";
            openFileDialog.Title = "Select Shops TSV";

            if (openFileDialog.ShowDialog() == true)
            {
                ShopsTsv.Text = openFileDialog.FileName;
            }
        }

        private void PatchShops_Click(object sender, RoutedEventArgs e)
        {
            string isoPath = IsoName.Text;
            string tsvPath = ShopsTsv.Text;

            if (string.IsNullOrWhiteSpace(isoPath) || string.IsNullOrWhiteSpace(tsvPath))
            {
                MessageBox.Show("Please select both an ISO file and a Shop TSV file before patching.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(isoPath) || !File.Exists(tsvPath))
            {
                MessageBox.Show("One or both files do not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using StreamReader reader = new StreamReader(tsvPath);
                string? line;

                // Step 1: Skip label row
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) break;
                }

                // Step 2: Read address row
                if (line == null) throw new Exception("Missing shop address header.");
                string[] labelLine = line.Split('\t', StringSplitOptions.None);

                line = reader.ReadLine();
                line = reader.ReadLine();
                if (line == null) throw new Exception("Missing shop address values.");
                string[] addressLine = line.Split('\t', StringSplitOptions.None);

                List<long> baseAddresses = new();
                for (int i = 0; i < addressLine.Length; i++)
                {
                    if (TryParseHex(addressLine[i].Trim(), out long addr))
                        baseAddresses.Add(addr);
                    else
                        baseAddresses.Add(-1); // Mark invalid column
                }

                // Step 3: Read card rows
                List<List<ushort>> columnData = baseAddresses.Select(_ => new List<ushort>()).ToList();

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] values = line.Split('\t', StringSplitOptions.None);

                    for (int col = 0; col < values.Length && col < baseAddresses.Count; col++)
                    {
                        string value = values[col].Trim();
                        if (ushort.TryParse(value, out ushort cardID))
                        {
                            columnData[col].Add(cardID);
                        }
                    }
                }

                // Step 4: Write to ISO
                using FileStream isoStream = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite);
                using BinaryWriter writer = new BinaryWriter(isoStream);

                for (int col = 0; col < baseAddresses.Count; col++)
                {
                    long addr = baseAddresses[col];
                    if (addr < 0) continue;

                    for (int i = 0; i < columnData[col].Count; i++)
                    {
                        long offset = addr + i * 2;
                        isoStream.Seek(offset, SeekOrigin.Begin);
                        writer.Write(columnData[col][i]);
                    }
                }

                MessageBox.Show("Shops patched successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during shop patching:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        #endregion

        #region Starter Symbols

        private void BrowseStarterSymbolsTsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "TSV files (*.tsv)|*.tsv";
            openFileDialog.Title = "Select Starter Symbols TSV";

            if (openFileDialog.ShowDialog() == true)
            {
                StarterSymbolsTsv.Text = openFileDialog.FileName;
            }
        }


        private void PatchStarterSymbols_Click(object sender, RoutedEventArgs e)
        {
            string isoPath = IsoName.Text;
            string tsvPath = StarterSymbolsTsv.Text;

            if (string.IsNullOrWhiteSpace(isoPath) || string.IsNullOrWhiteSpace(tsvPath))
            {
                MessageBox.Show("Please select both an ISO file and a Starter Symbols TSV file before patching.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(isoPath) || !File.Exists(tsvPath))
            {
                MessageBox.Show("One or both files do not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Dictionary<long, List<ushort>> addressDataMap = new();

                using StreamReader reader = new StreamReader(tsvPath);

                // Step 1: Read address headers (first non-empty line)
                string? addressLine = null;
                int c = 0;
                while ((addressLine = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(addressLine))
                        c++;

                    if (c == 2)
                        break;
                }

                if (addressLine == null)
                    throw new Exception("Starter TSV has no address header.");

                string[] addressParts = addressLine.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                List<long> addresses = new();

                foreach (string part in addressParts)
                {
                    if (TryParseHex(part.Trim(), out long addr))
                    {
                        addresses.Add(addr);
                        addressDataMap[addr] = new List<ushort>();
                    }
                }

                // Step 2: Fill each address column with vertical values
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] values = line.Split('\t');

                    for (int col = 0; col < values.Length && col < addresses.Count; col++)
                    {
                        string val = values[col].Trim();

                        if (ushort.TryParse(val, out ushort cardId))
                        {
                            addressDataMap[addresses[col]].Add(cardId);
                        }
                    }
                }

                // Step 3: Write all values to ISO
                using FileStream isoStream = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite);
                using BinaryWriter writer = new BinaryWriter(isoStream);

                foreach (var entry in addressDataMap)
                {
                    long baseAddr = entry.Key;
                    ushort ap = entry.Value[0];
                    long apAddr = baseAddr + 12;
                    ushort pp = entry.Value[1];
                    long ppAddr = baseAddr + 4;

                    isoStream.Seek(apAddr, SeekOrigin.Begin);
                    writer.Write(ap);


                    isoStream.Seek(ppAddr, SeekOrigin.Begin);
                    writer.Write(pp);

                }

                MessageBox.Show($"Successfully patched {addresses.Count} starter kit columns into the ISO.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during starter kit patching:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}