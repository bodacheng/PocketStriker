namespace NoSuchStudio.Common {
    public class SingletonChildEnabler : NoSuchMonoBehaviour {
        void OnEnable() {
            if (!GetComponent<Singleton>().IsChosenSingleton) return;
            for (int i = 0; i < transform.childCount; i++) {
                transform.GetChild(i).gameObject.SetActive(true);
            }
        }
    }
}