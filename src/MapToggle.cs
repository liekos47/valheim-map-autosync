using UnityEngine;
using UnityEngine.UI;

namespace AutoSyncMap
{
	/*
		The "Auto sync map" tick-box on the large map.

		It is a copy of the game's own "Visible to other players" tick-box (Minimap.m_publicPosition),
		so it looks and behaves like it, placed above it (see Place). The copy's click handler is replaced:
		the original's is set in the prefab and would toggle public position.
	*/
	internal static class MapToggle
	{
		private static Toggle s_toggle;

		internal static void Ensure()
		{
			if (Minimap.instance == null || Minimap.instance.m_publicPosition == null)
			{
				return;
			}
			if (s_toggle != null)
			{
				Place();
				return;
			}
			Toggle source = Minimap.instance.m_publicPosition;
			GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
			copy.name = "AutoSyncMapToggle";

			s_toggle = copy.GetComponent<Toggle>();
			Place();
			s_toggle.onValueChanged = new Toggle.ToggleEvent();
			s_toggle.isOn = AutoSyncMapPlugin.AutoSync.Value;
			s_toggle.onValueChanged.AddListener(on =>
			{
				AutoSyncMapPlugin.AutoSync.Value = on; // saved to the config file
				if (on)
				{
					ClientSync.RequestNow();
				}
			});
			SetLabel(copy, AutoSyncMapPlugin.ButtonLabel.Value);
			AutoSyncMapPlugin.Log.LogInfo("added the Auto sync map tick-box to the large map");
		}

		// One row above "Visible to other players", or two while the game's own "Cartography Table"
		// row (Minimap.m_sharedMapHint) is showing: that row sits directly above it and only appears
		// once the map holds shared data, which the first sync brings. Called on every tick, so the
		// tick-box moves up when that row appears.
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
			to.anchoredPosition = from.anchoredPosition
				+ new Vector2(AutoSyncMapPlugin.ButtonOffsetX.Value, row * rows + AutoSyncMapPlugin.ButtonOffsetY.Value);
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
		}
	}
}
