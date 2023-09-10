using UdonSharp;

namespace Chikuwa.Sliden
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LocalTablet : Tablet
    {
        protected override void Start()
        {
            base.Start();
            Lock = false;
        }
    }

}