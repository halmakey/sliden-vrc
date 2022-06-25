
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SpeakerTablet : UdonSharpBehaviour
{
    public UdonBehaviour Sliden;
    public UdonBehaviour Placeholder;
    public UdonBehaviour SpeakerScreenToggleButton;
    public UdonBehaviour SpeakerLockToggleButton;

    private float _handIntaractableDistance = 0.3f;
    private float _bodyIntaractableDistance = 0.6f;

    [UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(ScreenOn))]
    private bool _screenOn = true;

    public bool ScreenOn
    {
        get { return _screenOn; }
        set
        {
            _screenOn = value;

            Sliden.SendCustomEvent(value ? "SwitchAllScreenOn" : "SwitchAllScreenOff");
            SpeakerScreenToggleButton.SendCustomEvent(value ? "SetOn" : "SetOff");
        }
    }

    private bool _lock = true;
    public bool Lock
    {
        get { return _lock; }
        set
        {
            _lock = value;
            _tabletPickup.Drop();
            SpeakerLockToggleButton.SendCustomEvent(value ? "SetOn" : "SetOff");

            RefreshUI();
        }
    }

    private VRC_Pickup _tabletPickup;
    private Collider _placeholderCollider;
    private AudioSource _chime;
    private GameObject _canvas;
    private bool _lastPlaceholderContacts;
    private bool _lastPlaceholderClosing;
    private bool _lastTabletClosing;
    private bool _lastTabletFacing;
    private bool _lastOwned;

    private bool PlaceholderContacts { set { _needRefreshUI |= _lastPlaceholderContacts != value; _lastPlaceholderContacts = value; } }
    private bool PlaceholderClosing { set { _needRefreshUI |= _lastPlaceholderClosing != value; _lastPlaceholderClosing = value; } }
    private bool TabletClosing { set { _needRefreshUI |= _lastTabletClosing != value; _lastTabletClosing = value; } }
    private bool TabletFacing { set { _needRefreshUI |= _lastTabletFacing != value; _lastTabletFacing = value; } }
    private bool Owned { set { _lastOwned |= _lastOwned != value; _lastOwned = value; } }

    private bool _needRefreshUI = true;

    private void Start()
    {
        _tabletPickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        _placeholderCollider = (Collider)Placeholder.gameObject.GetComponent(typeof(Collider));
        _chime = (AudioSource)GetComponent(typeof(AudioSource));
        _canvas = GetComponentsInChildren(typeof(Canvas))[0].gameObject;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == _placeholderCollider)
        {
            PlaceholderContacts = true;
            RefreshUI();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == _placeholderCollider)
        {
            PlaceholderContacts = false;
            RefreshUI();
        }
    }

    private void SyncState()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        RequestSerialization();
    }

    public void ToggleScreen()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        ScreenOn = !ScreenOn;
        SyncState();
    }

    public void ToggleLock()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        Lock = !Lock;
        SyncState();
    }

    public void ResetPosition()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _tabletPickup.Drop();
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void PlayChime()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _chime.Play();
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "OnPlayChime");
    }

    public void OnPlayChime()
    {
        _chime.Play();
    }

    private void Update()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null)
        {
            PlaceholderClosing = true;
            TabletClosing = true;

            if (_needRefreshUI)
            {
                RefreshUI();
            }
            return;
        }

        var placeholderPosition = Placeholder.gameObject.transform.position;
        var tabletPosition = gameObject.transform.position;
        var tabletForward = gameObject.transform.forward;

        var playerLeftHandPosition = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
        var playerRightHandPosition = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        var playerHeadPosition = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

        var targetDirection = playerHeadPosition - tabletPosition;
        TabletFacing = Vector3.Dot(tabletForward, targetDirection.normalized) > 0;
        Owned = Networking.IsOwner(gameObject);

        if (playerLeftHandPosition == Vector3.zero || !player.IsUserInVR())
        {
            Vector3 bodyPosition = player.GetPosition();
            bodyPosition.y = placeholderPosition.y;
            float placeholderBodyDistance = (bodyPosition - placeholderPosition).magnitude;
            bodyPosition.y = tabletPosition.y;
            float tabletBodyDistance = (bodyPosition - tabletPosition).magnitude;

            PlaceholderClosing = placeholderBodyDistance < _bodyIntaractableDistance;
            TabletClosing = tabletBodyDistance < _bodyIntaractableDistance;

            if (_needRefreshUI)
            {
                RefreshUI();
            }
            return;
        }

        float placeholderDistance = Mathf.Min(
            (playerLeftHandPosition - placeholderPosition).magnitude,
            (playerRightHandPosition - placeholderPosition).magnitude
        );
        float tabletDistance = Mathf.Min(
            (playerLeftHandPosition - tabletPosition).magnitude,
            (playerRightHandPosition - tabletPosition).magnitude
        );

        PlaceholderClosing = placeholderDistance < _handIntaractableDistance;
        TabletClosing = tabletDistance < _handIntaractableDistance;

        if (_needRefreshUI)
        {
            RefreshUI();
        }
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject))
        {
            return;
        }
        RequestSerialization();
    }

    private void RefreshUI()
    {
        Placeholder.DisableInteractive = _lastPlaceholderContacts || !_lastPlaceholderClosing;
        _tabletPickup.pickupable = (_lastTabletFacing || _lastOwned) && !_lock && _lastTabletClosing;
        _canvas.SetActive(_lastTabletFacing);
    }
}
