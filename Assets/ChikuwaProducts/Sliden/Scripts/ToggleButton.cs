
using UdonSharp;

namespace Chikuwa.Sliden
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ToggleButton : UdonSharpBehaviour
    {
        public UnityEngine.UI.Image onImage;
        public UnityEngine.UI.Image offImage;
        public bool InitialOn;

        private bool _on;

        private void Start()
        {
            _on = InitialOn;

            RefreshImages();
        }

        public void SetOn()
        {
            _on = true;
            RefreshImages();
        }

        public void SetOff()
        {
            _on = false;
            RefreshImages();
        }

        private void RefreshImages()
        {
            onImage.enabled = _on;
            offImage.enabled = !_on;
        }
    }

}
