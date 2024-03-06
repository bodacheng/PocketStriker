using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Child_Label : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if (GetComponent<Collider>() != null)
            GetComponent<Collider>().isTrigger = true;
        if (GetComponent<Rigidbody>() != null)
            GetComponent<Rigidbody>().useGravity = false;
    }

    void OnDestroy()
    {
        if (GetComponent<Rigidbody>() != null)
            GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
    }
}
