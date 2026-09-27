//using System.Collections.Generic;
//using UnityEngine;

//public class SkillShowLines : MonoBehaviour
//{
//    [Space(11)]
//    [Header("GL Line materail")]
//    public Material mat;

//    List<Vector3[]> DrawLineMissions;
    
//    void OnPostRender()
//    {
//        if (DrawLineMissions != null)
//        {
//            Drawlines(DrawLineMissions);
//        }
//    }
    
//    public void ClearDrawingLines()
//    {
//        DrawLineMissions = null;
//    }
    
//    public void Drawlines(List<Vector3[]> drawLineMissions)//这是一个运行结构特殊的函数 仔细看就明白了 是靠外部激活
//    {
//        DrawLineMissions = drawLineMissions;
//        if (mat == null)
//            return;
        
//        if (drawLineMissions == null)
//            return;
        
//        foreach (Vector3[] _SAF in drawLineMissions)
//        {
//            if (_SAF.Length != 2)
//                continue;
//            GL.PushMatrix();
//            mat.SetPass(0);
//            GL.LoadOrtho();
//            GL.Begin(GL.LINES);
//            GL.Color(Color.white);
//            GL.Vertex(new Vector3(_SAF[0].x / Screen.width, _SAF[0].y / Screen.height, 0));
//            GL.Vertex(new Vector3(_SAF[1].x / Screen.width, _SAF[1].y / Screen.height, 0));
//            GL.End();
//            GL.PopMatrix();
//        }
//    }
//}
