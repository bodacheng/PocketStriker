using dataAccess;
using UnityEngine;
using UnityEngine.UI;

public class StoneContentCell : MonoBehaviour
{
    [SerializeField] private RectTransform stoneIconParent;
    [SerializeField] private Text skillName;
    [SerializeField] private string skillId;
    
    void Start()
    {
        Render();
    }

    async void Render()
    {
        skillName.text = SkillNameTable.GetSkillName(skillId);
        var icon = await Stones.GenerateStoneModel(skillId, false);
        if (this == null)
        {
            return;
        }
        icon.transform.SetParent(stoneIconParent);
        icon.gameObject.SetActive(true);
        icon.transform.localPosition = Vector3.zero;
        icon.transform.localScale = Vector3.one;
        icon.transform.GetComponent<RectTransform>().sizeDelta = stoneIconParent.transform.GetComponent<RectTransform>().sizeDelta;
    }
}
