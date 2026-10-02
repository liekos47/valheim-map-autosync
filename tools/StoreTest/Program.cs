using System.IO.Compression;
using AutoSyncMap;

// Stand-in for the game's Utils (gzip), so ServerStore.cs compiles and runs unchanged.
public static class Utils
{
    public static byte[] Compress(byte[] d) { using var o = new MemoryStream(); using (var g = new GZipStream(o, CompressionMode.Compress)) g.Write(d, 0, d.Length); return o.ToArray(); }
    public static byte[] Decompress(byte[] d) { using var i = new GZipStream(new MemoryStream(d), CompressionMode.Decompress); using var o = new MemoryStream(); i.CopyTo(o); return o.ToArray(); }
}

public static class Test
{
    const int N = 2048 * 2048;
    record Pin(long Owner, string Name, float X, float Y, float Z, int Type, bool Checked, string Author);

    static byte[] Blob(IEnumerable<int> explored, params Pin[] pins)
    {
        using var s = new MemoryStream(); using var w = new BinaryWriter(s);
        var e = new byte[N]; foreach (int i in explored) e[i] = 1;
        w.Write(3); w.Write(N); w.Write(e); w.Write(pins.Length);
        foreach (var p in pins) { w.Write(p.Owner); w.Write(p.Name); w.Write(p.X); w.Write(p.Y); w.Write(p.Z); w.Write(p.Type); w.Write(p.Checked); w.Write(p.Author); }
        w.Flush(); return Utils.Compress(s.ToArray());
    }
    static (HashSet<int> explored, List<Pin> pins) Read(byte[] compressed)
    {
        using var r = new BinaryReader(new MemoryStream(Utils.Decompress(compressed)));
        if (r.ReadInt32() != 3) throw new Exception("version");
        int n = r.ReadInt32(); var e = r.ReadBytes(n); var set = new HashSet<int>(); for (int i = 0; i < n; i++) if (e[i] != 0) set.Add(i);
        int c = r.ReadInt32(); var pins = new List<Pin>();
        for (int i = 0; i < c; i++) pins.Add(new Pin(r.ReadInt64(), r.ReadString(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadInt32(), r.ReadBoolean(), r.ReadString()));
        if (r.BaseStream.Position != r.BaseStream.Length) throw new Exception("trailing bytes");
        return (set, pins);
    }
    static int fails;
    static void Check(string what, bool ok) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}"); if (!ok) fails++; }

    public static int Main()
    {
        string file = Path.Combine(Path.GetTempPath(), "autosyncmap-test.dat"); File.Delete(file);
        var store = new ServerStore(file);
        var a1 = new Pin(1, "Astrid base", 10, 20, 30, 3, false, "Steam_1"); var a2 = new Pin(1, "Åstrid's mine ⛏", -5, 0, 7.5f, 1, true, "Steam_1");
        var b1 = new Pin(2, "Bjorn portal", 100, 0, -100, 6, false, "Steam_2");

        // Astrid uploads: her cells, her two pins, plus a stale copy of a pin she got from Bjorn (must be ignored).
        var m = Read(store.Merge(Blob(new[] { 1, 2, 3 }, a1, a2, new Pin(2, "old Bjorn pin", 1, 1, 1, 1, false, "Steam_2")), 1, out string info));
        Console.WriteLine("  " + info);
        Check("first upload: explored kept", m.explored.SetEquals(new[] { 1, 2, 3 }));
        Check("first upload: only the uploader's own pins stored", m.pins.Count == 2 && m.pins.All(p => p.Owner == 1));
        Check("pin text with non-ASCII characters survives", m.pins.Any(p => p.Name == "Åstrid's mine ⛏" && p.Checked && p.Z == 7.5f));

        m = Read(store.Merge(Blob(new[] { 3, 4, N - 1 }, b1), 2, out info));
        Console.WriteLine("  " + info);
        Check("second player: explored is the union", m.explored.SetEquals(new[] { 1, 2, 3, 4, N - 1 }));
        Check("second player: both players' pins present", m.pins.Count == 3 && m.pins.Count(p => p.Owner == 2) == 1);

        // Astrid deletes one pin and explores more; Bjorn's pin must stay, explored never shrinks.
        m = Read(store.Merge(Blob(new[] { 7 }, a1), 1, out info));
        Console.WriteLine("  " + info);
        Check("owner deleting a pin removes it for everyone", m.pins.Count == 2 && !m.pins.Any(p => p.Name.StartsWith("Å")));
        Check("other player's pin untouched", m.pins.Any(p => p.Name == "Bjorn portal"));
        Check("explored never shrinks", m.explored.SetEquals(new[] { 1, 2, 3, 4, 7, N - 1 }));

        // A new store reading the file back (server restart).
        var again = Read(new ServerStore(file).Merge(Blob(Array.Empty<int>()), 3, out info));
        Check("survives a restart (read back from disk)", again.explored.SetEquals(m.explored) && again.pins.Count == 2);

        bool threw = false; try { store.Merge(Utils.Compress(new byte[] { 9, 0, 0, 0, 0, 0, 0, 0 }), 1, out _); } catch (InvalidDataException) { threw = true; }
        Check("unknown version rejected", threw);
        Check("rejected upload did not damage the stored map", Read(store.Merge(Blob(Array.Empty<int>()), 3, out _)).explored.SetEquals(m.explored));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var big = Blob(Enumerable.Range(0, N).Where(i => i % 3 == 0), Enumerable.Range(0, 200).Select(i => new Pin(5, "pin " + i, i, 0, i, 1, false, "Steam_5")).ToArray());
        long build = sw.ElapsedMilliseconds; sw.Restart();
        byte[] merged = store.Merge(big, 5, out info);
        Console.WriteLine($"  realistic size: upload {big.Length / 1024} KB, merged {merged.Length / 1024} KB, merge took {sw.ElapsedMilliseconds} ms ({info})");
        File.Delete(file);
        Console.WriteLine(fails == 0 ? "ALL PASSED" : $"{fails} FAILED");
        return fails;
    }
}
