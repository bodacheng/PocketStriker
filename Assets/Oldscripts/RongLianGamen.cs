//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;
//using UnityEngine.SceneManagement;
//using System.Xml;
//using System.Xml.Serialization;
//using System;
//using System.Linq;
//using EZObjectPools;
//using mainMenu;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//public class RongLianGamen : MonoBehaviour {

    //public preparingScene _preparingScene;
    //public CameraManager _CameraManager;
    //public CharsManager _CharSetManager;

    //[Header("RongLian——GUI")]
    //[Space(5)]
    //public Text RongLian_monsterName;
    ////public UIBulletBar RongLianExpTiao;
    //public Text RonglianChoosingAILevel;
    //public Button ConfirmButton;
    //public RectTransform RonglianMonsterBox;
    //public Text RongLianInstruction;
    //public Transform RonglianBaseStand;
    //public Transform RonglianBaseWatch;
    //public Transform RonglianMaterielStand;
    //public Transform RonglianMaterielWatch;
    //public Transform FaceToSky;

    //[Header("RongLianConfirm——GUI")]
    //[Space(5)]
    //public Text materialName;
    //public Text baseName;
    //public Button RongLianFinalConfirm;

    //int _materialCharLocalID = -1, BaseCharLocalID = -1;//实际上这个环节光是靠角色的localID也行。localID其实是个“保存的出错了也没事儿”的信息
    //GameObject showingMChar,showingBChar;

    //public CharacterDataInfo processingCharInfo;

    //void Start()
    //{
    //    //GUIRefresh();
    //    processingCharInfo = null;
    //}

    //public void GUIRefresh() // 模块按理说应该是总体更新  
    //{
    //    foreach (KeyValuePair<int, GameObject> _pair in myModelPool.Instance.ModelDicBasedOnPlayerLocalID)
    //    {
    //        _pair.Value.SetActive(false);
    //    }
        
    //    if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_selectMaterialMonster)
    //    {
    //        this._CameraManager.Assign_Camera(Camera_Mode_Num.LockCamera);
    //        this._CameraManager.current_Camera_Mode.targets = new List<Transform>() { RonglianMaterielWatch };

    //        if (processingCharInfo != null && processingCharInfo.localID != -1)
    //        {
    //            showingMChar = myModelPool.Instance.getMyModel(processingCharInfo.localID);
    //            if (showingMChar != null)
    //            {
    //                showingMChar.SetActive(true);
    //                showingMChar.transform.SetParent(RonglianMaterielStand);
    //                showingMChar.transform.localPosition = new Vector3(0, 0, 0);
    //                showingMChar.transform.localRotation = new Quaternion();
    //            }
    //        }
    //    }
    //    else if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_selectBaseMonster)
    //    {
    //        this._CameraManager.Assign_Camera(Camera_Mode_Num.LockCamera);
    //        this._CameraManager.current_Camera_Mode.targets = new List<Transform>() { RonglianBaseWatch };

    //        if (processingCharInfo != null && processingCharInfo.localID != -1)
    //        {
    //            showingBChar = myModelPool.Instance.getMyModel(processingCharInfo.localID);
    //            if (showingBChar != null)
    //            {
    //                showingBChar.SetActive(true);
    //                showingBChar.transform.SetParent(RonglianBaseStand);
    //                showingBChar.transform.localPosition = new Vector3(0, 0, 0);
    //                showingBChar.transform.localRotation = new Quaternion();
    //            }
    //        }
    //    }

    //    ConfirmButton.gameObject.SetActive(false);

    //    if (processingCharInfo != null)
    //    {
    //        RongLian_monsterName.gameObject.SetActive(true);
    //        RonglianChoosingAILevel.gameObject.SetActive(true);
    //        //RongLianExpTiao.gameObject.SetActive(true);

    //        CharacterResourceInfo _CharacterResourceInfo = MonsterConfigInfos._monstersConfigTable.RowToCharacterResourceInfo(
    //            MonsterConfigInfos._monstersConfigTable.Find_ID(processingCharInfo.resource_num.ToString())
    //        );
    //        if (_CharacterResourceInfo != null)
    //            RongLian_monsterName.text = _CharacterResourceInfo.showNameEN;
    //        ConfirmButton.gameObject.SetActive(true);
    //    }else{
    //        RongLian_monsterName.gameObject.SetActive(false);
    //        RonglianChoosingAILevel.gameObject.SetActive(false);
    //        //RongLianExpTiao.gameObject.SetActive(false);
    //        ConfirmButton.gameObject.SetActive(false);
    //        if (FaceToSky != null)
    //        {
    //            this._CameraManager.Assign_Camera(Camera_Mode_Num.LockCamera);
    //            this._CameraManager.current_Camera_Mode.targets = new List<Transform>() { FaceToSky };
    //        }
    //    }
    //}

    //public void RonglianVersionMonsterIcon(CharacterDataInfo _CharacterDataInfo)
    //{
    //    this.processingCharInfo = _CharacterDataInfo;
    //    if (BaseCharLocalID ==  _CharacterDataInfo.localID || _materialCharLocalID == _CharacterDataInfo.localID)
    //    {            
    //        return; // 不能选一样的。
    //    }
    //}

    //public bool canRongLianNow()
    //{
    //    if (BaseCharLocalID != -1 && _materialCharLocalID != -1)
    //    {
    //        return true;
    //    }
    //    else
    //        return false;
    //}

    //IEnumerator RonglianAnimation()
    //{
    //    //_preparingScene._littleWindowSwap.showThisLittleWindow("JiNengRongLian_select");
    //    RonglianMonsterBox.gameObject.SetActive(false);
    //    if (RonglianBaseWatch != null)
    //    {
    //        this._CameraManager.Assign_Camera(Camera_Mode_Num.LockCamera);
    //        this._CameraManager.current_Camera_Mode.targets = new List<Transform>() { RonglianBaseWatch };
    //    }
    //    yield return new WaitForSeconds(2f);

    //    if (RongLianInstruction != null)
    //    {
    //        RongLianInstruction.text = "熔炼动画";
    //    }

    //    if (this.BaseCharLocalID != -1)
    //    {
    //        CharacterDataInfo baseMonster = AccountCharsSet.instance.getTheCharacterOfMine(BaseCharLocalID);
    //        CharacterResourceInfo _CharacterResourceInfo = MonsterConfigInfos._monstersConfigTable.RowToCharacterResourceInfo(
    //            MonsterConfigInfos._monstersConfigTable.Find_ID(baseMonster.resource_num.ToString())
    //        );

    //        if (_CharacterResourceInfo != null)
    //            RongLian_monsterName.text = _CharacterResourceInfo.showNameEN;
    //    }
    //    else
    //    {
    //        this.RongLian_monsterName.text = null;
    //        this.RonglianChoosingAILevel.text = "";
    //        //RongLianExpTiao.fillAmount = 0f;
    //    }

    //    ConfirmButton.onClick.RemoveAllListeners();
    //    ConfirmButton.gameObject.SetActive(false);
    //    yield break;
    //}

    //public void RongLianReturn() //这是一个直接在editor给返回按钮添加的函数。 
    //{
    //    if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_selectMaterialMonster)
    //    {
    //        _preparingScene.trySwitchToStep(MainSceneStep.TeamEditFront,true);
    //        this._materialCharLocalID = -1;
    //    }
    //    else if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_selectBaseMonster)
    //    {
    //        if (_materialCharLocalID != -1)
    //        {
    //            processingCharInfo = AccountCharsSet.instance.getTheCharacterOfMine(_materialCharLocalID);
    //        }
    //        _preparingScene.trySwitchToStep(MainSceneStep.JiNengRongLian_selectMaterialMonster, true);
    //    }
    //    else if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_waitForConfirm)
    //    {
    //        if (BaseCharLocalID != -1)
    //        {
    //            processingCharInfo = AccountCharsSet.instance.getTheCharacterOfMine(BaseCharLocalID);
    //        }
    //        _preparingScene.trySwitchToStep(MainSceneStep.JiNengRongLian_selectBaseMonster, true);
    //    }
    //}

    //public void MBProcessConfirm()
    //{
    //    if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_selectMaterialMonster)
    //    {
    //        if (processingCharInfo != null)
    //            _materialCharLocalID = processingCharInfo.localID;

    //        if (showingMChar)
    //            showingMChar.SetActive(false);
    //        if (showingBChar)
    //            showingBChar.SetActive(false);
            
    //        _preparingScene.trySwitchToStep(MainSceneStep.JiNengRongLian_selectBaseMonster, true);

    //        if (BaseCharLocalID != -1)
    //        {
    //            processingCharInfo = AccountCharsSet.instance.getTheCharacterOfMine(BaseCharLocalID);
    //        }
    //        GUIRefresh();
    //    }
    //    else if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_selectBaseMonster)
    //    {
    //        if (processingCharInfo != null)
    //            BaseCharLocalID = processingCharInfo.localID;
            
    //        if (showingMChar)
    //            showingMChar.SetActive(false);
    //        if (showingBChar)
    //            showingBChar.SetActive(false);
            
    //        _preparingScene.trySwitchToStep(MainSceneStep.JiNengRongLian_waitForConfirm, true);
    //        processingCharInfo = null;
    //    }
    //    else if (_preparingScene.processesRunner.currentProcess.thisProcessStep == MainSceneStep.JiNengRongLian_waitForConfirm)
    //    {
    //        showingMChar.SetActive(false);
    //        //showingBChar.SetActive(false);
    //        //AccountCharsSet.Instance.SkillRONGLIAN(BaseCharLocalID, _materialCharLocalID);
    //        //AccountCharsSet.Instance.overrideMyCharsInfo();
    //        //_preparingScene.trySwitchToStep("TeamEditFront");//可能没那么快，有个播放动画的问题
    //        //_preparingScene.step = "playingRongLianAnimation";
    //        //_preparingScene.MenuProcess = StartCoroutine(RonglianAnimation());
    //    }
    //}

    //public void trySwitchToWaitForConfirm(IDictionary<int, GameObject> ModelDicBasedOnLocalID)
    //{
    //    this._CameraManager.Assign_Camera(Camera_Mode_Num.LockCamera);
    //    this._CameraManager.current_Camera_Mode.targets = new List<Transform>() { _preparingScene.TeamEditWatchPoint };

    //    foreach (KeyValuePair<int, GameObject> _pair in ModelDicBasedOnLocalID)
    //    {
    //        if (_pair.Value)
    //            _pair.Value.SetActive(false);
    //    }

    //    showingMChar = myModelPool.Instance.getMyModel(_materialCharLocalID);
    //    showingBChar = myModelPool.Instance.getMyModel(BaseCharLocalID);

    //    if (showingBChar)
    //    {
    //        showingBChar.SetActive(true);
    //        showingBChar.transform.SetParent(RonglianBaseStand);
    //        showingBChar.transform.localPosition = Vector3.zero;
    //        showingBChar.transform.localRotation = new Quaternion();
    //    }
    //    if (showingMChar)
    //    {
    //        showingMChar.SetActive(true);
    //        showingMChar.transform.SetParent(RonglianMaterielStand);
    //        showingMChar.transform.localPosition = Vector3.zero;
    //        showingMChar.transform.localRotation = new Quaternion();
    //    }

    //    CharacterDataInfo baseMonster = AccountCharsSet.instance.getTheCharacterOfMine(BaseCharLocalID);

    //    CharacterResourceInfo _CharacterResourceInfo = MonsterConfigInfos._monstersConfigTable.RowToCharacterResourceInfo(
    //        MonsterConfigInfos._monstersConfigTable.Find_ID(baseMonster.resource_num.ToString())
    //    );
    //    if (_CharacterResourceInfo != null)
    //        baseName.text = _CharacterResourceInfo.showNameEN;

    //    CharacterDataInfo materialMonster = AccountCharsSet.instance.getTheCharacterOfMine(_materialCharLocalID);
    //    CharacterResourceInfo _CharacterResourceInfoofM = MonsterConfigInfos._monstersConfigTable.RowToCharacterResourceInfo(
    //        MonsterConfigInfos._monstersConfigTable.Find_ID(materialMonster.resource_num.ToString())
    //    );

    //    if (_CharacterResourceInfoofM != null)
    //        materialName.text = _CharacterResourceInfoofM.showNameEN;

    //    UnityEngine.Events.UnityAction validation = () =>
    //    {
    //        _preparingScene._LoadingCanvas.arrangeValiationWindow(MBProcessConfirm, "确实要进行技能交换？");
    //    };
    //    RongLianFinalConfirm.onClick.RemoveAllListeners();
    //    RongLianFinalConfirm.onClick.AddListener(validation);
    //    return;
    //}

    //这个，和底下那个selectBaseMonsterGUIRefresh，貌似都是说去根据当前熔炼界面但操作情况去更新页面？我们希望这个环节做的事情全部都是显示类更新而无任何致命操作，
    //并且由于牵扯到了_CharSetManager模块所以它也应该是个协程
    //public IEnumerator selectMaterialMonsterGUIRefresh()
    //{
    //    if (this._materialCharLocalID != -1)
    //    {
    //        CharacterDataInfo materialMonster = TeamSet.Instance.localCustomerInfo.getTheCharacterOfMine(_materialCharLocalID);
    //        if (materialMonster != null)
    //        {
    //            RongLian_monsterName.text =
    //                _CharSetManager.getCharDataBaseSetByType(materialMonster.type)._CharResourceDataBase.GetByID(materialMonster.resource_num).showName;
    //        }
    //    }
    //    if (this._materialCharLocalID != -1 && this.materialCharAINum != -1)
    //    {
    //        CharacterDataInfo materialMonster = TeamSet.Instance.localCustomerInfo.getTheCharacterOfMine(_materialCharLocalID);
    //        RongLian_monsterName.text = 
    //            _CharSetManager.getCharDataBaseSetByType(materialMonster.type)._CharResourceDataBase.GetByID(materialMonster.resource_num).showName;
    //        UnityEngine.Events.UnityAction toBaseSelect = () =>
    //        {
    //            this.BaseCharLocalID = -1;
    //            this.toOverrideAINum = -1;

    //            RonglianAI1Button.GetComponentInChildren<Text>().text = null;
    //            RonglianAI2Button.GetComponentInChildren<Text>().text = null;

    //            this.RongLian_monsterName.text = null;
    //            if (showingMChar)
    //                showingMChar.SetActive(false);
    //            this.RonglianChoosingAILevel.text = "";
    //            RongLianExpTiao.fillAmount = 0f;
    //            _preparingScene.trySwitchToStep("JiNengRongLian_selectBaseMonster");
    //        };
    //        ConfirmButton.onClick.RemoveAllListeners();
    //        ConfirmButton.onClick.AddListener(toBaseSelect);
    //        ConfirmButton.gameObject.SetActive(true);
    //    }
    //    else
    //    {
    //        ConfirmButton.gameObject.SetActive(false);
    //    }

    //    UnityEngine.Events.UnityAction MaterialSelectStepReturnBUtton = () =>
    //    {
    //        _preparingScene.trySwitchToStep("TeamEditFront");
    //    };
    //    ProcessReturnButton.onClick.RemoveAllListeners();
    //    ProcessReturnButton.onClick.AddListener(MaterialSelectStepReturnBUtton);
    //    yield break;
    //}

    //public IEnumerator selectBaseMonsterGUIRefresh()
    //{
    //    if (RongLianInstruction != null)
    //    {
    //        RongLianInstruction.text = "选择要被覆盖技能的怪";
    //    }
    //    if (this.BaseCharLocalID != -1)
    //    {
    //        CharacterDataInfo baseMonster = TeamSet.Instance.localCustomerInfo.getTheCharacterOfMine(BaseCharLocalID);
    //        if (baseMonster != null)
    //        {
    //            GUIRefresh(baseMonster);
    //        }
    //    }
    //    else
    //    {
    //        GUIRefresh(null);
    //    }

    //    if (BaseCharLocalID != -1 && toOverrideAINum != -1)
    //    {
    //        CharacterDataInfo baseMonster = TeamSet.Instance.localCustomerInfo.getTheCharacterOfMine(BaseCharLocalID);
    //        UnityEngine.Events.UnityAction toConfirm = () =>
    //        {
    //            _preparingScene.trySwitchToStep("JiNengRongLian_waitForConfirm");
    //        };
    //        ConfirmButton.onClick.RemoveAllListeners();
    //        ConfirmButton.onClick.AddListener(toConfirm);
    //        ConfirmButton.gameObject.SetActive(true);
    //    }
    //    else
    //    {
    //        ConfirmButton.gameObject.SetActive(false);
    //        Debug.Log("材料宠物都没选你点个鸡巴确认");
    //    }

    //    UnityEngine.Events.UnityAction MaterialSelectStepReturnBUtton = () =>
    //    {
    //        _preparingScene.trySwitchToStep("JiNengRongLian_selectMaterialMonster");
    //    };
    //    ProcessReturnButton.onClick.RemoveAllListeners();
    //    ProcessReturnButton.onClick.AddListener(MaterialSelectStepReturnBUtton);
    //    yield break;
    //}

    //void setRongLianBase(CharacterDataInfo m)
    //{
    //    this.BaseCharLocalID = m.localID;
    //    setRongLianBaseAIID(-1);
    //    StartCoroutine(selectBaseMonsterGUIRefresh());
    //}
    //void setRongLianBaseAIID(int AIID)
    //{
    //    this.toOverrideAINum = AIID;
    //    CharacterDataInfo _CharacterDataInfo = TeamSet.Instance.localCustomerInfo.getTheCharacterOfMine(this.BaseCharLocalID);
    //    if (_CharacterDataInfo == null)
    //    {
    //        Debug.Log("还没选basemonster");
    //        return;
    //    }
    //    if (this.toOverrideAINum == 1)
    //    {
    //        RonglianAI1Button.GetComponent<Image>().sprite = ChoosingAISprite;
    //        RonglianAI2Button.GetComponent<Image>().sprite = unchosenAISprite;
    //    }
    //    if (this.toOverrideAINum == 2)
    //    {
    //        RonglianAI1Button.GetComponent<Image>().sprite = unchosenAISprite;
    //        RonglianAI2Button.GetComponent<Image>().sprite = ChoosingAISprite;
    //    }
    //    if (this.toOverrideAINum == -1)
    //    {
    //        RonglianAI1Button.GetComponent<Image>().sprite = unchosenAISprite;
    //        RonglianAI2Button.GetComponent<Image>().sprite = unchosenAISprite;
    //        this.RonglianChoosingAILevel.text = "";
    //        RongLianExpTiao.fillAmount = 0f;
    //        return;
    //    }

    //    if (this.toOverrideAINum == 1)
    //    {
    //        AIBeheviourInfo _choosingAIBeheviourInfo = _CharacterDataInfo.AI1;
    //        this.RonglianChoosingAILevel.text = "Level" + _choosingAIBeheviourInfo.returnCurrentLevel().level.ToString();
    //        RongLianExpTiao.fillAmount =
    //            (float)(_choosingAIBeheviourInfo.returnCurrentLevel().currentExpInLevel) 
    //                           / _choosingAIBeheviourInfo.wholeExpAtCurrentLevel(_choosingAIBeheviourInfo.returnCurrentLevel().level);
    //    }
    //    else if (this.toOverrideAINum == 2)
    //    {
    //        AIBeheviourInfo _choosingAIBeheviourInfo = _CharacterDataInfo.AI2;
    //        this.RonglianChoosingAILevel.text = "Level" + _choosingAIBeheviourInfo.returnCurrentLevel().level.ToString();
    //        RongLianExpTiao.fillAmount =
    //            (float)(_choosingAIBeheviourInfo.returnCurrentLevel().currentExpInLevel) 
    //                           / _choosingAIBeheviourInfo.wholeExpAtCurrentLevel(_choosingAIBeheviourInfo.returnCurrentLevel().level);
    //    }else{
    //        this.RonglianChoosingAILevel.text = "";
    //        RongLianExpTiao.fillAmount = 0f;
    //    }
    //    StartCoroutine(selectBaseMonsterGUIRefresh());
    //}

    //void setRongLianMaterial(CharacterDataInfo m)
    //{
    //    this._materialCharLocalID = m.localID;
    //    setRongLianMaterialAIID(-1);
    //    StartCoroutine(selectMaterialMonsterGUIRefresh());
    //}
    //void setRongLianMaterialAIID(int AIID)
    //{
    //    this.materialCharAINum = AIID;
    //    CharacterDataInfo _CharacterDataInfo = TeamSet.Instance.localCustomerInfo.getTheCharacterOfMine(this._materialCharLocalID);
    //    if (_CharacterDataInfo == null)
    //    {
    //        Debug.Log("还没选MaterialMonster");
    //        return;
    //    }
    //    if (this.materialCharAINum == 1)
    //    {
    //        RonglianAI1Button.GetComponent<Image>().sprite = ChoosingAISprite;
    //        RonglianAI2Button.GetComponent<Image>().sprite = unchosenAISprite;
    //    }
    //    if (this.materialCharAINum == 2)
    //    {
    //        RonglianAI1Button.GetComponent<Image>().sprite = unchosenAISprite;
    //        RonglianAI2Button.GetComponent<Image>().sprite = ChoosingAISprite;
    //    }
    //    if (this.materialCharAINum == -1)
    //    {
    //        RonglianAI1Button.GetComponent<Image>().sprite = unchosenAISprite;
    //        RonglianAI2Button.GetComponent<Image>().sprite = unchosenAISprite;

    //        this.RonglianChoosingAILevel.text = "";
    //        RongLianExpTiao.fillAmount = 0f;
    //        return;
    //    }

    //    if ((this.materialCharAINum == 1 && _CharacterDataInfo.AI1 != null) || (this.materialCharAINum == 2 && _CharacterDataInfo.AI2 != null))
    //    {
    //        AIBeheviourInfo _choosingAIBeheviourInfo;
    //        if (this.materialCharAINum == 1)
    //        {
    //            _choosingAIBeheviourInfo = _CharacterDataInfo.AI1;
    //        }
    //        else if(this.materialCharAINum == 2)
    //        {
    //            _choosingAIBeheviourInfo = _CharacterDataInfo.AI2;
    //        }else{
    //            Debug.Log("错误");
    //            return;
    //        }
    //        this.RonglianChoosingAILevel.text = "Level" + _choosingAIBeheviourInfo.returnCurrentLevel().level.ToString();
    //        RongLianExpTiao.fillAmount = (float)(_choosingAIBeheviourInfo.returnCurrentLevel().currentExpInLevel)
    //            / _choosingAIBeheviourInfo.wholeExpAtCurrentLevel(_choosingAIBeheviourInfo.returnCurrentLevel().currentExpInLevel);
    //    }
    //    else
    //    {
    //        Debug.Log("AI号码下角色没有脚本");
    //    }
    //    StartCoroutine(selectMaterialMonsterGUIRefresh());
    //}
//}
