using UnityEngine;
using UnityEngine.UI;

// Keeps the switch art in sync with the native Toggle, including silent preference restores.
[DisallowMultipleComponent, RequireComponent(typeof(Toggle))]
public sealed class BattleCameraTogglePresentation : MonoBehaviour
{
    [SerializeField] Image track;
    [SerializeField] Image outline;
    [SerializeField] RectTransform thumb;
    Toggle toggle;
    bool displayedOn;
    bool displayedInteractable;

    void OnEnable()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnValueChanged);
        RefreshVisuals();
    }

    void OnDisable()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnValueChanged);
    }

    void OnValueChanged(bool _) => RefreshVisuals();

    void LateUpdate()
    {
        if (toggle != null && (displayedOn != toggle.isOn || displayedInteractable != toggle.IsInteractable()))
            RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        if (toggle == null) toggle = GetComponent<Toggle>();
        displayedOn = toggle.isOn;
        displayedInteractable = toggle.IsInteractable();
        float alpha = displayedInteractable ? 1f : 0.45f;
        track.color = displayedOn ? new Color(0.04f, 0.55f, 0.65f, alpha) : new Color(0.09f, 0.16f, 0.21f, alpha);
        outline.color = displayedOn ? new Color(0.30f, 0.90f, 0.96f, alpha) : new Color(0.39f, 0.54f, 0.60f, alpha);
        var anchor = new Vector2(displayedOn ? 1f : 0f, 0.5f);
        thumb.anchorMin = thumb.anchorMax = anchor;
        thumb.anchoredPosition = new Vector2(displayedOn ? -44f : 44f, 0);
    }
}
