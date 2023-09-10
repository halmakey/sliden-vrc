
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Chikuwa.Sliden
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabletPlaceholder : UdonSharpBehaviour
    {
        public readonly float FarDistance = 0.6f;
        public readonly float NearDistance = 0.3f;

        public Tablet Tablet;
        private bool _needUpdate;
        private MeshRenderer _meshRenderer;
        private bool _lastTabletLock;
        private float _lastTabletDistance;
        private float _lastBodyDistance;

        public bool LastTabletLock { set { _needUpdate |= _lastTabletLock != value; _lastTabletLock = value; } }
        public float LastTabletDistance { set { _needUpdate |= _lastTabletDistance != value; _lastTabletDistance = value; } }
        public float LastBodyDistance { set { _needUpdate |= _lastBodyDistance != value; _lastBodyDistance = value; } }

        void Start()
        {
            _needUpdate = true;
            _meshRenderer = (MeshRenderer)GetComponentInChildren(typeof(MeshRenderer));

            if (Tablet == null)
            {
                return;
            }

            Tablet.AddPlaceholder(this);
            UpdateIfNeeded();
        }

        public override void Interact()
        {
            if (Tablet == null)
            {
                return;
            }
            Networking.SetOwner(Networking.LocalPlayer, Tablet.gameObject);

            Tablet.ResetPosition(transform);
        }

        public void UpdateIfNeeded()
        {
            if (!_needUpdate)
            {
                return;
            }
            _needUpdate = false;

            var tabletInteractable = (_lastTabletDistance > NearDistance && !_lastTabletLock) || _lastTabletDistance > FarDistance;
            var bodyInteractable = _lastBodyDistance < FarDistance;

            DisableInteractive = !(tabletInteractable && bodyInteractable);
            _meshRenderer.enabled = tabletInteractable;
        }
    }
}