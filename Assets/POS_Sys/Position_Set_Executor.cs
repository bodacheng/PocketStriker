using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

//这个文件用以事先记录所有场景中相互事件时候两个角色间的相互位置 大概应该是搞成单例模式

public class Position_Set_Executor
{
    private static Position_Set_Executor instance;

    private Position_Set_Executor()
    {
        P_sets = new List<Position_set>();
    }
    public static Position_Set_Executor Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new Position_Set_Executor();
            }
            return instance;
        }
    }

    [HideInInspector]
    public List<Position_set> P_sets;

    void Awake()
    {
        //instance = this;
    }

    void Start()
    {
    }

    public void Update()
    {
        if (P_sets.Count != 0)
        {
            List<int> ended_pos_set_nums = new List<int>();
            int ended_pos_set_num = 0;
            foreach (Position_set Position_set in P_sets)
            {                
                //接下来判断的前提应该是子物体真正是父物体的子物体
                if (Position_set.getCommandStep() == 1)
                {
                    if (Position_set.getOnTriggerFlag()) {
                        System.Type T = typeof(Start_End_Processes);
                        MethodInfo theMethod = T.GetMethod(Position_set.on_trigger_process_name); //激活同名函数
                        if (theMethod != null)
                        {   
                            theMethod.Invoke(Start_End_Processes.Instance, new object[] { Position_set.Child, Position_set.Parent });
                        } 
                        Position_set.setOnTriggerFlag(false);
                    }

                    if (Position_set.Child.GetComponent<Child_Label>() == null) {
                        Position_set.Child.AddComponent<Child_Label>();
                    }
                    if (Position_set.Parent.GetComponent<Parent_Label>() == null) {
                        Position_set.Parent.AddComponent<Parent_Label>();
                    }
                    
                    if (Position_set.getStartProcessGo()) {
                        
                        if (!Position_set.getParented())
                        {
                            Position_set.Child.transform.SetParent(Position_set.Parent.transform, true); // 这一句话即便重复执行应该也没什么问题。
                            Position_set.setParented(true);
                        }
                        Position_set.setFrameCounter(Position_set.getFrameCounter() + Position_set.frame_plus);
                        Position_set.Child.transform.localPosition = Vector3.Lerp(Position_set.Child.transform.localPosition, Position_set.Local_Position, Position_set.getFrameCounter());// 这里这个数字仍然需要讨论
                        Position_set.Child.transform.localRotation = Quaternion.Slerp(Position_set.Child.transform.localRotation, Quaternion.Euler(Position_set.Local_Rotation), Position_set.getFrameCounter());

                        if (Mathf.Approximately(Vector3.Distance(Position_set.Child.transform.localPosition, Position_set.Local_Position), 0.0f)
                            &&
                                Mathf.Approximately(Quaternion.Angle(Position_set.Child.transform.localRotation, Quaternion.Euler(Position_set.Local_Rotation)), 0.0f))
                        {                                              
                            System.Type T = typeof(Start_End_Processes);
                            MethodInfo theMethod = T.GetMethod(Position_set.start_process_name); //激活同名函数
                            if (theMethod != null)
                            {   
                                theMethod.Invoke(Start_End_Processes.Instance, new object[] { Position_set.Child, Position_set.Parent });//如果这个方法没有能执行成功，那么系统会不断卡在这个位置，无法进行下面的Position_set.setCommandStep(2);从而无法跳出当前步骤
							}                            
                            Position_set.setStartProcessGo(false);
							Position_set.setFrameCounter(0f);
                            Position_set.setCommandStep(2);
                        }
                    }
                }
                else if (Position_set.getCommandStep() == 2)
                {
                    Position_set.Child.transform.localPosition = Vector3.Lerp(Position_set.Child.transform.localPosition, Position_set.Local_Position, 1);
                    Position_set.Child.transform.localRotation = Quaternion.Slerp(Position_set.Child.transform.localRotation, Quaternion.Euler(Position_set.Local_Rotation), 1);

                    if (Position_set.update_process_name!=null) {
    					System.Type T = typeof(Start_End_Processes);
    					MethodInfo theMethod = T.GetMethod(Position_set.update_process_name); //激活同名函数
    					if (theMethod != null)
    					{
    						theMethod.Invoke(Start_End_Processes.Instance, new object[] { Position_set.Child, Position_set.Parent });
    					}
                    }
                    if (Position_set.process_time > 0f) {
                        Position_set.countProcessTime();
                        if (Position_set.getProcessCounterTime() >= Position_set.process_time)
						{
							Position_set.end();
						}
                    }
                }
                else if (Position_set.getCommandStep() == 3)
                {   
                    Position_set.Child.transform.parent = null;
                    GameObject.Destroy(Position_set.Child.GetComponent<Child_Label>());
                    GameObject.Destroy(Position_set.Parent.GetComponent<Parent_Label>());

                    System.Type T = typeof(Start_End_Processes);
                    MethodInfo theMethod = T.GetMethod(Position_set.end_process_name); //激活同名函数
                    if (theMethod != null)
                    {
                        theMethod.Invoke(Start_End_Processes.Instance, new object[] { Position_set.Child, Position_set.Parent });
                    }

                    if (Position_set.Child.GetComponent<Collider>() != null)
                        Position_set.Child.GetComponent<Collider>().isTrigger = false;
                    if (Position_set.Child.GetComponent<Rigidbody>() != null)
                        Position_set.Child.GetComponent<Rigidbody>().useGravity = true;

                    ended_pos_set_nums.Add(ended_pos_set_num);
                }
                ended_pos_set_num++;
            }

            //下面这些都是为了确保将任务从列表中剔除的时候没有那种时空悖论
            List<Position_set> to_be_del = new List<Position_set>();
            foreach (int num in ended_pos_set_nums)
            {
                to_be_del.Add(P_sets[num]);
            }
            foreach (Position_set set in to_be_del)
            {
                P_sets.Remove(set);
            }
        }
    }
}



