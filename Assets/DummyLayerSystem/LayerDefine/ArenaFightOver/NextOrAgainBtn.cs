using System;
using UnityEngine;
using UnityEngine.UI;

public class NextOrAgainBtn : MonoBehaviour
{
    [SerializeField] private RectTransform wholeRect;
    [SerializeField] private Text stageTitle;
    [SerializeField] private RectTransform modeRoot;
    [SerializeField] private BOButton againFor1v1Btn;
    [SerializeField] private BOButton againForMultiBtn;
    [SerializeField] private BOButton againBtn;
    
    [SerializeField] private float normalAgainBtnWidth = 250;
    [SerializeField] private float longerAgainBtnWidth = 400;

    public void SetUp(int fightMode, string title)
    {
        stageTitle.text = title;
        var showAgainFor1v1Btn = fightMode is 0 or 2;
        var showAgainForMultiBtn = fightMode is 0 or 1;
        againFor1v1Btn.gameObject.SetActive(showAgainFor1v1Btn);
        againForMultiBtn.gameObject.SetActive(showAgainForMultiBtn);
        modeRoot.gameObject.SetActive(showAgainFor1v1Btn || showAgainForMultiBtn);
        wholeRect.sizeDelta = new Vector2(
            (showAgainFor1v1Btn || showAgainForMultiBtn)? longerAgainBtnWidth : normalAgainBtnWidth,
            wholeRect.sizeDelta.y);
    }

    public void SetUpAction(Action mainAction, Action vsAction = null, Action multiAction = null)
    {
        againBtn.SetListener(mainAction);
        againFor1v1Btn.SetListener(vsAction);
        againForMultiBtn.SetListener(multiAction);
    }
}
