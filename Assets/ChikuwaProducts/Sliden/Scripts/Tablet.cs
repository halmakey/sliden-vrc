
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;

namespace Chikuwa.Sliden
{

    public class Tablet : SlidenListener
    {
        public readonly float HandIntaractableDistance = 0.05f;
        public readonly float BodyIntaractableDistance = 0.6f;

        public Sliden Sliden;

        private bool _needUpdate;
        private bool _lastLock;
        private bool _lastClosing;
        private bool _lastFacing;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private TabletPlaceholder[] _placeholders = Array.Empty<TabletPlaceholder>();
        private Collider _collider;
        private Button _reloadButton;

        internal bool Lock
        {
            set { _needUpdate |= _lastLock != value; _lastLock = value; }
            get { return _lastLock; }
        }
        internal bool Closing { set { _needUpdate |= _lastClosing != value; _lastClosing = value; } get { return _lastClosing; } }
        internal bool Facing { set { _needUpdate |= _lastFacing != value; _lastFacing = value; } get { return _lastFacing; } }

        protected VRC_Pickup Pickup { get; private set; }
        protected Canvas Canvas { get; private set; }
        protected UdonBehaviour LockButton { get; private set; }
        protected virtual void Start()
        {
            Pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            Canvas = (Canvas)GetComponentInChildren(typeof(Canvas));

            foreach (var button in (Button[])GetComponentsInChildren(typeof(Button), true))
            {
                if (LockButton == null && button.name.EndsWith("Lock"))
                {
                    LockButton = (UdonBehaviour)button.GetComponent(typeof(UdonBehaviour));
                }
                else if (button.name.EndsWith("Reload"))
                {
                    _reloadButton = button;
                }
            }

            _collider = (Collider)GetComponent(typeof(Collider));

            if (Sliden != null)
            {
                Sliden.AddListener(this);
            }

            _needUpdate = true;
        }

        protected virtual void Update()
        {
            if (Sliden == null || Pickup == null || Canvas == null)
            {
                return;
            }

            if (!Vector3.Equals(_lastPosition, transform.position) || !Quaternion.Equals(_lastRotation, transform.rotation))
            {
                if (Networking.IsOwner(gameObject))
                {
                    var right = transform.position + (transform.rotation * Vector3.right);
                    right.y = transform.position.y;
                    var direction = right - transform.position;
                    transform.rotation = Quaternion.LookRotation(direction, transform.rotation * Vector3.up)
                        * Quaternion.FromToRotation(Vector3.right, Vector3.forward);
                }
                foreach (var placeholder in _placeholders)
                {
                    placeholder.LastTabletDistance = Vector3.Distance(placeholder.transform.position, transform.position);
                    placeholder.UpdateIfNeeded();
                }

                _lastPosition = transform.position;
                _lastRotation = transform.rotation;
            }

            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null)
            {
                Closing = true;
                Facing = true;
                UpdateIfNeeded();
                return;
            }

            var playerHeadPosition = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            var playerLeftHandPosition = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
            var playerRightHandPosition = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
            var useBodyPosition = !player.IsUserInVR() || (playerLeftHandPosition == Vector3.zero && playerRightHandPosition == Vector3.zero);

            Facing = Vector3.Dot(transform.forward, (playerHeadPosition - transform.position).normalized) < 0;

            Vector3 bodyPosition = player.GetPosition();
            bodyPosition.y = transform.position.y;
            float bodyDistance = Vector3.Distance(bodyPosition, transform.position);
            if (!useBodyPosition && bodyDistance > BodyIntaractableDistance)
            {
                Closing = false;
                UpdateIfNeeded();
                return;
            }

            if (useBodyPosition)
            {
                foreach (var placeholder in _placeholders)
                {
                    bodyPosition.y = placeholder.transform.position.y;
                    placeholder.LastBodyDistance = Vector3.Distance(placeholder.transform.position, bodyPosition);
                    placeholder.UpdateIfNeeded();
                }
                Closing = true;
                UpdateIfNeeded();
                return;
            }

            Closing = Vector3.Distance(_collider.ClosestPoint(playerLeftHandPosition), playerLeftHandPosition) <= HandIntaractableDistance
                || Vector3.Distance(_collider.ClosestPoint(playerRightHandPosition), playerRightHandPosition) <= HandIntaractableDistance;

            UpdateIfNeeded();
        }

        private void UpdateIfNeeded()
        {
            if (!_needUpdate)
            {
                return;
            }
            _needUpdate = false;

            Pickup.pickupable = (Networking.IsOwner(gameObject) || _lastFacing) && _lastClosing && !_lastLock;
            Canvas.enabled = _lastFacing;

            if (LockButton != null)
            {
                LockButton.SendCustomEvent(Lock ? "SetOn" : "SetOff");
            }

            foreach (var placeholder in _placeholders)
            {
                placeholder.LastTabletLock = Lock;
                placeholder.UpdateIfNeeded();
            }
        }

        public virtual void ResetPosition(Transform target)
        {
            Pickup.Drop();
            transform.position = target.position;
            transform.rotation = target.rotation;

            foreach (var placeholder in _placeholders)
            {
                placeholder.LastTabletDistance = Vector3.Distance(placeholder.transform.position, transform.position);
                placeholder.UpdateIfNeeded();
            }
        }

        public virtual void ToggleLock()
        {
            Pickup.Drop();
            Lock = !Lock;
        }


        public void ReloadLocal()
        {
            if (Sliden != null)
            {
                Sliden.ReloadLocal();
            }
        }

        public void NextPage()
        {
            if (Sliden != null)
            {
                Sliden.NextPage();
            }
        }

        public void PrevPage()
        {
            if (Sliden != null)
            {
                Sliden.PrevPage();
            }
        }

        public virtual void AddPlaceholder(TabletPlaceholder placeholder)
        {
            _placeholders = (TabletPlaceholder[])ArrayUtils.Append(_placeholders, placeholder);

            placeholder.LastTabletLock = Lock;
            placeholder.LastTabletDistance = Vector3.Distance(placeholder.transform.position, transform.position);
            placeholder.LastBodyDistance =
                (Networking.LocalPlayer != null && !Networking.LocalPlayer.IsUserInVR())
                ? Vector3.Distance(placeholder.transform.position, Networking.LocalPlayer.GetPosition())
                : 0;
            placeholder.UpdateIfNeeded();
        }

        public override void OnSlidenLoad(VRCUrl url)
        {
            _reloadButton.interactable = false;
        }

        public override void OnSlidenReady(VRCUrl url, uint maxPage, uint page)
        {
            /* NOP */
        }

        public override void OnSlidenError(SlidenError error)
        {
            /* NOP */
        }

        public override void OnSlidenNavigatePage(uint page)
        {
            /* NOP */
        }

        public override void OnSlidenCanLoad()
        {
            _reloadButton.interactable = true;
        }
    }
}