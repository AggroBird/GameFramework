using AggroBird.UnityEngineExtend;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AggroBird.GameFramework
{
    [RequireComponent(typeof(Animator))]
    public class Dummy : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        public Animator Animator => animator;


#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            Utility.EnsureComponentReference(this, ref animator);
        }
#endif
    }
}