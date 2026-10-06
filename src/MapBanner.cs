using TMPro;
using UnityEngine;

namespace MapAutoSync
{
	/*
		What the mod has to say to the player, where the player is looking.

		While the large map is open the game's own messages are easy to miss: the top-left ones are
		small and sit in the corner beside the map. So with the map open the text goes on the map
		itself instead, large, at the top in the middle. It is a copy of the label of the game's
		"Visible to other players" tick-box, so it has the game's font and colour.

		With the map closed, the game's own messages are used.
	*/
	internal static class MapBanner
	{
		private const float Seconds = 4f;
		private static TMP_Text s_text;
		private static float s_until;

		// Shows the text on the large map if it is open; otherwise as a game message of the given kind.
		internal static void Show(string text, MessageHud.MessageType otherwise)
		{
			if (OnMap(text))
			{
				return;
			}
			if (Player.m_localPlayer != null)
			{
				Player.m_localPlayer.Message(otherwise, text);
			}
		}

		private static bool OnMap(string text)
		{
			Minimap map = Minimap.instance;
			if (map == null || map.m_largeRoot == null || !map.m_largeRoot.activeInHierarchy || map.m_publicPosition == null)
			{
				return false;
			}
			if (s_text == null)
			{
				TMP_Text label = map.m_publicPosition.GetComponentInChildren<TMP_Text>(true);
				if (label == null)
				{
					return false;
				}
				s_text = Object.Instantiate(label, map.m_largeRoot.transform);
				s_text.name = "MapAutoSyncBanner";
				RectTransform rect = s_text.rectTransform;
				rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
				rect.pivot = new Vector2(0.5f, 1f);
				rect.anchoredPosition = new Vector2(0f, -MapAutoSyncPlugin.BannerOffset.Value);
				rect.sizeDelta = new Vector2(1200f, 80f);
				s_text.alignment = TextAlignmentOptions.Center;
				s_text.enableAutoSizing = false;
				s_text.fontSize = MapAutoSyncPlugin.BannerSize.Value;
				s_text.raycastTarget = false; // never in the way of clicks on the map
			}
			s_text.text = text;
			s_text.transform.SetAsLastSibling(); // drawn over the map and everything else on it
			s_text.gameObject.SetActive(true);
			s_until = Time.time + Seconds;
			return true;
		}

		// Called every frame on a client.
		internal static void Frame()
		{
			if (s_text != null && s_text.gameObject.activeSelf && Time.time >= s_until)
			{
				s_text.gameObject.SetActive(false);
			}
		}

		internal static void Remove()
		{
			if (s_text != null)
			{
				Object.Destroy(s_text.gameObject);
				s_text = null;
			}
		}
	}
}
