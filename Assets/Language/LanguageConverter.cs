using UnityEngine;
using UnityEngine.UI;

public class LanguageConverter : MonoBehaviour
{
    public string languageCode;
    public Text target;
    
    void Awake()
    {
        if (!LanguageConverterManger.List.Contains(this))
        {
            LanguageConverterManger.List.Add(this);
        }
        if (target == null)
            target = transform.GetComponent<Text>();
    }

    public void ChangeAtOnce(string languageCode)
    {
        this.languageCode = languageCode;
        Change();
    }

    void Start()
    {
        Change();
    }

    public void Change()
    {
        if (target != null && !string.IsNullOrEmpty(languageCode))
        {
            Translate.Row row = Translate.Find_RECORD_ID(languageCode);
            if (row != null)
            {
                switch (AppSetting.Value.Language)
                {
                    case SystemLanguage.English:
                        target.text = row.EN;
                        break;
                    case SystemLanguage.Japanese:
                        target.text = row.JP;
                        break;
                    case SystemLanguage.Chinese:
                        target.text = row.CH;
                        break;
                }
            }
        }
    }
}
