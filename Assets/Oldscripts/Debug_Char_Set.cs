//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;
//using SFB;
//using System;
//using System.Text.RegularExpressions;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif
//
//[System.Serializable]
//public class Debug_Char_Set : MonoBehaviour
//{
//    private CharSetManager _CharSetManager;
//    public GameObject icon;
//    public GameObject prefab;
//    public Toggle ifPlayer;
//    public GameObject transition;
//    public Button Add;
//    public Dropdown tag_dropdown,enemy_tag_dropdown;
//
//    private bool isPlayer, being_placed = false;
//    private string tag_selected, AI_selected;
//    private string[] enemy_tag_selected;
//
//    void Awake()
//    {
//        AI_selected = "";
//        #if UNITY_EDITOR
//        foreach(string a_tag in UnityEditorInternal.InternalEditorUtility.tags) 
//        {
//            GameObject newbutton =  Instantiate(button_prefab);
//            newbutton.name = a_tag;
//            UnityEngine.Events.UnityAction action1 = () => { this.setTag(a_tag); };
//            newbutton.GetComponent<Button>().onClick.AddListener(action1);
//            newbutton.GetComponentInChildren<Text>().text = a_tag;
//            newbutton.SetActive(true);
//            newbutton.transform.SetParent(tag_buttons_container.transform);
//            tag_dropdown.options.Add(new Dropdown.OptionData(a_tag));
//            enemy_tag_dropdown.options.Add(new Dropdown.OptionData(a_tag));
//        }
//        #endif
//    }
//
//	// Use this for initialization
//	void Start()
//    {
//        List<string> tagsInSet = new List<string>();
//        IDictionary<string, int> tag_OptionValues = new Dictionary<string, int>();
//        int i = 0;
//        foreach(UnityEngine.UI.Dropdown.OptionData one in tag_dropdown.options)
//        {
//            tagsInSet.Add(one.text);
//            tag_OptionValues.Add(new KeyValuePair<string,int>(one.text,i));
//            i++;
//        }
//        if (tagsInSet.Contains(prefab.tag))
//        {
//            int value = -1;
//            tag_OptionValues.TryGetValue(prefab.tag,out value);
//            tag_dropdown.value = value;
//        }
//
//        if (prefab.GetComponent<AI_DATA_CENTER>().enemy_tags.Length > 0)
//        {
//            IDictionary<string, int> EnemyTag_OptionValues = new Dictionary<string, int>();
//            int y = 0;
//            foreach (UnityEngine.UI.Dropdown.OptionData one in enemy_tag_dropdown.options)
//            {
//                EnemyTag_OptionValues.Add(new KeyValuePair<string, int>(one.text, y));
//                y++;
//            }
//            if (tagsInSet.Contains(prefab.GetComponent<AI_DATA_CENTER>().enemy_tags[0]))
//            {
//                int value2 = -1;
//                EnemyTag_OptionValues.TryGetValue(prefab.GetComponent<AI_DATA_CENTER>().enemy_tags[0], out value2);
//                enemy_tag_dropdown.value = value2;
//            }
//        }
//	}
//
//    public Texture2D cursorTexture;
//
//	// Update is called once per frame
//	void Update()
//    {
//        SelectModeUpdate();
//    }
//
//    public bool ifBeingPlacing()
//    {
//        return this.being_placed;
//    }
//
//    public void setCharSetManager(CharSetManager s)
//    {
//        _CharSetManager = s;
//    }
//
//    public void SelectModeUpdate()
//    {
//        switch (_CharSetManager.step)
//        {
//            case CharSetManager_step.ally_or_enemy_select:
//                icon.SetActive(true);
//                tag_dropdown.gameObject.SetActive(true);
//                enemy_tag_dropdown.gameObject.SetActive(true);
//                transition.SetActive(true);
//                Add.gameObject.SetActive(true);
//                ifPlayer.gameObject.SetActive(true);
//                setTagAndEnemy();
//                break;
//            case CharSetManager_step.ally_or_enemy_placement:
//                icon.SetActive(false);
//                tag_dropdown.gameObject.SetActive(false);
//                enemy_tag_dropdown.gameObject.SetActive(false);
//                transition.SetActive(false);
//                Add.gameObject.SetActive(false);
//                ifPlayer.gameObject.SetActive(false);
//                placingCharacter();
//                break;
//        }
//    }
//
//    public void setTagAndEnemy()
//    {
//        if (tag_dropdown.value >= 0)
//        {
//            tag_selected = tag_dropdown.options[tag_dropdown.value].text;
//        }
//        if (enemy_tag_dropdown.value >= 0)
//        {
//            enemy_tag_selected = new string[1] { enemy_tag_dropdown.options[enemy_tag_dropdown.value].text };
//        }
//        if (ifPlayer.isOn)
//        {
//            isPlayer = true;
//        }else{
//            isPlayer = false;
//        }
//    }
//
//    public void addButtonBeheviour()
//    {
//        being_placed = true;
//        changeCharSetMangerStep(5);
//    }
//
//    public void placingCharacter()
//    {
//        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
//        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor ||
//            Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
//        {
//            if (Input.GetMouseButtonDown(0))
//            {
//                GameObject o = this.CreateCharacter(this.prefab);
//                if (o != null)
//                {
//                    placeAObject(o, decidePlace());
//                    if (isPlayer)
//                    {
//                        o.GetComponent<AIStateRunner>().playerMode = true;
//                        o.GetComponent<AIStateRunner>().mainCam = _CharSetManager.mainC.transform;
//                    }
//                    if (isPlayer)
//                    {
//                        _CharSetManager.player = o;
//                    }
//                    changeCharSetMangerStep(4);
//                    this.being_placed = false;
//                }
//                else
//                {
//                    changeCharSetMangerStep(4);
//                    this.being_placed = false;
//                    Debug.Log("生成角色失败");
//                }
//                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
//            }
//        }
//    }
//
//    public void changeCharSetMangerStep(int step)
//    {
//        if (this._CharSetManager != null)
//            this._CharSetManager.step = (CharSetManager_step)(CharSetManagerMode)step;
//    }
//
//    public void openfile() 
//    {
//        WriteResult(StandaloneFileBrowser.OpenFilePanel("Open File", "", "", false));
//    }
//
//    public string getFileAddress() 
//    {
//        string[] splitString = AI_selected.Split(new string[] { "Assets" }, StringSplitOptions.None);
//        if (splitString.Length > 1) {
//            string temp = splitString[1];
//            temp = temp.Replace("\n","").Replace("\r","");
//            temp = temp.Replace("\\n", "").Replace("\\r", "");
//            temp = temp.Replace("\n\n", "");
//            temp = temp.Replace("\r\n", "");
//            temp = temp.Replace(Environment.NewLine, "");
//			temp = Regex.Replace(temp, @"^\s*$[\r\n]*", string.Empty, RegexOptions.Multiline);
//            //上面这些 说真的我也不知道哪个不需要 姑且全这么弄上
//            return temp;
//        }
//        else return null;
//    }
//
//    void WriteResult(string[] paths) 
//    {
//        if (paths.Length == 0) {
//            return;
//        }
//        AI_selected = "";
//        foreach (var p in paths) {
//            AI_selected += p + "\n";
//        }
//    }
//
//    public GameObject CreateCharacter(GameObject the_char) 
//    {
//        if (AI_selected != "" && AI_selected != null)
//        {
//            the_char.GetComponent<AIStateRunner>().AI_States_path = this.getFileAddress();
//        }
//        if (tag_dropdown.value >= 0)
//        {
//            the_char.tag = tag_selected;
//        }
//        if (enemy_tag_dropdown.value >= 0)
//        {
//            the_char.GetComponent<Data_Center>().enemy_tags = enemy_tag_selected;
//        }
//        if (tag_selected == enemy_tag_selected[0])
//        {
//            return null;
//        }
//
//        GameObject one_char = Instantiate(the_char,this.decidePlace(),new Quaternion(0,0,0,0));
//        if (one_char.GetComponent<AIStateRunner>().State_Transition_Set_List.Count < 1)
//        {
//            Debug.Log("AI script loading error,couldnt create character");
//            return null;
//        }
//        one_char.GetComponent<AIStateRunner>().changeState(AI_State_Number.Empty);
//        return one_char;
//    }
//
//    public void placeAObject(GameObject o,Vector3 position)
//	{
//		o.transform.position = position;
//	}
//
//    public Vector3 decidePlace()
//    {
//	    RaycastHit hit;
//        Ray ray = _CharSetManager.mainC.ScreenPointToRay(Input.mousePosition);
//        if (Physics.Raycast(ray, out hit))
//            return hit.point;
//        return new Vector3(0,0,0);
//    }
//}
//
