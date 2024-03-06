using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//using System.Linq;

//#if UNITY_EDITOR
//using UnityEngine.Windows;
//#endif

//public enum DebugMode : int
//{
//    resource_mode = 1,
//    ab_mode = 2
//}

////这个Debugmanager其实是为了不浪费老版本企划造出的东西而创建的。因为这个debug版本是依靠xml战斗脚本来决定角色技能。
////而xml脚本驱动这个东西在正式版本里已经不存在了。所以下面所有的加载角色函数，xml脚本都是读取resource。这个Debug模块能测试的只是角色自身构造以及特效。
//namespace FightScene
//{
//    public class DebugManager : MonoBehaviour
//    {
//        public CharsManager _CharSetManager;//0610 charsetmanger临时扮演数据库的作用
//        public NetFightScene _NetFightScene;
//        public CameraManager _CameraManager;//这个东西其实其他模块如果也拥有对其操作权的话，倒不会产生多大的问题

//        public BoundaryControllByGod _BoundaryControllByGod;

//        public DebugMode debugMode = DebugMode.ab_mode;

//        public void switchMode()
//        {
//            if (this.debugMode == DebugMode.ab_mode)
//            {
//                this.debugMode = DebugMode.resource_mode;
//                StartCoroutine(resouceModeOnTypeChanged());
//            }
//            else
//                this.debugMode = DebugMode.ab_mode;
//        }

//        [Header("测试用机能代码。添加object去用的时候靠这些参数来生成角色")]
//        [Space(6)]
//        public GameObject debugCharPlacer;
//        public Dropdown type;
//        public InputField pretabName;
//        public Dropdown charsOfType;//new
//        public Dropdown tag_dropdown;
//        public InputField basicPackNameInput;//
//        public InputField AIScriptName;
//        public Dropdown AIScriptsOfType;//new 
//        public Dropdown ZokuseiDropDown;//new 
//        public InputField personalMagic;
//        public InputField AIlevelNum;
//        public Button _debugCharAddButton;
//        public Texture2D cursorTexture;
//        private float pressed_counter = 1f;
//        private bool create_chance = true;
//        public int debugModePlayerPlacementStep; // 0:没有放置任务，1:正在放置

//        public IEnumerator resouceModeOnTypeChanged()
//        {
//            yield return AnimationResourceLoader.Instance.PrepareAllAttackAnimationClipsByTypeFromResourceAndPutItIntoDic(type.options[type.value].text);

//            List<UnityEngine.Object> fightChars = Resources.LoadAll("CharPretabs/" + type.options[type.value].text).ToList();
//            List<UnityEngine.Object> AIScripts = Resources.LoadAll("AIScripts/" + type.options[type.value].text).ToList();

//            List<string> resourceNames = new List<string>();
//            foreach (UnityEngine.Object _one in fightChars)
//            {
//                resourceNames.Add(_one.name);
//            }
//            charsOfType.ClearOptions();
//            foreach (string Rname in resourceNames)
//            {
//                Dropdown.OptionData m_NewData = new Dropdown.OptionData();
//                m_NewData.text = Rname;
//                charsOfType.options.Add(m_NewData);
//            }

//            List<string> AISeriesNames = new List<string>();
//            foreach (UnityEngine.Object _one in AIScripts)
//            {
//                AISeriesNames.Add(_one.name);
//            }
//            AIScriptsOfType.ClearOptions();
//            foreach (string AIname in AISeriesNames)
//            {
//                Dropdown.OptionData m_NewData = new Dropdown.OptionData();
//                m_NewData.text = AIname;
//                AIScriptsOfType.options.Add(m_NewData);
//            }
//            yield break;
//        }

//        // Use this for initialization
//        void Start()
//        {
//            //StartCoroutine(startUp());
//        }

//        IEnumerator startUp()
//        {
//            type.ClearOptions();
//            yield return MonstersConfigTable.Instance.LoadMonstersConfig();
//            List<string> typeList = MonstersConfigTable.Instance.GetTypeList();

