using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public static class UniTaskUtility
    {
        public static UniTask Watch(this UniTask task, MonoBehaviour monoBehaviour)
        {
            return task.AttachExternalCancellation(monoBehaviour.GetCancellationTokenOnDestroy());
        }
        public static UniTask Watch(this UniTask task, Component monoBehaviour)
        {
            return task.AttachExternalCancellation(monoBehaviour.GetCancellationTokenOnDestroy());
        }
        public static UniTask Watch(this UniTask task, GameObject monoBehaviour)
        {
            return task.AttachExternalCancellation(monoBehaviour.GetCancellationTokenOnDestroy());
        }

        public static UniTask<T> Watch<T>(this UniTask<T> task, MonoBehaviour monoBehaviour)
        {
            return task.AttachExternalCancellation(monoBehaviour.GetCancellationTokenOnDestroy());
        }
        public static UniTask<T> Watch<T>(this UniTask<T> task, Component monoBehaviour)
        {
            return task.AttachExternalCancellation(monoBehaviour.GetCancellationTokenOnDestroy());
        }
        public static UniTask<T> Watch<T>(this UniTask<T> task, GameObject monoBehaviour)
        {
            return task.AttachExternalCancellation(monoBehaviour.GetCancellationTokenOnDestroy());
        }
    }
}