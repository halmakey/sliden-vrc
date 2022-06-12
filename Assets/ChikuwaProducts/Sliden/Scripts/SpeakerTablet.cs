
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

            RefreshInteractable();
        }
    }

    private VRC_Pickup _tabletPickup;
    private Collider _placeholderCollider;
    private AudioSource _chime;
    private bool _placeholderContacts;
    private bool _placeholderClosing;
    private bool _tabletClosing;

    private void Start()
    {
        _tabletPickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        _placeholderCollider = (Collider)Placeholder.gameObject.GetComponent(typeof(Collider));
        _chime = (AudioSource)GetComponent(typeof(AudioSource));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == _placeholderCollider)
        {
            _placeholderContacts = true;
            RefreshInteractable();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == _placeholderCollider)
        {
            _placeholderContacts = false;
            RefreshInteractable();
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
            _placeholderClosing = true;
            _tabletClosing = true;

            RefreshInteractable();
            return;
        }

        Vector3 placeholderPosition = Placeholder.gameObject.transform.position;
        Vector3 tabletPosition = gameObject.transform.position;

        Vector3 playerLeftHandPosition = player.GetBonePosition(HumanBodyBones.LeftHand);
        Vector3 playerRightHandPosition = player.GetBonePosition(HumanBodyBones.RightHand);

        if (playerLeftHandPosition == Vector3.zero || !player.IsUserInVR())
        {
            Vector3 bodyPosition = player.GetPosition();
            bodyPosition.y = placeholderPosition.y;
            float placeholderBodyDistance = (bodyPosition - placeholderPosition).magnitude;
            bodyPosition.y = tabletPosition.y;
            float tabletBodyDistance = (bodyPosition - tabletPosition).magnitude;

            _placeholderClosing = placeholderBodyDistance < _bodyIntaractableDistance;
            _tabletClosing = tabletBodyDistance < _bodyIntaractableDistance;

            RefreshInteractable();
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

        _placeholderClosing = placeholderDistance < _handIntaractableDistance;
        _tabletClosing = tabletDistance < _handIntaractableDistance;

        RefreshInteractable();
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject))
        {
            return;
        }
        RequestSerialization();
    }

    private void RefreshInteractable()
    {
        Placeholder.DisableInteractive = _placeholderContacts || !_placeholderClosing;
        _tabletPickup.pickupable = !_lock && _tabletClosing;
    }
}
