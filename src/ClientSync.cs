using System;
using UnityEngine;

namespace MapAutoSync
{
	/*
		The player's side: when to sync, and the two halves of a sync.

		A sync is what a visit to a cartography table does, in the table's order turned around so one
		round trip is enough: write (send Minimap.GetSharedMapData), then read (merge what the server
		sends back with Minimap.AddSharedMapData). It happens once about 20 seconds after spawning,
		then each time the in-game day changes, and at once when the tick-box is switched on or
		"Sync now" is clicked.
	*/
	internal static class ClientSync
	{
		private const float JoinDelay = 20f;
		private static float s_readyAt = -1f;
		private static bool s_synced;
		private static int s_lastDay = -1;
		private static bool s_now;

		// Called when no session is running, so the next join syncs again.
		internal static void Reset()
		{
			s_readyAt = -1f;
			s_synced = false;
			s_lastDay = -1;
			s_now = false;
		}

		internal static void RequestNow()
		{
			s_now = true;
		}

		internal static void Tick()
		{
			if (Player.m_localPlayer == null || Minimap.instance == null || EnvMan.instance == null || ZRoutedRpc.instance == null)
			{
				return;
			}
			if (s_readyAt < 0f)
			{
				s_readyAt = Time.time + JoinDelay;
			}
			// "Sync now" works whether or not the tick-box is on; the timed syncs need it on.
			int day = EnvMan.instance.GetDay();
			bool timed = MapAutoSyncPlugin.AutoSync.Value
				&& ((!s_synced && Time.time >= s_readyAt) || (s_synced && day != s_lastDay));
			bool due = s_now || timed;
			if (!due)
			{
				return;
			}
			s_now = false;
			s_synced = true;
			s_lastDay = day;
			Upload();
		}

		private static void Upload()
		{
			try
			{
				byte[] data = Utils.Compress(Minimap.instance.GetSharedMapData(null));
				Transfer.Send(ZRoutedRpc.instance.GetServerPeerID(), Transfer.Upload, Player.m_localPlayer.GetPlayerID(), data);
				MapAutoSyncPlugin.Log.LogInfo($"sent map to the server: {data.Length / 1024} KB");
			}
			catch (Exception e)
			{
				MapAutoSyncPlugin.Log.LogError($"map upload failed: {e}");
			}
		}

		// The server's merged map arrived.
		internal static void Merged(byte[] data)
		{
			if (Player.m_localPlayer == null || Minimap.instance == null)
			{
				return;
			}
			bool changed = Minimap.instance.AddSharedMapData(Utils.Decompress(data));
			MapToggle.SyncDone();
			MapAutoSyncPlugin.Log.LogInfo($"merged the server's map: {data.Length / 1024} KB, {(changed ? "new areas or pins" : "nothing new")}");
			if (MapAutoSyncPlugin.ShowMessage.Value)
			{
				Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, changed ? "Map synced: new areas or pins" : "Map synced");
			}
		}
	}
}
