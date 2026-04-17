using UnityEngine;

namespace AggroBird.GameFramework
{
    [CreateAssetMenu(menuName = "Game/Framework/EntryPoint", fileName = "EntryPoint")]
    public class EntryPoint : ScriptableObject
    {
        [SerializeField] private AppInstance gameInstancePrefab = default;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            if (!AppInstance.IsInitialized)
            {
                EntryPoint entryPointPrefab = Resources.Load<EntryPoint>("EntryPoint");
                if (!entryPointPrefab) throw new FatalGameException("Failed to find entry point asset");
                EntryPoint entryPoint = Instantiate(entryPointPrefab);
                AppInstance gameInstance = Instantiate(entryPoint.gameInstancePrefab);
                gameInstance.Initialize();
            }
        }
    }
}