//            foreach (string typeName in typeList)//数据库引入后这个环节就要变化。
//            {
//                Dropdown.OptionData m_NewData = new Dropdown.OptionData();
//                m_NewData.text = typeName;
//                type.options.Add(m_NewData);
//            }

//            ZokuseiDropDown.ClearOptions();
//            Dropdown.OptionData m_NewZokuseiData1 = new Dropdown.OptionData();
//            m_NewZokuseiData1.text = "red";
//            Dropdown.OptionData m_NewZokuseiData2 = new Dropdown.OptionData();
//            m_NewZokuseiData2.text = "blue";
//            Dropdown.OptionData m_NewZokuseiData3 = new Dropdown.OptionData();
//            m_NewZokuseiData3.text = "green";
//            Dropdown.OptionData m_NewZokuseiData4 = new Dropdown.OptionData();
//            m_NewZokuseiData4.text = "dark";
//            Dropdown.OptionData m_NewZokuseiData5 = new Dropdown.OptionData();
//            m_NewZokuseiData5.text = "light";
//            ZokuseiDropDown.options.Add(m_NewZokuseiData1);
//            ZokuseiDropDown.options.Add(m_NewZokuseiData2);
//            ZokuseiDropDown.options.Add(m_NewZokuseiData3);
//            ZokuseiDropDown.options.Add(m_NewZokuseiData4);
//            ZokuseiDropDown.options.Add(m_NewZokuseiData5);
//            yield return resouceModeOnTypeChanged();
//            if (_debugCharAddButton != null)
//            {
//                UnityEngine.Events.UnityAction action1 = () => { this.debugCharAddButton(); };
//                _debugCharAddButton.GetComponentInChildren<Button>().onClick.AddListener(action1);
//            }
//        }

//        public void debugCharAddButton()
//        {
//            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);//这个是在你点了add按钮之后才有的。。。所以add按钮的函数和这个placingCharacter（）应该是两个
//            this.debugModePlayerPlacementStep = 1;
//        }

//        void debugAddCharUIStateReset()
//        {
//            this.debugModePlayerPlacementStep = 0;
//            pressed_counter = 1f;
//            create_chance = true;
//            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
//        }

//        //这个应该是如果按了那个add按钮后，本程序每帧执行内容.
//        // 只依靠这个模块去实现点了add钮，就进入添加等待，点一下地图，生成角色结束添加等待。。这个应该是没有什么实现起来复杂的。但怕的是什么呢，就是进入了添加等待后产生某些例外让一些状态量卡在这个环节。。。
//        public void placingCharacter()
//        {
//            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
//            {
//                if (UnityEngine.Input.touchCount > 0)
//                {
//                    if (this.debugModePlayerPlacementStep == 1)
//                    {
//                        if (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Ended)
//                        {
//                            this.debugModePlayerPlacementStep = 3;
//                        }
//                    }
//                    if (this.debugModePlayerPlacementStep == 3)
//                    {
//                        if (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began)
//                        {
//                            this.debugModePlayerPlacementStep = 2;
//                        }
//                    }

//                    if (debugModePlayerPlacementStep == 2) // 其实这里面是没有包括按住屏幕但手指不滑动的情况 
//                    {
//                        if (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Stationary || UnityEngine.Input.GetTouch(0).phase == TouchPhase.Moved)
//                        {
//                            if (create_chance)
//                            {
//                                int level = int.Parse(AIlevelNum.text);
//                                level = Mathf.Clamp(level, 1, 100);
//                                CharConfig _CharacterResourceInfo = MonstersConfigTable.Instance.RowToCharacterResourceInfo(MonstersConfigTable.Instance.Find_REAL_NAME(pretabName.text));
//                                if (_CharacterResourceInfo == null)
//                                {
//                                    debugAddCharUIStateReset();
//                                    return;
//                                }

