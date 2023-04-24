using System;

namespace AggroBird.GameFramework
{
    public class FatalGameException : Exception
    {
        public FatalGameException(string message) : base(message)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit(-1);
#endif
        }
    }
}
