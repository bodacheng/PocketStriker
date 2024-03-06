using System.Collections.Generic;

namespace mainMenu
{
    public class MainSceneLog
    {
        public MainSceneStep step;
        public string description;
    }
    
    public static class MainSceneLogger
    {
        public static List<MainSceneLog> Logs = new List<MainSceneLog>();
    }
}