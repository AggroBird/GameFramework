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
        [SerializeField] private bool createPlayableGraph = false;
        public Animator Animator => animator;

        private PlayableGraph playableGraph;
        private AnimationMixerPlayable mixerPlayable;
        private AnimationClipPlayable clipPlayable;

        private AnimationClip overrideAnimationClip = null;
        private bool isPlayingOverride = false;
        private float overrideStartTime = 0;
        private bool overrideLoop = false;
        private float overrideSpeed = 1;


        protected virtual void Awake()
        {
            if (createPlayableGraph)
            {
                playableGraph = PlayableGraph.Create($"{name}.DummyAnimation");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                AnimationPlayableOutput playableOutput = AnimationPlayableOutput.Create(playableGraph, $"AnimatorOutput", animator);

                mixerPlayable = AnimationMixerPlayable.Create(playableGraph, 2);
                playableOutput.SetSourcePlayable(mixerPlayable);

                AnimatorControllerPlayable controllerPlayable = AnimatorControllerPlayable.Create(playableGraph, animator.runtimeAnimatorController);
                mixerPlayable.ConnectInput(0, controllerPlayable, 0);

                BlendClip(0);

                animator.runtimeAnimatorController = null;
            }
        }
        protected virtual void OnDestroy()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        protected virtual void OnEnable()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Play();
            }
        }
        protected virtual void OnDisable()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Stop();
            }
        }


        public void SetOverrideAnimationClip(AnimationClip clip, bool loop = false, float speed = 1)
        {
            if (clip == null)
            {
                ClearOverrideAnimationClip();
                return;
            }

            if (playableGraph.IsValid())
            {
                if (overrideAnimationClip != clip)
                {
                    DisconnectClip();

                    clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
                    clipPlayable.SetSpeed(0);
                    mixerPlayable.ConnectInput(1, clipPlayable, 0);

                    BlendClip(1);

                    overrideAnimationClip = clip;
                }

                isPlayingOverride = true;
                overrideStartTime = Time.time;
                overrideLoop = loop;
                overrideSpeed = Mathf.Max(speed, 0);
            }
        }
        public void ClearOverrideAnimationClip()
        {
            if (isPlayingOverride)
            {
                BlendClip(0);

                DisconnectClip();

                overrideAnimationClip = null;
                isPlayingOverride = false;
            }
        }

        private void BlendClip(float blend)
        {
            mixerPlayable.SetInputWeight(0, 1 - blend);
            mixerPlayable.SetInputWeight(1, blend);
        }
        private void DisconnectClip()
        {
            if (clipPlayable.IsValid())
            {
                mixerPlayable.DisconnectInput(1);
                clipPlayable.Destroy();
            }
        }

        protected virtual void LateUpdate()
        {
            if (isPlayingOverride)
            {
                float currentPlayTime = (Time.time - overrideStartTime) * overrideSpeed;
                float clipLength = overrideAnimationClip.length;
                if (!overrideLoop && currentPlayTime > clipLength)
                {
                    ClearOverrideAnimationClip();
                }
                else
                {
                    clipPlayable.SetTime(currentPlayTime % clipLength);
                }
            }
        }


#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            Utility.EnsureComponentReference(this, ref animator);
        }
#endif
    }
}