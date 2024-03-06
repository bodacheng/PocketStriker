using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Intro : MonoBehaviour {

    public Image current_image;
    public Sprite[] pics;
    private int pages,index = 0;

    void Awake()
    {
        pages = pics.Length;
    }

    // Use this for initialization
    void Start () {
        index = 0;
	}
	
    public void nextPage()
    {
        index++;
        if (index > pages -1)
        {
            index = 0;
        }
    }

    public void prePage()
    {
        index--;
        if (index < 0)
        {
            index = pages - 1;
        }
    }

	// Update is called once per frame
	void Update () {
        current_image.sprite = pics[index];
	}

    void OnGUI()
    {
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), current_image.mainTexture);
        if (GUI.Button(new Rect(Screen.width-200, Screen.height -120, 100, 77), "Next"))
            nextPage();

        if (GUI.Button(new Rect(200, Screen.height - 120, 100, 77), "Pre"))
            prePage();

        if (GUI.Button(new Rect(90, 90, 100, 77), "Return"))
            SceneManager.LoadScene(1);
    }
}
