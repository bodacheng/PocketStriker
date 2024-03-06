namespace Crosstales.BWF
{
   [System.Serializable]
   public class OnReady : UnityEngine.Events.UnityEvent
   {
   }

   [System.Serializable]
   public class OnContainsCompleted : UnityEngine.Events.UnityEvent<string, bool>
   {
   }

   [System.Serializable]
   public class OnGetAllCompleted : UnityEngine.Events.UnityEvent<string, string>
   {
   }

   [System.Serializable]
   public class OnReplaceAllCompleted : UnityEngine.Events.UnityEvent<string, string>
   {
   }

   public delegate void ContainsComplete(string originalText, bool containsBadWords);

   public delegate void GetAllComplete(string originalText, System.Collections.Generic.List<string> badWords);

   public delegate void ReplaceAllComplete(string originalText, string cleanText);
}
// © 2020-2021 crosstales LLC (https://www.crosstales.com)