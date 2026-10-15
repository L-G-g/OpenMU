// Adds the monster spots of the server to the map which the Season 6 client opens with TAB.
//
// Usage (.NET 10, file-based app):
//   dotnet run MinimapSpots.cs -- <spots.csv> <original minimap folder> <output folder>
//
// spots.csv: one line per spot, "map;x1;y1;quantity;monster name", without header.
// The original minimap folder holds the untouched Minimap_World<N>_eng.bmd files of the client.
// The output folder gets a copy of each file of a map with spots, with one marker per spot
// (NPC kind, so the client shows the name on mouse over). A spot with 7 monsters is labeled as hotspot.
// The client reads at most 100 markers per map, so when there are too many, the nearest spots of the
// same monster are joined into one marker, e.g. "Bull Fighter x2".
using System.Globalization;
using System.Text;

const int MaxEntries = 100;
const int EntrySize = 116;
const int TailSize = 45;
const ushort ChecksumKey = 0x2BC1;
const int HotspotQuantity = 7;
byte[] buxCode = [0xFC, 0xCF, 0xAB];
var latin1 = Encoding.Latin1;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: dotnet run MinimapSpots.cs -- <spots.csv> <original minimap folder> <output folder>");
    return 1;
}

var spotsByMap = File.ReadAllLines(args[0])
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(line => line.Split(';'))
    .Select(p => (Map: int.Parse(p[0], CultureInfo.InvariantCulture), X: int.Parse(p[1], CultureInfo.InvariantCulture), Y: int.Parse(p[2], CultureInfo.InvariantCulture), Quantity: int.Parse(p[3], CultureInfo.InvariantCulture), Name: p[4].Trim()))
    .GroupBy(s => s.Map);

Directory.CreateDirectory(args[2]);
foreach (var mapSpots in spotsByMap.OrderBy(g => g.Key))
{
    var fileName = $"Minimap_World{mapSpots.Key + 1}_eng.bmd";
    var originalPath = Path.Combine(args[1], fileName);
    if (!File.Exists(originalPath))
    {
        Console.WriteLine($"{fileName}: not found, map {mapSpots.Key} skipped");
        continue;
    }

    var (entries, tail) = Read(originalPath);
    var existing = entries.Where(e => e.Kind != 0).ToList();
    var capacity = MaxEntries - existing.Count;

    // The marker is in the middle of the 4x4 area of the spot.
    var markers = mapSpots
        .Select(s => new Marker(s.Name, s.X + 1.5, s.Y + 1.5, 1, s.Quantity >= HotspotQuantity))
        .ToList();
    var spotCount = markers.Count;
    while (markers.Count > capacity)
    {
        var pair = markers
            .SelectMany((a, i) => markers.Skip(i + 1).Where(b => b.Name == a.Name).Select(b => (A: a, B: b)))
            .OrderBy(p => Math.Pow(p.A.X - p.B.X, 2) + Math.Pow(p.A.Y - p.B.Y, 2))
            .FirstOrDefault();
        if (pair.A is null)
        {
            Console.WriteLine($"{fileName}: {markers.Count - capacity} markers don't fit");
            markers = markers.Take(capacity).ToList();
            break;
        }

        var count = pair.A.Count + pair.B.Count;
        markers.Remove(pair.A);
        markers.Remove(pair.B);
        markers.Add(new Marker(
            pair.A.Name,
            ((pair.A.X * pair.A.Count) + (pair.B.X * pair.B.Count)) / count,
            ((pair.A.Y * pair.A.Count) + (pair.B.Y * pair.B.Count)) / count,
            count,
            pair.A.IsHotspot || pair.B.IsHotspot));
    }

    var result = existing
        .Concat(markers
            .OrderBy(m => m.X)
            .ThenBy(m => m.Y)
            .Select(m => new Entry(1, (int)Math.Round(m.X), (int)Math.Round(m.Y), 0, m.Label)))
        .ToList();
    Write(Path.Combine(args[2], fileName), result, tail);
    Console.WriteLine($"{fileName}: {existing.Count} original markers, {spotCount} spots in {markers.Count} markers");
}

return 0;

(List<Entry> Entries, byte[] Tail) Read(string path)
{
    var data = File.ReadAllBytes(path);
    var bodySize = (EntrySize * MaxEntries) + TailSize;
    var body = data.AsSpan(0, bodySize).ToArray();
    var checksum = BitConverter.ToUInt32(data, bodySize);
    if (checksum != Checksum(body, ChecksumKey))
    {
        throw new InvalidDataException($"{path}: wrong checksum");
    }

    var entries = new List<Entry>();
    for (var i = 0; i < MaxEntries; i++)
    {
        var raw = body.AsSpan(i * EntrySize, EntrySize).ToArray();
        Bux(raw);
        var nameLength = Array.IndexOf(raw, (byte)0, 16);
        var name = latin1.GetString(raw, 16, (nameLength < 0 ? EntrySize : nameLength) - 16);
        entries.Add(new Entry(raw[0], BitConverter.ToInt32(raw, 4), BitConverter.ToInt32(raw, 8), BitConverter.ToInt32(raw, 12), name));
    }

    return (entries, body[(EntrySize * MaxEntries)..]);
}

void Write(string path, List<Entry> entries, byte[] tail)
{
    var body = new byte[(EntrySize * MaxEntries) + TailSize];
    for (var i = 0; i < MaxEntries && i < entries.Count; i++)
    {
        var entry = entries[i];
        var raw = new byte[EntrySize];
        raw[0] = entry.Kind;
        BitConverter.GetBytes(entry.X).CopyTo(raw, 4);
        BitConverter.GetBytes(entry.Y).CopyTo(raw, 8);
        BitConverter.GetBytes(entry.Rotation).CopyTo(raw, 12);
        var name = latin1.GetBytes(entry.Name);
        Array.Copy(name, 0, raw, 16, Math.Min(name.Length, EntrySize - 16 - 1));
        Bux(raw);
        raw.CopyTo(body, i * EntrySize);
    }

    tail.CopyTo(body, EntrySize * MaxEntries);
    using var file = File.Create(path);
    file.Write(body);
    file.Write(BitConverter.GetBytes(Checksum(body, ChecksumKey)));
}

void Bux(byte[] buffer)
{
    for (var i = 0; i < buffer.Length; i++)
    {
        buffer[i] ^= buxCode[i % 3];
    }
}

static uint Checksum(byte[] buffer, ushort key)
{
    uint dwKey = key;
    var result = dwKey << 9;
    for (var offset = 0; offset <= buffer.Length - 4; offset += 4)
    {
        var value = BitConverter.ToUInt32(buffer, offset);
        if (((offset / 4) + key) % 2 == 0)
        {
            result ^= value;
        }
        else
        {
            result += value;
        }

        if (offset % 16 == 0)
        {
            result ^= (dwKey + result) >> (((offset / 4) % 8) + 1);
        }
    }

    return result;
}

internal record Entry(byte Kind, int X, int Y, int Rotation, string Name);

internal record Marker(string Name, double X, double Y, int Count, bool IsHotspot)
{
    public string Label => this.Name + (this.Count > 1 ? $" x{this.Count}" : string.Empty) + (this.IsHotspot ? " (Hotspot)" : string.Empty);
}
