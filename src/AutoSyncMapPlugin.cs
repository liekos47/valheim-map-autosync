using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AutoSyncMap
{
	/*
		Shares every player's explored map and pins automatically, without a cartography table.

		One DLL, installed on the dedicated server and on each player who wants it:

		- On a player's game it adds an "Auto sync map" tick-box next to "Visible to other players"
		  on the large map. While it is on, the game sends its shared map data to the server when the
		  player joins and once every in-game day, and merges what comes back. Both steps use the
		  game's own cartography table code (Minimap.GetSharedMapData / AddSharedMapData), so the
		  result is what writing to and reading from a table gives.
		- On the server it keeps the one merged map (ServerStore), beside the world save, and sends
		  it back to whoever uploads.

		Players without the mod are unaffected, and so is a server without it: the upload is a routed
		RPC the server simply has no handler for.
	*/
	[BepInPlugin(Guid, Name, Version)]
	public class AutoSyncMapPlugin : BaseUnityPlugin
	{
		public const string Guid = "liekos47.autosyncmap";
		public const string Name = "AutoSyncMap";
		public const string Version = "0.1.0";

		internal static ManualLogSource Log;
		internal static AutoSyncMapPlugin Instance;

		internal static ConfigEntry<bool> AutoSync;
		internal static ConfigEntry<bool> ShowMessage;
		internal static ConfigEntry<string> ButtonLabel;
		internal static ConfigEntry<float> ButtonOffsetX;
		internal static ConfigEntry<float> ButtonOffsetY;

		private Harmony harmony;
		private ZRoutedRpc registeredFor;
		private ServerStore store;
		private string storeWorld;
		private float nextTick;
		// Work finished on a background thread that must be completed on the main thread.
		private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();

		private void Awake()
		{
			Log = Logger;
			Instance = this;

			AutoSync = Config.Bind("General", "AutoSync", true,
				"Share your explored map and pins with the server when you join and once every in-game day. This is the tick-box on the large map.");
			ShowMessage = Config.Bind("General", "ShowMessage", true,
				"Show a short top-left message when the map has been synced.");
			ButtonLabel = Config.Bind("Button", "Label", "Auto sync map",
				"The text beside the tick-box on the large map.");
			ButtonOffsetX = Config.Bind("Button", "OffsetX", 0f,
				"Moves the tick-box sideways from the \"Visible to other players\" tick-box, in screen units. Applied when the map screen is created (rejoin to see a change).");
			ButtonOffsetY = Config.Bind("Button", "OffsetY", 0f,
				"Moves the tick-box up or down. 0 places it one row above \"Visible to other players\"; use a negative number to put it below.");

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
			ZRoutedRpc.instance.Register<ZPackage>(Transfer.Rpc, OnPackage);
			registeredFor = ZRoutedRpc.instance;
		}

		private void OnPackage(long sender, ZPackage pkg)
		{
			try
			{
				if (!Transfer.Receive(sender, pkg, out byte kind, out long playerId, out byte[] data))
				{
					return;
				}
				bool server = ZNet.instance != null && ZNet.instance.IsServer();
				if (kind == Transfer.Upload && server)
				{
					ServerMerge(sender, playerId, data);
				}
				else if (kind == Transfer.Merged && !server)
				{
					ClientSync.Merged(data);
				}
			}
			catch (Exception e)
			{
				Log.LogError($"bad map package from {sender}: {e}");
			}
		}

		private void ServerMerge(long sender, long playerId, byte[] upload)
		{
			string world = ZNet.instance.GetWorldName();
			if (store == null || storeWorld != world)
			{
				store = new ServerStore(Path.Combine(SaveSystem.GetWorldsSaveRootPath(ZNet.m_world.m_fileSource), world + ".autosyncmap.dat"));
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
							Transfer.Send(sender, Transfer.Merged, 0L, merged);
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