//                                TeamConfig teamConfig;
//                                if (tag_dropdown.options[tag_dropdown.value].text == "Player1")
//                                {
//                                    teamConfig = RealTimeGameProcessManager.target.heroTeamConfig;
//                                }
//                                else
//                                {
//                                    teamConfig = RealTimeGameProcessManager.target.EnemyTeamConfig;
//                                }

//                                Zokusei Zokusei;
//                                switch (ZokuseiDropDown.options[ZokuseiDropDown.value].text)
//                                {
//                                    case "red":
//                                        Zokusei = Zokusei.redMagic;
//                                        break;
//                                    case "blue":
//                                        Zokusei = Zokusei.blueMagic;
//                                        break;
//                                    case "green":
//                                        Zokusei = Zokusei.greenMagic;
//                                        break;
//                                    case "dark":
//                                        Zokusei = Zokusei.darkMagic;
//                                        break;
//                                    case "light":
//                                        Zokusei = Zokusei.lightMagic;
//                                        break;
//                                    default:
//                                        Zokusei = Zokusei.lightMagic;
//                                        break;
//                                }

//                                if (debugMode == DebugMode.ab_mode)
//                                {
//                                    IDictionary<string, GameObject> teamDic = null;
//                                    string localID = (-1).ToString();
//                                    if (teamConfig == RealTimeGameProcessManager.target.heroTeamConfig)
//                                    {
//                                        localID = MyModelPool.Instance.ModelDicBasedOnPlayerLocalID.Count.ToString();
//                                        teamDic = MyModelPool.Instance.ModelDicBasedOnPlayerLocalID;
//                                    }
//                                    if (teamConfig == RealTimeGameProcessManager.target.EnemyTeamConfig)
//                                    {
//                                        //localID = myModelPool.Instance.ModelDicBasedOnEnemiesLocalID.Count.ToString();
//                                        //teamDic = myModelPool.Instance.ModelDicBasedOnEnemiesLocalID;
//                                    }

//                                    switch (ResourceLoadingSetting.ModelLoadingMode)
//                                    {
//                                        case ResourceLoadMode.CachAB:
//                                            StartCoroutine(
//                                                _CharSetManager.CreateCharacterFromABByCach(
//                                                    _CharacterResourceInfo.GetTestCharConfig(localID),
//                                                    AIScriptName.text,
//                                                    Zokusei,
//                                                    _CharacterResourceInfo.SPECIAL_ZOKUSEI,
//                                                    teamConfig.myTeam,
//                                                    decidePlace(), Quaternion.identity
//                                            ));
//                                            break;
//                                        case ResourceLoadMode.StreamingAssetAB:
//                                            StartCoroutine(
//                                                _CharSetManager.CreateCharacterFromABByStreamingAssets(
//                                                    _CharacterResourceInfo.GetTestCharConfig(localID),
//                                                    AIScriptName.text,
//                                                    Zokusei,
//                                                    _CharacterResourceInfo.SPECIAL_ZOKUSEI,
//                                                    teamConfig.myTeam,
//                                                    decidePlace(), Quaternion.identity
//                                            ));
//                                            break;
//                                    }
//                                }
//                                else
//                                {
//                                    StartCoroutine(
//                                        createCharByResourcePath(
//                                        type.options[type.value].text,
//                                        charsOfType.options[charsOfType.value].text,
//                                        basicPackNameInput.text,
//                                        AIScriptsOfType.options[AIScriptsOfType.value].text,
//                                        level,
//                                        Zokusei,
//                                         _CharacterResourceInfo.SPECIAL_ZOKUSEI,
//                                        teamConfig,
//                                        decidePlace(),
//                                        Quaternion.identity
//                                        ));
//                                }

//                                if (_CharacterResourceInfo == null)
//                                {
//                                    debugAddCharUIStateReset();
//                                    return;
//                                }
//                                create_chance = false;
//                            }
//                            else
//                            {
//                                pressed_counter -= Time.deltaTime;
//                                if (pressed_counter < 0f)
//                                {
//                                    pressed_counter = 0.15f;
//                                    create_chance = true;
//                                }
//                            }
//                        }
//                        else if (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Ended)
//                        {
//                            debugAddCharUIStateReset();
//                        }
//                    }
//                }
//            }

