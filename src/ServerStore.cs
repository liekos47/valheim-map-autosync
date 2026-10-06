using System;
using System.Collections.Generic;
using System.IO;

namespace MapAutoSync
{
	/*
		The server's one merged map: what a cartography table would hold if every player wrote to it
		and read from it every day.

		The data is the game's own shared-map format (Minimap.GetSharedMapData, version 3): an int
		version, an int length, that many explored flags (one byte each), an int pin count, then per
		pin: long owner, string name, three floats position, int type, bool checked, string author.
		It travels and is stored gzip-compressed (Utils.Compress), as on a table.

		Merging an upload from one player:
		- explored area is only ever added (OR);
		- that player's pins are replaced by the set they sent, so a pin they deleted disappears for
		  everyone; every other player's pins are kept as they are. The upload also carries pins the
		  player received from others; those are ignored, the owner's own upload is the truth.

		Why one merged map and not one per player: Minimap.AddSharedMapData deletes every pin owned by
		someone else that is not in the data it is given, so a client must receive everybody's pins
		in a single block.

		Merging runs off the main thread (it decompresses and walks about 4 MB), so everything here
		is plain managed code under one lock, with no Unity calls.
	*/
	internal class ServerStore
	{
		private struct Pin
		{
			public long Owner;
			public string Name, Author;
			public float X, Y, Z;
			public int Type;
			public bool Checked;
		}

		private const int Version = 3;
		private readonly object m_lock = new object();
		private readonly string m_file;
		private byte[] m_explored;
		private List<Pin> m_pins = new List<Pin>();
		private bool m_loaded;

		internal ServerStore(string file)
		{
			m_file = file;
		}

		// Returns the merged map, compressed, ready to send back. Throws on data it cannot read.
		internal byte[] Merge(byte[] uploadCompressed, long playerId, out string info)
		{
			lock (m_lock)
			{
				Load();
				Parse(Utils.Decompress(uploadCompressed), out byte[] explored, out List<Pin> pins);

				int added = 0;
				if (m_explored == null || m_explored.Length != explored.Length)
				{
					m_explored = explored;
					foreach (byte b in explored) added += b;
				}
				else
				{
					for (int i = 0; i < explored.Length; i++)
					{
						if (explored[i] != 0 && m_explored[i] == 0)
						{
							m_explored[i] = 1;
							added++;
						}
					}
				}

				int before = m_pins.Count;
				m_pins.RemoveAll(p => p.Owner == playerId);
				int others = m_pins.Count, own = 0;
				foreach (Pin pin in pins)
				{
					if (pin.Owner == playerId)
					{
						m_pins.Add(pin);
						own++;
					}
				}

				byte[] merged = Utils.Compress(Write());
				string tmp = m_file + ".tmp";
				File.WriteAllBytes(tmp, merged);
				if (File.Exists(m_file))
				{
					File.Delete(m_file);
				}
				File.Move(tmp, m_file);
				info = $"{added} newly explored cells, {own} own pins ({before - others} before), {m_pins.Count} pins in total, {merged.Length / 1024} KB";
				return merged;
			}
		}

		private void Load()
		{
			if (m_loaded)
			{
				return;
			}
			m_loaded = true;
			if (File.Exists(m_file))
			{
				Parse(Utils.Decompress(File.ReadAllBytes(m_file)), out m_explored, out m_pins);
			}
		}

		private static void Parse(byte[] data, out byte[] explored, out List<Pin> pins)
		{
			using (var r = new BinaryReader(new MemoryStream(data)))
			{
				int version = r.ReadInt32();
				if (version != Version)
				{
					throw new InvalidDataException($"shared map version {version}, expected {Version}");
				}
				int length = r.ReadInt32();
				if (length < 0 || length > data.Length)
				{
					throw new InvalidDataException($"explored length {length}");
				}
				explored = r.ReadBytes(length);
				int count = r.ReadInt32();
				if (count < 0 || count > 100000)
				{
					throw new InvalidDataException($"pin count {count}");
				}
				pins = new List<Pin>(count);
				for (int i = 0; i < count; i++)
				{
					pins.Add(new Pin
					{
						Owner = r.ReadInt64(),
						Name = r.ReadString(),
						X = r.ReadSingle(),
						Y = r.ReadSingle(),
						Z = r.ReadSingle(),
						Type = r.ReadInt32(),
						Checked = r.ReadBoolean(),
						Author = r.ReadString(),
					});
				}
			}
		}

		private byte[] Write()
		{
			using (var stream = new MemoryStream(m_explored.Length + 64 * m_pins.Count + 16))
			using (var w = new BinaryWriter(stream))
			{
				w.Write(Version);
				w.Write(m_explored.Length);
				w.Write(m_explored);
				w.Write(m_pins.Count);
				foreach (Pin pin in m_pins)
				{
					w.Write(pin.Owner);
					w.Write(pin.Name ?? "");
					w.Write(pin.X);
					w.Write(pin.Y);
					w.Write(pin.Z);
					w.Write(pin.Type);
					w.Write(pin.Checked);
					w.Write(pin.Author ?? "");
				}
				w.Flush();
				return stream.ToArray();
			}
		}
	}
}
