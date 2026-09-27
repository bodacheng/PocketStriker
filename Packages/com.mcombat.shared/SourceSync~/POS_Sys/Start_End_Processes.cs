using UnityEngine;

public class Start_End_Processes {

    static Start_End_Processes instance;
    public static Start_End_Processes Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new Start_End_Processes();
            }
            return instance;
        }
    }

    public void SpecialAttack1_onTrigger(GameObject Child, GameObject Parent) //下面这两个方法写在这就是举个例子而已。
    {
        //Parent.GetComponent<AIStateRunner>().changeState(AI_State_Number.E1_Ender);
        //Child.GetComponent<AIStateRunner>().changeState(AI_State_Number.Controlled);
    }

    public void SpecialAttack1_start(GameObject Child, GameObject Parent) //下面这两个方法写在这就是举个例子而已。
    {
    }

    public void SpecialAttack1_process(GameObject Child, GameObject Parent)
    {
    }

    public void SpecialAttack1_end(GameObject Child, GameObject Parent)
    {
        //Child.GetComponent<AIStateRunner>().changeState(AI_State_Number.KnockOff);
        //Parent.GetComponent<AIStateRunner>().changeState(AI_State_Number.Move);
    }


	public void test_start(GameObject Child, GameObject Parent) //下面这两个方法写在这就是举个例子而已。
	{
        //Child.GetComponent<AIStateRunner>().changeState(AI_State_Number.Empty);
	}

    public void test_process(GameObject Child, GameObject Parent) 
    {        
    }

	public void test_end(GameObject Child, GameObject Parent)
	{
        //Child.GetComponent<AIStateRunner>().changeState(AI_State_Number.Move);
	}
}
