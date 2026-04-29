using UnityEngine;

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        [SerializeField]
        string skillConfigType;

        public string SkillConfigType
        {
            get => skillConfigType;
            set => skillConfigType = value;
        }
    }
}
