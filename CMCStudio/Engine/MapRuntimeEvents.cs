using System.IO;
using System.Text.Json;

namespace CMCStudio.Engine;

// Map-owned callbacks follow the selected map, never the receiving character.
// Unknown callbacks are rejected rather than copied with unresolved dependencies.
internal static class MapRuntimeEvents
{
    const int Entry = 18 * 4;
    const int ScriptLimit = 0x7000;
    static readonly byte[] Marker = "CMCMapE2"u8.ToArray();
    static readonly byte[] Water = LoadWater();
    sealed class Profile
    {
        public int Map { get; set; }
        public string Bytes { get; set; }
        public string Status { get; set; }
        public string Initialization { get; set; }
    }
    static readonly Dictionary<int,Profile> Profiles = LoadProfiles();
    static Dictionary<int,Profile> LoadProfiles()
    {
        using var stream=typeof(MapRuntimeEvents).Assembly.GetManifestResourceStream("MapTurnEvents.json")
            ?? throw new InvalidDataException("Missing map event profiles.");
        return JsonSerializer.Deserialize<Profile[]>(stream).ToDictionary(p=>p.Map);
    }
    public static void RequireSupported(int originalMap,int map)
    {
        if(map!=originalMap && Profiles.TryGetValue(map,out var profile) && profile.Status=="unresolved")
            throw new InvalidDataException($"Map {map}'s turn event still has unresolved script dependencies. This swap cannot safely transfer its effects yet.");
    }
    public static bool HasEvent(int map) => Profiles.TryGetValue(map,out var p) && p.Status is "recovered" or "restored";
    public static byte[] Initialization(int map) => Profiles.TryGetValue(map,out var p) && !string.IsNullOrEmpty(p.Initialization)
        ? Convert.FromHexString(p.Initialization) : Array.Empty<byte>();
    static byte[] LoadWater()
    {
        using var stream = typeof(MapRuntimeEvents).Assembly.GetManifestResourceStream("MapWaterEvent.bin")
            ?? throw new InvalidDataException("Missing water event bytecode.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
    public static bool Apply(ref byte[] bytes, int originalMap, int map)
    {
        bool changed = Restore(bytes);
        if (map == originalMap) return changed;
        RequireSupported(originalMap,map);
        // A map without an original encounter has no encounter turn callback.
        // Explicit return also removes the old map's effects on the receiver.
        byte[] payload = Profiles.TryGetValue(map,out var profile)
            ? Convert.FromHexString(profile.Bytes) : new byte[] { 4 };
        int saved = BitConverter.ToInt32(bytes, Entry);
        int header = -1;
        for (int p = 160; p <= bytes.Length - 16 - payload.Length; p++)
            if (bytes.AsSpan(p, 8).SequenceEqual(Marker)
                && BitConverter.ToInt32(bytes, p + 8) == saved
                && BitConverter.ToInt32(bytes, p + 12) == payload.Length)
            { header = p; break; }
        if (header < 0)
        {
            header = bytes.Length;
            int length = checked(header + 16 + payload.Length);
            if (length > ScriptLimit)
                throw new InvalidDataException($"Map {map}'s event cannot fit this encounter's 28 KiB script buffer ({length} bytes needed).");
            Array.Resize(ref bytes, length);
            Marker.CopyTo(bytes, header);
            BitConverter.GetBytes(saved).CopyTo(bytes, header + 8);
            BitConverter.GetBytes(payload.Length).CopyTo(bytes, header + 12);
        }
        payload.CopyTo(bytes, header + 16);
        BitConverter.GetBytes(header + 16 - Entry).CopyTo(bytes, Entry);
        return true;
    }
    public static bool Restore(byte[] bytes)
    {
        bool changed = false;
        while (true)
        {
            long target = (long)Entry + BitConverter.ToInt32(bytes, Entry);
            if (target < 16 || target > bytes.Length) return changed;
            int current = (int)target;
            var marker = bytes.AsSpan(current - 16, 8);
            int expected = marker.SequenceEqual("CMCWater"u8) ? 2417
                : marker.SequenceEqual("CMCTurn1"u8) ? 8192
                : marker.SequenceEqual("CMCWat2!"u8) || marker.SequenceEqual(Marker) ? BitConverter.ToInt32(bytes, current - 4) : 0;
            if (marker.SequenceEqual("CMCWat2!"u8) && expected != 1 && expected != 2417 && expected != Water.Length)
                throw new InvalidDataException("Invalid water event size.");
            if(marker.SequenceEqual(Marker) && (expected<1 || expected>ScriptLimit))
                throw new InvalidDataException("Invalid map event size.");
            if (expected == 0) return changed;
            if (BitConverter.ToInt32(bytes, current - 4) != expected
                || target + expected > bytes.Length)
                throw new InvalidDataException("Invalid saved map-event patch header.");
            int saved = BitConverter.ToInt32(bytes, current - 8);
            long original = (long)Entry + saved;
            if (original < 160 || original >= current - 16)
                throw new InvalidDataException("Invalid original map-event entry.");
            BitConverter.GetBytes(saved).CopyTo(bytes, Entry);
            changed = true;
        }
    }
}