//            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor ||
//                Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
//            {
//                if (UnityEngine.Input.GetMouseButton(0))
//                {
//                    this.debugModePlayerPlacementStep = 2;
//                    if (debugModePlayerPlacementStep == 2)
//                    {
//                        if (create_chance)
//                        {
//                            int level = int.Parse(AIlevelNum.text);
//                            TeamConfig teamConfig;
//                            if (tag_dropdown.options[tag_dropdown.value].text == "Player1")
//                            {
//                                teamConfig = RealTimeGameProcessManager.target.heroTeamConfig;
//                            }
//                            else
//                            {
//                                teamConfig = RealTimeGameProcessManager.target.EnemyTeamConfig;
//                            }

//                            Zokusei Zokusei;
//                            switch (ZokuseiDropDown.options[ZokuseiDropDown.value].text)
//                            {
//                                case "red":
//                                    Zokusei = Zokusei.redMagic;
//                                    break;
//                                case "blue":
//                                    Zokusei = Zokusei.blueMagic;
//                                    break;
//                                case "green":
//                                    Zokusei = Zokusei.greenMagic;
//                                    break;
//                                case "dark":
//                                    Zokusei = Zokusei.darkMagic;
//                                    break;
//                                case "light":
//                                    Zokusei = Zokusei.lightMagic;
//                                    break;
//                                default:
//                                    Zokusei = Zokusei.lightMagic;
//                                    break;
//                            }

//                            if (debugMode == DebugMode.ab_mode)
//                            {
//                                IDictionary<string, GameObject> teamDic = null;
//                                string localID = (-1).ToString();
//                                if (teamConfig == RealTimeGameProcessManager.target.heroTeamConfig)
//                                {
//                                    localID = MyModelPool.Instance.ModelDicBasedOnPlayerLocalID.Count.ToString();
//                                    teamDic = MyModelPool.Instance.ModelDicBasedOnPlayerLocalID;
//                                }
//                                if (teamConfig == RealTimeGameProcessManager.target.EnemyTeamConfig)
//                                {
//                                    //localID = myModelPool.Instance.ModelDicBasedOnEnemiesLocalID.Count.ToString();
//                                    //teamDic = myModelPool.Instance.ModelDicBasedOnEnemiesLocalID;
//                                }

//                                CharDataInfo _CharacterDataInfo = new CharDataInfo();
//                                _CharacterDataInfo.monsterOfPlayerId = localID;
//                                _CharacterDataInfo.ResourceID = charsOfType.options[charsOfType.value].text; // 确切的说这个也就是角色的pretab编号，最后也就是数据库里master table的主key。
//                                _CharacterDataInfo._NineAndTwo = null;

//                                if (ResourceLoadingSetting.ModelLoadingMode == ResourceLoadMode.StreamingAssetAB)
//                                {
//                                    StartCoroutine(
//                                        _CharSetManager.CreateCharacterFromABByStreamingAssets(
//                                            _CharacterDataInfo,
//                                            AIScriptName.text,
//                                            Zokusei,
//                                            personalMagic.text,
//                                            teamConfig.myTeam,
//                                            decidePlace(), Quaternion.identity
//                                    ));
//                                }
//                                else if (ResourceLoadingSetting.ModelLoadingMode == ResourceLoadMode.CachAB)
//                                {
//                                    StartCoroutine(
//                                        _CharSetManager.CreateCharacterFromABByCach(
//                                            _CharacterDataInfo,
//                                            AIScriptName.text,
//                                            Zokusei,
//                                            personalMagic.text,
//                                            teamConfig.myTeam,
//                                            decidePlace(), Quaternion.identity
//                                    ));
//                                }
//                            }
//                            else
//                            {
//                                StartCoroutine(
//                                    createCharByResourcePath(
//                                    type.options[type.value].text,
//                                    charsOfType.options[charsOfType.value].text,
//                                    basicPackNameInput.text,
//                                    AIScriptsOfType.options[AIScriptsOfType.value].text,
//                                    level,
//                                    Zokusei,
//                                    personalMagic.text,
//                                    teamConfig,
//                                    decidePlace(),
//                                    new Quaternion(0, 0, 0, 0)
//                                ));
//                            }
//                            create_chance = false;
//                        }
//                        else
//                        {
//                            pressed_counter -= Time.deltaTime;
//                            if (pressed_counter < 0f)
//                            {
//                                pressed_counter = 0.2f;
//                                create_chance = true;
//                            }
//                        }
//                    }
//                }
//                else
//                {
//                    if (!UnityEngine.Input.GetMouseButton(0))
//                    {
//                        if (this.debugModePlayerPlacementStep == 2)
//                        {
//                            debugAddCharUIStateReset();
//                        }
//                    }
//                }
//            }
//        }

