using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Position_Set_Libary : MonoBehaviour {

    public Position_set[] cooperation_list;
}

[System.Serializable]
public class Position_set
{
    public string on_trigger_process_name;
    public string start_process_name;
    public string update_process_name;//这个方法当前是规定在位置调整结束后每帧运行的
    public string end_process_name;
    public float process_time;
    float process_time_counter;
    public void countProcessTime()
    {
        process_time_counter += Time.deltaTime;
    }
    public float getProcessCounterTime(){
        return process_time_counter;
    }

    private bool on_triggerFlag = false;
    public void setOnTriggerFlag(bool on) {
        this.on_triggerFlag = on;
    }
    public bool getOnTriggerFlag()
    {
        return this.on_triggerFlag;
    }
    protected bool start_process_go = false;
    protected bool parented = false;

    public GameObject Child;
    public GameObject Parent;
    public Vector3 Local_Position;
    public Vector3 Local_Rotation;

    [HideInInspector]
    int command_step = 0;
    public int getCommandStep()
    {
        return this.command_step;
    }
    public void setCommandStep(int c)
    {
        this.command_step = c;
    }

    float frame_counter = 0;
    public float getFrameCounter()
    {
        return this.frame_counter;
    }
    public void setFrameCounter(float s)
    {
        this.frame_counter = s;
    }

    /// <summary>
    /// 必须是乘以某整数能得到1的小数
    /// </summary>
    public float frame_plus;

    public void setStartProcessGo(bool temp)
    {
        this.start_process_go = temp;
    }

    public bool getStartProcessGo()
    {
        return start_process_go;
    }

    public void setParented(bool temp)
    {
        this.parented = temp;
    }

    public bool getParented()
    {
        return this.parented;
    }

    public void run() //启动函数。将此position_set任务加入队列
    {
        this.setOnTriggerFlag(true);
        this.start_process_go = true;
        process_time_counter = 0f;
        Position_Set_Executor.Instance.P_sets.Add(this);
        this.command_step = 1;
    }

    public void end()
    {
        this.command_step = 3;
    }

    public void copyData (Position_set copy) {
        parented = copy.parented;
        start_process_name = copy.start_process_name;
        end_process_name = copy.end_process_name;
        start_process_go = copy.start_process_go;
        frame_plus = copy.frame_plus;
        Child = copy.Child;
        Parent = copy.Parent;
        Local_Position = copy.Local_Position;
        Local_Rotation = copy.Local_Rotation;
        command_step = copy.command_step;
        process_time = copy.process_time;
        process_time_counter = 0f;
    }
}
