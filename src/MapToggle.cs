using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MapAutoSync
{
	/*
		The "Auto sync map" tick-box and the "Sync now" button on the large map.

		Both are copies of the game's own "Visible to other players" row: the tick-box
		(Minimap.m_publicPosition) together with the panel it sits in, which carries the dark shade
		behind the label. So they look and behave like the game's rows, and are placed above them
		(see Place). The copies' click handlers are replaced: the original's is set in the prefab and
		would toggle public position.

		"Sync now" is a tick-box used as a button: a click asks for a sync, and the tick shows while
		it is under way, clearing when the server's map has been merged. The player is told what is
		happening twice over: in the button's own label ("Syncing...", then "Sync complete"), and
		in large text on the map (MapBanner).
	*/
	internal static class MapToggle
	{
		private const float SyncNowTimeout = 10f;
		private const float ResultSeconds = 4f; // how long the label keeps showing the outcome
		private static Toggle s_toggle;
		private static Toggle s_syncNow;
		private static RectTransform s_toggleRow;
		private static RectTransform s_syncNowRow;
		private static float s_syncNowUntil;
		private static float s_labelResetAt = -1f;

		internal static void Ensure()
		{
			if (Minimap.instance == null || Minimap.instance.m_publicPosition == null)
			{
				return;
			}
			if (s_toggle != null && s_syncNow != null)
			{
				Place();
				// No reply (a server without the mod, or a lost one): do not leave the tick on.
				if (s_syncNow.isOn && Time.time >= s_syncNowUntil)
				{
					Finish("No reply from the server");
				}
				else if (!s_syncNow.isOn && s_labelResetAt >= 0f && Time.time >= s_labelResetAt)
				{
					s_labelResetAt = -1f;
					SetLabel(s_syncNow.gameObject, MapAutoSyncPlugin.SyncNowLabel.Value);
				}
				return;
			}
			Remove();

			RectTransform source = SourceRow();
			s_toggle = Copy(source, "MapAutoSyncToggle", MapAutoSyncPlugin.ButtonLabel.Value, out s_toggleRow);
			s_toggle.isOn = MapAutoSyncPlugin.AutoSync.Value;
			s_toggle.onValueChanged.AddListener(on =>
			{
				MapAutoSyncPlugin.AutoSync.Value = on; // saved to the config file
				if (on)
				{
					ClientSync.RequestNow();
				}
			});

			s_syncNow = Copy(source, "MapAutoSyncSyncNow", MapAutoSyncPlugin.SyncNowLabel.Value, out s_syncNowRow);
			s_syncNow.isOn = false;
			s_syncNow.onValueChanged.AddListener(on =>
			{
				if (on)
				{
					s_syncNowUntil = Time.time + SyncNowTimeout;
					s_labelResetAt = -1f;
					SetLabel(s_syncNow.gameObject, "Syncing...");
					Say("Syncing map...");
					ClientSync.RequestNow();
					ClientSync.Tick(); // send it now rather than at the next tick
				}
			});

			// In a layout group the order of the rows decides where they go, not their position.
			if (InLayoutGroup(source))
			{
				Transform first = source;
				GameObject hint = Minimap.instance.m_sharedMapHint;
				if (hint != null && hint.transform.parent == source.parent && hint.transform.GetSiblingIndex() < first.GetSiblingIndex())
				{
					first = hint.transform;
				}
				s_toggleRow.SetSiblingIndex(first.GetSiblingIndex());
				s_syncNowRow.SetSiblingIndex(s_toggleRow.GetSiblingIndex());
			}
			Place();
			MapAutoSyncPlugin.Log.LogInfo($"added the Auto sync map tick-box and the Sync now button to the large map (copied {Path(source)}, tick-box {Path(Minimap.instance.m_publicPosition.transform)}, layout group {InLayoutGroup(source)})");
		}

		// The server's map has been merged. Returns true if this was a sync the player asked for with
		// "Sync now", in which case the player has been told here.
		internal static bool SyncDone(bool changed)
		{
			if (s_syncNow == null || !s_syncNow.isOn)
			{
				return false;
			}
			Finish(changed ? "Sync complete: new areas or pins" : "Sync complete", "Sync complete");
			return true;
		}

		// Ends a "Sync now": clears the tick, and shows the outcome on screen and, for a few seconds,
		// in the button's label.
		private static void Finish(string message, string label = null)
		{
			s_syncNow.SetIsOnWithoutNotify(false);
			SetLabel(s_syncNow.gameObject, label ?? message);
			s_labelResetAt = Time.time + ResultSeconds;
			Say(message);
		}

		// On the map if it is open (it is, when the button was clicked), else mid-screen.
		private static void Say(string message)
		{
			MapBanner.Show(message, MessageHud.MessageType.Center);
		}

		// The whole "Visible to other players" row: the tick-box and, around it, the panel with the
		// shade behind the label. That is the largest object holding the tick-box that is still only
		// that row: below the large map's root, not holding the "Cartography Table" row, and with no
		// other control in it. If nothing like that surrounds the tick-box, the tick-box itself.
		private static RectTransform SourceRow()
		{
			Transform toggle = Minimap.instance.m_publicPosition.transform;
			Transform root = Minimap.instance.m_largeRoot != null ? Minimap.instance.m_largeRoot.transform : null;
			GameObject hint = Minimap.instance.m_sharedMapHint;
			Transform row = toggle;
			while (row.parent is RectTransform parent && parent != root
				&& (hint == null || !hint.transform.IsChildOf(parent))
				&& parent.GetComponentsInChildren<Selectable>(true).Length == 1)
			{
				row = parent;
			}
			return (RectTransform)row;
		}

		private static bool InLayoutGroup(Transform row)
		{
			return row.parent != null && row.parent.GetComponent<LayoutGroup>() != null;
		}

		private static Toggle Copy(RectTransform source, string name, string label, out RectTransform row)
		{
			GameObject copy = Object.Instantiate(source.gameObject, source.parent);
			copy.name = name;
			row = (RectTransform)copy.transform;
			Toggle toggle = copy.GetComponentInChildren<Toggle>(true);
			toggle.onValueChanged = new Toggle.ToggleEvent();
			SetLabel(toggle.gameObject, label);
			return toggle;
		}

		// "Auto sync map" goes one row above "Visible to other players", or two while the game's own
		// "Cartography Table" row (Minimap.m_sharedMapHint) is showing: that row sits directly above
		// it and only appears once the map holds shared data, which the first sync brings. "Sync now"
		// goes one row above "Auto sync map". Called on every tick, so both move up when that row
		// appears.
		private static void Place()
		{
			RectTransform from = SourceRow();
			if (InLayoutGroup(from) || from.parent == null)
			{
				return;
			}
			float row = from.rect.height > 1f ? from.rect.height + 4f : 32f;
			int rows = 1;
			GameObject hint = Minimap.instance.m_sharedMapHint;
			if (hint != null && hint.transform is RectTransform hintRect)
			{
				// The real distance between the two rows, measured between their centres in the
				// parent's space so that different pivots do not matter.
				float step = from.parent.InverseTransformPoint(Centre(hintRect)).y - from.parent.InverseTransformPoint(Centre(from)).y;
				if (step > 1f)
				{
					row = step;
				}
				if (hint.activeSelf)
				{
					rows = 2;
				}
			}
			Vector2 offset = new Vector2(MapAutoSyncPlugin.ButtonOffsetX.Value, MapAutoSyncPlugin.ButtonOffsetY.Value);
			s_toggleRow.anchoredPosition = from.anchoredPosition + offset + new Vector2(0f, row * rows);
			s_syncNowRow.anchoredPosition = from.anchoredPosition + offset + new Vector2(0f, row * (rows + 1));
		}

		private static Vector3 Centre(RectTransform rect)
		{
			var corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			return (corners[0] + corners[2]) * 0.5f;
		}

		// For the log: where in the map screen an object sits.
		private static string Path(Transform transform)
		{
			var names = new List<string>();
			Transform root = Minimap.instance.m_largeRoot != null ? Minimap.instance.m_largeRoot.transform : null;
			for (Transform t = transform; t != null && names.Count < 8; t = t.parent)
			{
				names.Insert(0, t.name);
				if (t == root)
				{
					break;
				}
			}
			return string.Join("/", names);
		}

		// The label is a TextMeshPro or a legacy Text component depending on the game version; both
		// have a string "text" property, set here without tying the mod to either type.
		private static void SetLabel(GameObject root, string text)
		{
			foreach (Component component in root.GetComponentsInChildren<Component>(true))
			{
				if (component == null || component is Toggle)
				{
					continue;
				}
				var property = component.GetType().GetProperty("text");
				if (property != null && property.PropertyType == typeof(string) && property.CanWrite)
				{
					property.SetValue(component, text, null);
				}
			}
		}

		internal static void Remove()
		{
			if (s_toggleRow != null)
			{
				Object.Destroy(s_toggleRow.gameObject);
			}
			if (s_syncNowRow != null)
			{
				Object.Destroy(s_syncNowRow.gameObject);
			}
			MapBanner.Remove();
			s_toggle = null;
			s_syncNow = null;
			s_toggleRow = null;
			s_syncNowRow = null;
		}
	}
}