//        // 通用系 但会针对网络和本地模式进行不同的处理。
//        //根据新的企划，AI模式和player模式的转化看起来不再是靠一个函数进行一个反转，因为从
//        //AI到player和从player到AI是完全不同的两个按钮
//        //原则上jueSeLiebiao模块应该完全负责这一类功能。就是说改造的方向甚至是想法让本张脚本里的focusingchar变量消失
//        public IEnumerator createCharByResourcePath(string type, string prefabName, string basicPackName, string AIScriptName, int AIlevel,
//                                                    Zokusei _zokusei, string personalMagicpath,
//                                                    TeamConfig TeamConfig, Vector3 position, Quaternion rotation)
//        {
//            GameObject fightChar = Resources.Load("CharPretabs/" + type + "/" + prefabName) as GameObject;
//            if (fightChar == null)
//                yield break;

//            TextAsset AIScriptPrefab = Resources.Load("AIScripts/" + type + "/" + AIScriptName) as TextAsset;
//            if (AIScriptPrefab == null)
//                yield break;

//            if (TeamConfig != null)
//            {
//                Data_Center aI_DATA_CENTER;
//                OutsideDataLink outsideDataLink = fightChar.GetComponent<OutsideDataLink>();
//                if (fightChar != null && outsideDataLink != null)
//                {
//                    aI_DATA_CENTER = outsideDataLink._C;
//                }
//                else
//                {
//                    yield break;
//                }

//                /// ///////////////////////////////////////////////////////
//                GameObject one_char = Instantiate(fightChar, position, new Quaternion(0, 0, 0, 0));
//                one_char.transform.rotation = rotation;
//                // 在角色生成的瞬间各个组件的awake和onenable就已经都开了，而一些数据的初始化是从下一行开始，所以要确保这个过程不会有一些因为变量没被初始化而形成的报错。
//                yield return (aI_DATA_CENTER.Step1Initialize(type, basicPackName, personalMagicpath));
//                yield return (aI_DATA_CENTER.step2InitializeByResourceFolder(type, AIScriptPrefab, _zokusei, personalMagicpath));
//                aI_DATA_CENTER.Step3Initialize(TeamConfig, 2000);
//            }
//            else
//            {
//                FightLoadError.Instance.FightLoadErrors.Add("标签设置错误");
//                yield return null;
//            }
//        }

//        public List<string> getPathsUnderResourcePath(string resourePath)
//        {
//            List<string> list = new List<string>();
//            var directries = System.IO.Directory.GetDirectories(resourePath);
//            foreach (var directry in directries)
//            {
//                list.Add(directry);
//            }
//            return list;
//        }

//        public Vector3 decidePlace()
//        {
//            RaycastHit hit;
//            Ray ray = new Ray();
//            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
//            {
//                ray = CameraManager._camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
//            }
//            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor ||
//                Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
//            {
//                ray = CameraManager._camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
//            }

//            if (Physics.Raycast(ray, out hit))
//                return hit.point;
//            return new Vector3(0, 0, 0);
//        }
//    }
//}