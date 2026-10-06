using UnityEngine;
using UnityEngine.UI;

namespace AutoSyncMap
{
	/*
		The "Auto sync map" tick-box and the "Sync now" button on the large map.

		Both are copies of the game's own "Visible to other players" tick-box (Minimap.m_publicPosition),
		so they look and behave like it, placed above it (see Place). The copies' click handlers are
		replaced: the original's is set in the prefab and would toggle public position.

		"Sync now" is a tick-box used as a button: a click asks for a sync, and the tick shows while
		it is under way, clearing when the server's map has been merged.
	*/
	internal static class MapToggle
	{
		private const float SyncNowTimeout = 10f;
		private static Toggle s_toggle;
		private static Toggle s_syncNow;
		private static float s_syncNowUntil;

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
					s_syncNow.SetIsOnWithoutNotify(false);
				}
				return;
			}
			Remove();

			s_toggle = Copy("AutoSyncMapToggle", AutoSyncMapPlugin.ButtonLabel.Value);
			s_toggle.isOn = AutoSyncMapPlugin.AutoSync.Value;
			s_toggle.onValueChanged.AddListener(on =>
			{
				AutoSyncMapPlugin.AutoSync.Value = on; // saved to the config file
				if (on)
				{
					ClientSync.RequestNow();
				}
			});

			s_syncNow = Copy("AutoSyncMapSyncNow", AutoSyncMapPlugin.SyncNowLabel.Value);
			s_syncNow.isOn = false;
			s_syncNow.onValueChanged.AddListener(on =>
			{
				if (on)
				{
					s_syncNowUntil = Time.time + SyncNowTimeout;
					ClientSync.RequestNow();
				}
			});
			Place();
			AutoSyncMapPlugin.Log.LogInfo("added the Auto sync map tick-box and the Sync now button to the large map");
		}

		// The server's map has been merged: clear the tick on "Sync now".
		internal static void SyncDone()
		{
			if (s_syncNow != null)
			{
				s_syncNow.SetIsOnWithoutNotify(false);
			}
		}

		private static Toggle Copy(string name, string label)
		{
			Toggle source = Minimap.instance.m_publicPosition;
			GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
			copy.name = name;
			Toggle toggle = copy.GetComponent<Toggle>();
			toggle.onValueChanged = new Toggle.ToggleEvent();
			SetLabel(copy, label);
			return toggle;
		}

		// "Auto sync map" goes one row above "Visible to other players", or two while the game's own
		// "Cartography Table" row (Minimap.m_sharedMapHint) is showing: that row sits directly above
		// it and only appears once the map holds shared data, which the first sync brings. "Sync now"
		// goes one row above "Auto sync map". Called on every tick, so both move up when that row
		// appears.
		private static void Place()
		{
			RectTransform from = Minimap.instance.m_publicPosition.GetComponent<RectTransform>();
			RectTransform to = s_toggle.GetComponent<RectTransform>();
			float row = from.rect.height > 1f ? from.rect.height + 4f : 32f;
			int rows = 1;
			GameObject hint = Minimap.instance.m_sharedMapHint;
			if (hint != null && hint.activeSelf && hint.transform is RectTransform hintRect && to.parent != null)
			{
				// The real distance between the two rows, measured between their centres in the
				// parent's space so that different pivots do not matter.
				float step = to.parent.InverseTransformPoint(Centre(hintRect)).y - to.parent.InverseTransformPoint(Centre(from)).y;
				if (step > 1f)
				{
					row = step;
				}
				rows = 2;
			}
			Vector2 offset = new Vector2(AutoSyncMapPlugin.ButtonOffsetX.Value, AutoSyncMapPlugin.ButtonOffsetY.Value);
			to.anchoredPosition = from.anchoredPosition + offset + new Vector2(0f, row * rows);
			s_syncNow.GetComponent<RectTransform>().anchoredPosition = from.anchoredPosition + offset + new Vector2(0f, row * (rows + 1));
		}

		private static Vector3 Centre(RectTransform rect)
		{
			var corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			return (corners[0] + corners[2]) * 0.5f;
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
			if (s_toggle != null)
			{
				Object.Destroy(s_toggle.gameObject);
				s_toggle = null;
			}
			if (s_syncNow != null)
			{
				Object.Destroy(s_syncNow.gameObject);
				s_syncNow = null;
			}
		}
	}
}
