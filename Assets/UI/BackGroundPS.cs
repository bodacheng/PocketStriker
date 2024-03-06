using System.Collections.Generic;
using UnityEngine;

public class BackGroundPS : MonoBehaviour
{
    [SerializeField] List<GameObject> BCPs;
    public static BackGroundPS target;
    
    int playingNo = 0;
    
    void Awake()
    {
        target = this;
    }
    
    public void Next()
    {
        playingNo++;
        if (playingNo == BCPs.Count)
        {
            playingNo = 0;
        }
        SwitchBG(playingNo);
    }
    
    public void Off()
    {
        SwitchBG(-1);
    }
    
    public void ChangeBGByElement(Element element)
    {
        switch (element)
        {
            case Element.darkMagic:
                Dark();
            break;
            case Element.blueMagic:
                Blue();
            break;
            case Element.greenMagic:
                Green();
            break;
            case Element.lightMagic:
                Light();
            break;
            case Element.redMagic:
                Red();
            break;
            default:
                Void();
            break;
        }
    }
    
    void Red()
    {
        SwitchBG(0);
    }
    
    void Blue()
    {
        SwitchBG(2);
    }
    
    void Green()
    {
        SwitchBG(1);
    }
    
    void Light()
    {
        SwitchBG(3);
    }
    
    void Dark()
    {
        SwitchBG(4);
    }

    public void Void()
    {
        SwitchBG(5);
    }
    
    void SwitchBG(int index)
    {
        for (var i = 0; i < BCPs.Count; i++)
        {
            if (i == index)
            {
                if (BCPs[i].gameObject.activeSelf) continue;
                BCPs[i].gameObject.SetActive(true);
            }else{
                BCPs[i].gameObject.SetActive(false);
            }
        }
    }
}
