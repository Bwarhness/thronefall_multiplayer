using TMPro;
using UnityEngine;

namespace ThronefallMP.UI;

// Small text strip parented into the loadout grid frame showing each player's weapon pick:
//   "GenoC: Long Bow   ·   Too Damn FilthY: —"
// Created lazily on first Show(); it lives inside the frame so it shows/hides with it, but is also
// toggled explicitly so single-player popups don't show an MP strip. The frame (and thus the strip)
// is destroyed on scene change; the Unity-null check rebuilds it lazily.
public static class LoadoutStatusStrip
{
    private static GameObject _root;
    private static TextMeshProUGUI _text;

    public static void Show()
    {
        if (_root == null)
        {
            var frame = LoadoutFrames.PrimaryGridFrame;
            if (frame == null)
            {
                return;
            }

            _root = new GameObject("MP Loadout Status", typeof(RectTransform));
            _root.transform.SetParent(frame.transform, false);
            var rect = (RectTransform)_root.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(1400f, 40f);

            _text = _root.AddComponent<TextMeshProUGUI>();
            // Steal the font from an existing label in the frame so it matches the game's style.
            var existing = frame.GetComponentInChildren<TMP_Text>(true);
            if (existing != null)
            {
                _text.font = existing.font;
            }
            _text.fontSize = 24f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.color = Color.white;
            _text.raycastTarget = false;
        }

        _root.SetActive(true);
    }

    public static void Hide()
    {
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    public static void SetText(string value)
    {
        if (_text != null)
        {
            _text.text = value;
        }
    }
}
