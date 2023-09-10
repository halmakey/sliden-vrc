
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Chikuwa.Sliden
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Screen : UdonSharpBehaviour
    {
        public Sliden Sliden;
        void Start()
        {
            if (Sliden) {
                Sliden.AddScreen(gameObject);
            }
        }
    }
}

