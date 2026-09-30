using UnityEngine.UI;
using UnityEngine;

public class FightModeSwitch : MonoBehaviour
{
    [SerializeField] private BOButton btn;
    [SerializeField] private Text modeText;
    [SerializeField] private Animator animator;
    
    private TeamMode _teamMode;
    PreparationButtonSkin _preparationSkin;
    public TeamMode TeamMode => _teamMode;

    public void ApplyPreparationSkin()
    {
        if (_preparationSkin != null) return;
        _preparationSkin = PreparationButtonSkin.Apply(btn, modeText, new Color(0.42f, 0.65f, 0.73f, 0.75f), false, 28);
        if (_preparationSkin == null) return;
        animator.enabled = false;
        var oldBackground = btn.GetComponent<Image>();
        if (oldBackground != null) oldBackground.enabled = false;
    }
    
    void OnClick()
    {
        if (_teamMode == TeamMode.Rotation)
        {
            PlayerPrefs.SetInt("preferAdventureMode", 1);
            SetMode(TeamMode.MultiRaid);
        }
        else if (_teamMode == TeamMode.MultiRaid)
        {
            PlayerPrefs.SetInt("preferAdventureMode", 2);
            SetMode(TeamMode.Rotation);
        }
    }

    public void Setup(int arcadeFightMode, int defaultMode)
    {
        btn.onClick.RemoveAllListeners();
        btn.gameObject.SetActive(true);
        switch (arcadeFightMode)
        {
            case 1:
                btn.interactable = false;
                animator.enabled = false;
                SetMode(TeamMode.MultiRaid);
            break;
            case 2:
                btn.interactable = false;
                animator.enabled = false;
                SetMode(TeamMode.Rotation);
            break;
            case 3:
                btn.gameObject.SetActive(false);
                SetMode(TeamMode.Rotation);
            break;
            default:
                btn.onClick.AddListener(OnClick);
                btn.interactable = true;
                animator.enabled = _preparationSkin == null;
                SetMode(defaultMode == (int)TeamMode.MultiRaid ? TeamMode.MultiRaid : TeamMode.Rotation);
            break;
        }
        _preparationSkin?.RefreshState();
    }

    void SetMode(TeamMode mode)
    {
        _teamMode = mode;
        if (_teamMode == TeamMode.Rotation)
        {
            modeText.text = Translate.Get("TeamModeR");
        }
        if (_teamMode == TeamMode.MultiRaid)
        {
            modeText.text = Translate.Get("TeamModeM");
        }
    }
}
