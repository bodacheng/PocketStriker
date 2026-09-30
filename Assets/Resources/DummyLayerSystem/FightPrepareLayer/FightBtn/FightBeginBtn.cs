using System;
using UnityEngine;

public class FightBeginBtn : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private BOButton btn;
    PreparationButtonSkin _preparationSkin;
    bool _guide;

    public void ApplyPreparationSkin()
    {
        if (_preparationSkin != null) return;
        var label = transform.Find("text").GetComponent<UnityEngine.UI.Text>();
        // The actual selectable owns the new graphics, so the entire new
        // rectangle remains a hit target instead of the old circular area.
        PreparationButtonSkin.Fit((RectTransform)btn.transform);
        label.transform.SetParent(btn.transform, false);
        _preparationSkin = PreparationButtonSkin.Apply(btn, label, new Color(0.95f, 0.72f, 0.31f), true, 40);
        if (_preparationSkin == null) return;
        animator.enabled = false;
        foreach (var image in GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if (image.name != "PreparationFill" && image.name != "PreparationOutline") image.enabled = false;
        ((RectTransform)transform).sizeDelta = new Vector2(416, 104);
        _preparationSkin.SetGuide(_guide);
    }

    public void SetAction(Action action)
    {
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action.Invoke);
    }
    
    public void Enable(bool on, bool guide = false)
    {
        btn.interactable = on;
        _guide = guide;
        if (_preparationSkin != null) _preparationSkin.SetGuide(guide);
        else
        {
            animator.SetBool("On", on);
            animator.SetBool("Guide", guide);
        }
    }
}
