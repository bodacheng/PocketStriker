using System;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine;

public class GotchaLayer : UILayer
{
    [SerializeField] List<DropTablePage> dropTables;
    [SerializeField] Button left, right;
    
    public void Setup(Action<string,string,int> execute, Action<string> dropTableInfo, Action<int, List<DropTablePage>> indexAction, bool tutorial)
    {
        for (var i = 0; i < dropTables.Count; i++)
        {
            var dropTable = dropTables[i];
            dropTable.Setup(execute, dropTableInfo, tutorial);
        }
        
        left.onClick.AddListener(() => { indexAction(-1, dropTables);});
        right.onClick.AddListener(() => { indexAction(1, dropTables);});
        
        indexAction(0, dropTables);
        if (tutorial)
        {
            left.gameObject.SetActive(false);
            right.gameObject.SetActive(false);
        }
    }
}