using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.EventSystems;
//using System;
//using UnityEngine.UI;
//
//public class Save_Trans : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
//{
//    public CharSetManager _CharSetManager;
//    public GameObject button_prefab;
//	public RectTransform container;
//	public bool isOpen;
//
//	public void OnPointerEnter(PointerEventData eventData)
//	{
//		isOpen = true;
//
////        foreach (GameObject a_char in _CharSetManager.getALLChars())
////		{
////			GameObject newbutton = Instantiate(button_prefab);
////			newbutton.name = a_char.name;
////            UnityEngine.Events.UnityAction action1 = () => { this.saveTrans(a_char.GetComponent<AIStateRunner>().State_Transition_Set_List,newbutton.GetComponentInChildren<InputField>().text); };
////            newbutton.GetComponentInChildren<Button>().onClick.AddListener(action1);
////            newbutton.GetComponentInChildren<Text>().text = a_char.name;
////			newbutton.SetActive(true);
////			newbutton.transform.SetParent(container.transform);
////		}
//	}
//
//	public void OnPointerExit(PointerEventData eventData)
//	{
//		foreach (Transform child in container.transform)
//		{
//            foreach (Transform s in child){
//                removeAllUIElement(s);
//                Destroy(s.gameObject);
//            }
//
//            removeAllUIElement(child);
//            Destroy(child.gameObject);
//		}
//		isOpen = false;
//	}
//
//    public void removeAllUIElement(Transform t)
//	{
//        Destroy(t.gameObject.GetComponent<Text>());
//        Destroy(t.gameObject.GetComponent<Image>());
//        Destroy(t.gameObject.GetComponent<Button>());
//    }
//
//    public void saveTrans(List<State_Transition_Set> State_Transition_Set,string AI_States_path) {
//        Char_Info_Loader.Instance.saveStateTransitionInfo(State_Transition_Set, AI_States_path);
//    }
//
//	// Use this for initialization
//	void Start()
//	{
//
//	}
//
//	// Update is called once per frame
//	void Update()
//	{
//        foreach(RectTransform rt in container){
//            Vector3 scale = rt.localScale;
//			scale.y = Mathf.Lerp(scale.y, isOpen ? 1 : 0, Time.deltaTime * 12);
//			rt.localScale = scale;
//        }
//
//		//Vector3 scale = container.localScale;
//		//scale.y = Mathf.Lerp(scale.y, isOpen ? 1 : 0, Time.deltaTime * 12);
//		//container.localScale = scale;
//	}
//}
