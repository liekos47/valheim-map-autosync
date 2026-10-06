using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapAutoSync
{
	/*
		Sends one block of bytes between a client and the server as a series of routed RPCs.

		A shared map is a few hundred kilobytes compressed, and one network message has a size limit,
		so the block goes out in chunks and is put back together on the other side. Every chunk is one
		"MapAutoSync" routed RPC carrying: kind, transfer id, chunk index, chunk count, the sender's
		player id, the sender's mod version, and the bytes.
	*/
	internal static class Transfer
	{
		internal const string Rpc = "MapAutoSync";
		// The name the mod's RPC had before 0.3.0, when the mod was called AutoSyncMap. Its packets
		// are the same but carry no version. The server still answers it, so players who have not
		// updated keep syncing (and are told to update).
		internal const string LegacyRpc = "AutoSyncMap";
		internal const byte Upload = 1; // client -> server: my explored map and pins
		internal const byte Merged = 2; // server -> client: everyone's, merged
		private const int ChunkSize = 64 * 1024;
		private const float StaleSeconds = 120f;

		private class Incoming
		{
			public byte[][] Chunks;
			public int Received;
			public float Started;
		}

		private static readonly Dictionary<(long, int), Incoming> s_incoming = new Dictionary<(long, int), Incoming>();
		private static int s_nextId = 1;

		internal static void Send(long target, string rpc, byte kind, long playerId, byte[] data)
		{
			int id = s_nextId++;
			int total = Mathf.Max(1, (data.Length + ChunkSize - 1) / ChunkSize);
			for (int i = 0; i < total; i++)
			{
				int offset = i * ChunkSize;
				int length = Mathf.Min(ChunkSize, data.Length - offset);
				var chunk = new byte[length];
				Buffer.BlockCopy(data, offset, chunk, 0, length);
				var pkg = new ZPackage();
				pkg.Write(kind);
				pkg.Write(id);
				pkg.Write(i);
				pkg.Write(total);
				pkg.Write(playerId);
				if (rpc != LegacyRpc)
				{
					pkg.Write(MapAutoSyncPlugin.Version);
				}
				pkg.Write(chunk);
				ZRoutedRpc.instance.InvokeRoutedRPC(target, rpc, pkg);
			}
		}

		// Feeds one received chunk in. Returns true with the whole block once the last chunk is here.
		// "version" is the sender's mod version, or empty for a packet under the old name.
		internal static bool Receive(long sender, ZPackage pkg, bool legacy, out byte kind, out long playerId, out string version, out byte[] data)
		{
			kind = pkg.ReadByte();
			int id = pkg.ReadInt();
			int index = pkg.ReadInt();
			int total = pkg.ReadInt();
			playerId = pkg.ReadLong();
			version = legacy ? "" : pkg.ReadString();
			byte[] chunk = pkg.ReadByteArray();
			data = null;
			if (total < 1 || total > 4096 || index < 0 || index >= total)
			{
				return false;
			}

			foreach (var stale in s_incoming.Where(kv => Time.time - kv.Value.Started > StaleSeconds).Select(kv => kv.Key).ToList())
			{
				s_incoming.Remove(stale);
			}
			var key = (sender, id);
			if (!s_incoming.TryGetValue(key, out Incoming incoming))
			{
				s_incoming[key] = incoming = new Incoming { Chunks = new byte[total][], Started = Time.time };
			}
			if (incoming.Chunks.Length != total || incoming.Chunks[index] != null)
			{
				return false;
			}
			incoming.Chunks[index] = chunk;
			if (++incoming.Received < total)
			{
				return false;
			}
			s_incoming.Remove(key);
			data = new byte[incoming.Chunks.Sum(c => c.Length)];
			int at = 0;
			foreach (byte[] c in incoming.Chunks)
			{
				Buffer.BlockCopy(c, 0, data, at, c.Length);
				at += c.Length;
			}
			return true;
		}
	}
}
