using UnityEngine;
using UnityEngine.UI;

namespace AutoSyncMap
{
	/*
		The "Auto sync map" tick-box on the large map.

		It is a copy of the game's own "Visible to other players" tick-box (Minimap.m_publicPosition),
		so it looks and behaves like it, placed one row above. The copy's click handler is replaced:
		the original's is set in the prefab and would toggle public position.
	*/
	internal static class MapToggle
	{
		private static Toggle s_toggle;

		internal static void Ensure()
		{
			if (s_toggle != null || Minimap.instance == null || Minimap.instance.m_publicPosition == null)
			{
				return;
			}
			Toggle source = Minimap.instance.m_publicPosition;
			GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
			copy.name = "AutoSyncMapToggle";

			RectTransform from = source.GetComponent<RectTransform>();
			RectTransform to = copy.GetComponent<RectTransform>();
			float row = from.rect.height > 1f ? from.rect.height + 4f : 32f;
			to.anchoredPosition = from.anchoredPosition
				+ new Vector2(AutoSyncMapPlugin.ButtonOffsetX.Value, row + AutoSyncMapPlugin.ButtonOffsetY.Value);

			s_toggle = copy.GetComponent<Toggle>();
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
