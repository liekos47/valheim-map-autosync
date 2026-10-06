using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MapAutoSync
{
	/*
		Shares every player's explored map and pins automatically, without a cartography table.

		One DLL, installed on the dedicated server and on each player who wants it:

		- On a player's game it adds an "Auto sync map" tick-box next to "Visible to other players"
		  on the large map, and a "Sync now" button above it. While it is on, the game sends its shared map data to the server when the
		  player joins and once every in-game day, and merges what comes back. Both steps use the
		  game's own cartography table code (Minimap.GetSharedMapData / AddSharedMapData), so the
		  result is what writing to and reading from a table gives.
		- On the server it keeps the one merged map (ServerStore), beside the world save, and sends
		  it back to whoever uploads.

		Players without the mod are unaffected, and so is a server without it: the upload is a routed
		RPC the server simply has no handler for.
	*/
	[BepInPlugin(Guid, Name, Version)]
	public class MapAutoSyncPlugin : BaseUnityPlugin
	{
		public const string Guid = "liekos47.mapautosync";
		public const string Name = "MapAutoSync";
		public const string Version = "0.3.0";

		internal static ManualLogSource Log;
		internal static MapAutoSyncPlugin Instance;

		internal static ConfigEntry<bool> AutoSync;
		internal static ConfigEntry<bool> ShowMessage;
		internal static ConfigEntry<string> ButtonLabel;
		internal static ConfigEntry<string> SyncNowLabel;
		internal static ConfigEntry<float> ButtonOffsetX;
		internal static ConfigEntry<float> ButtonOffsetY;

		private Harmony harmony;
		private ZRoutedRpc registeredFor;
		private ServerStore store;
		private string storeWorld;
		private float nextTick;
		// Connections already told, this session, that their copy of the mod is out of date.
		private readonly HashSet<long> reminded = new HashSet<long>();
		// Work finished on a background thread that must be completed on the main thread.
		private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();

		private void Awake()
		{
			Log = Logger;
			Instance = this;

			AutoSync = Config.Bind("General", "AutoSync", true,
				"Share your explored map and pins with the server when you join and once every in-game day. This is the tick-box on the large map. The \"Sync now\" button above it works either way.");
			ShowMessage = Config.Bind("General", "ShowMessage", true,
				"Show a short top-left message when the map has been synced automatically. A \"Sync now\" always reports on screen.");
			ButtonLabel = Config.Bind("Button", "Label", "Auto sync map",
				"The text beside the tick-box on the large map.");
			SyncNowLabel = Config.Bind("Button", "SyncNowLabel", "Sync now",
				"The text beside the button above the tick-box that syncs the map straight away.");
			ButtonOffsetX = Config.Bind("Button", "OffsetX", 0f,
				"Moves the tick-box sideways from the \"Visible to other players\" tick-box, in screen units. Applied when the map screen is created (rejoin to see a change).");
			ButtonOffsetY = Config.Bind("Button", "OffsetY", 0f,
				"Moves the tick-box up or down. 0 places it one row above \"Visible to other players\", or above the game's \"Cartography Table\" row when that is showing; use a negative number to move it down.");

			// Its own Harmony id per load, so an unloading copy (hot reload) cannot remove a new copy's patches.
			harmony = new Harmony($"{Guid}.{DateTime.Now.Ticks}");
			harmony.PatchAll();
			Log.LogInfo($"{Name} {Version} loaded");
		}

		private void Update()
		{
			while (mainThread.TryDequeue(out Action action))
			{
				try { action(); } catch (Exception e) { Log.LogError($"{e}"); }
			}
			if (Time.time < nextTick)
			{
				return;
			}
			nextTick = Time.time + 2f;
			try
			{
				RegisterRpc();
				if (ZNet.instance != null && !ZNet.instance.IsServer())
				{
					MapToggle.Ensure();
					ClientSync.Tick();
				}
				else
				{
					ClientSync.Reset();
				}
			}
			catch (Exception e)
			{
				Log.LogError($"tick failed: {e}");
			}
		}

		// ZRoutedRpc is recreated for every session. A hot-reloaded copy also has to replace the
		// handler the previous copy left behind, since registering a name twice throws.
		private void RegisterRpc()
		{
			if (ZRoutedRpc.instance == null || ZRoutedRpc.instance == registeredFor)
			{
				return;
			}
			ZRoutedRpc.instance.m_functions.Remove(Transfer.Rpc.GetStableHashCode());
			ZRoutedRpc.instance.m_functions.Remove(Transfer.LegacyRpc.GetStableHashCode());
			ZRoutedRpc.instance.Register<ZPackage>(Transfer.Rpc, (sender, pkg) => OnPackage(sender, pkg, false));
			ZRoutedRpc.instance.Register<ZPackage>(Transfer.LegacyRpc, (sender, pkg) => OnPackage(sender, pkg, true));
			reminded.Clear();
			registeredFor = ZRoutedRpc.instance;
		}

		// "legacy": the packet came under the name the mod had before 0.3.0.
		private void OnPackage(long sender, ZPackage pkg, bool legacy)
		{
			try
			{
				if (!Transfer.Receive(sender, pkg, legacy, out byte kind, out long playerId, out string version, out byte[] data))
				{
					return;
				}
				bool server = ZNet.instance != null && ZNet.instance.IsServer();
				if (kind == Transfer.Upload && server)
				{
					ServerMerge(sender, playerId, data, legacy ? Transfer.LegacyRpc : Transfer.Rpc);
					Remind(sender, version, legacy);
				}
				else if (kind == Transfer.Merged && !server && !legacy)
				{
					ClientSync.Merged(data);
				}
			}
			catch (Exception e)
			{
				Log.LogError($"bad map package from {sender}: {e}");
			}
		}

		// Server: a player whose copy of the mod is older than the server's still gets synced, and is
		// told once per connection, in the middle of their screen, to update. The message goes
		// through the game's own "ShowMessage" RPC, so it needs nothing on the player's side.
		private void Remind(long sender, string version, bool legacy)
		{
			bool outdated = legacy
				|| (System.Version.TryParse(version, out System.Version theirs) && theirs < new System.Version(Version));
			if (!outdated || !reminded.Add(sender))
			{
				return;
			}
			string text = legacy
				? $"Your map sync mod is out of date. Please replace AutoSyncMap with {Name} {Version} or later."
				: $"{Name} {version} is out of date. This server runs {Version}, please update.";
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, "ShowMessage", (int)MessageHud.MessageType.Center, text);
			string who = ZNet.instance.GetPeer(sender)?.m_playerName ?? sender.ToString();
			Log.LogInfo($"{who} runs {(legacy ? "a version from before 0.3.0 (AutoSyncMap)" : version)}: reminded to update");
		}

		private void ServerMerge(long sender, long playerId, byte[] upload, string rpc)
		{
			string world = ZNet.instance.GetWorldName();
			if (store == null || storeWorld != world)
			{
				string folder = SaveSystem.GetWorldsSaveRootPath(ZNet.m_world.m_fileSource);
				string file = Path.Combine(folder, world + ".mapautosync.dat");
				// The shared map kept by a version from before 0.3.0 carries over.
				string before = Path.Combine(folder, world + ".autosyncmap.dat");
				if (!File.Exists(file) && File.Exists(before))
				{
					File.Move(before, file);
					Log.LogInfo($"took over the shared map from before 0.3.0: {Path.GetFileName(before)} is now {Path.GetFileName(file)}");
				}
				store = new ServerStore(file);
				storeWorld = world;
			}
			ServerStore target = store;
			string who = ZNet.instance.GetPeer(sender)?.m_playerName ?? sender.ToString();
			// Decompressing, merging and compressing about 4 MB takes long enough to be felt, so it runs
			// off the main thread; only the send happens back on it.
			Task.Run(() =>
			{
				try
				{
					var watch = System.Diagnostics.Stopwatch.StartNew();
					byte[] merged = target.Merge(upload, playerId, out string info);
					mainThread.Enqueue(() =>
					{
						if (ZNet.instance != null && ZNet.instance.GetPeer(sender) != null)
						{
							Transfer.Send(sender, rpc, Transfer.Merged, 0L, merged);
						}
						Log.LogInfo($"synced {who}: {upload.Length / 1024} KB up, {info}, {watch.ElapsedMilliseconds} ms");
					});
				}
				catch (Exception e)
				{
					mainThread.Enqueue(() => Log.LogError($"merge failed for {who}: {e}"));
				}
			});
		}

		private void OnDestroy()
		{
			harmony?.UnpatchSelf();
			MapToggle.Remove();
			if (Instance == this)
			{
				Instance = null;
			}
		}
	}
}
