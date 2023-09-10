
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;
using System;

namespace Chikuwa.Sliden
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class SpeakerTablet : Tablet
    {
        internal static string GetPageText(uint maxPage, uint page)
        {
            return (page + 1) + "/" + (maxPage + 1);
        }

        internal static string GetMessage(SlidenError error)
        {
            switch (error)
            {
                case SlidenError.Unknown:
                    return "Unknown";
                case SlidenError.AccessDenied:
                    return "AccessDenied";
                case SlidenError.RateLimit:
                    return "RateLimit";
                case SlidenError.InvalidURL:
                    return "InvalidURL";
                case SlidenError.Player:
                    return "PlayerError";
                default:
                    return "";
            }
        }

        [UdonSynced]
        private bool _nextLock;

        [UdonSynced]
        private bool _nextScreenOn;

        private AudioSource _chime;
        private UdonBehaviour _hideButton;
        private VRCUrlInputField _urlInputField;
        private Text _currentUrlText;
        private Button _resetButton;
        private Text _pageText;
        private Text _messageText;
        private Button[] _nextButtons = Array.Empty<Button>();
        private Button[] _prevButtons = Array.Empty<Button>();

        private bool _screenOn;
        private bool _initialized;

        private bool CanNext { get { return Sliden != null && Sliden.Page < Sliden.MaxPage; } }
        private bool CanPrev { get { return Sliden != null && Sliden.Page > 0; } }

        protected override void Start()
        {
            base.Start();

            _chime = (AudioSource)GetComponent(typeof(AudioSource));

            _hideButton = (UdonBehaviour)transform.Find("Canvas/SpeakerControl/SpeakerHide").GetComponent(typeof(UdonBehaviour));

            _resetButton = (Button)transform.Find("Canvas/SpeakerReset").GetComponent(typeof(Button));

            _urlInputField = (VRCUrlInputField)transform.Find("Canvas/SpeakerUrlInputField").GetComponent(typeof(VRCUrlInputField));
            _currentUrlText = (Text)_urlInputField.transform.Find("CurrentUrl").GetComponent(typeof(Text));

            _pageText = (Text)transform.Find("Canvas/SpeakerControl/SpeakerPage").GetComponent(typeof(Text));
            _messageText = (Text)transform.Find("Canvas/SpeakerControl/SpeakerMessage").GetComponent(typeof(Text));
            
            _nextButtons = (Button[])ArrayUtils.Append(_nextButtons, (Button)transform.Find("Canvas/Next").GetComponent(typeof(Button)));
            _nextButtons = (Button[])ArrayUtils.Append(_nextButtons, (Button)transform.Find("Canvas/SpeakerControl/Next").GetComponent(typeof(Button)));

            _prevButtons = (Button[])ArrayUtils.Append(_prevButtons, (Button)transform.Find("Canvas/Prev").GetComponent(typeof(Button)));
            _prevButtons = (Button[])ArrayUtils.Append(_prevButtons, (Button)transform.Find("Canvas/SpeakerControl/Prev").GetComponent(typeof(Button)));

            _screenOn = true;
            Lock = false;

            SendCustomNetworkEvent(
                VRC.Udon.Common.Interfaces.NetworkEventTarget.All,
                nameof(InitializeSyncTablet)
            );
        }

        public void ToggleHide()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _nextScreenOn = !_screenOn;
            RequestSerialization();
        }

        public override void ToggleLock()
        {
            base.ToggleLock();
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _nextLock = !_nextLock;
            RequestSerialization();
        }

        public override void ResetPosition(Transform target)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            Pickup.Drop();
            transform.position = target.position;
            transform.rotation = target.rotation;

            RequestSerialization();
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(ResetPositionAll));
        }

        public void ResetPositionAll()
        {
            Pickup.Drop();
        }

        public void PlayChime()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _chime.Play();
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(OnPlayChime));
        }

        public void OnPlayChime()
        {
            _chime.Play();
        }

        protected override void Update()
        {
            if (!_initialized)
            {
                return;
            }

            base.Update();

            if (_nextScreenOn != _screenOn)
            {
                _screenOn = _nextScreenOn;
                Sliden.SetHideAllHidables(!_screenOn);
                _hideButton.SendCustomEvent(_screenOn ? "SetOn" : "SetOff");

                if (Sliden != null)
                {
                    Sliden.RefreshUI();
                }
            }

            if (_nextLock != Lock)
            {
                Pickup.Drop();
                Lock = _nextLock;
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

        public void InitializeSyncTablet()
        {
            if (Networking.IsOwner(gameObject))
            {
                _nextLock = Lock;
                _nextScreenOn = _screenOn;
                RequestSerialization();
            }

            _initialized = true;
        }

        public void OnUrlChanged()
        {
            _currentUrlText.text = "";
            if (Sliden != null)
            {
                Sliden.Load(_urlInputField.GetUrl());
            }
        }

        public void ResetUrl()
        {
            _urlInputField.SetUrl(VRCUrl.Empty);
            _currentUrlText.text = "";

            if (Sliden != null)
            {
                Sliden.ResetUrl();
            }
        }

        internal override void OnSlidenLoad(VRCUrl url)
        {
            base.OnSlidenLoad(url);
            _resetButton.interactable = false;
            _pageText.enabled = false;
            _messageText.enabled = true;
            _messageText.text = "Loading...";

            foreach (var button in _nextButtons)
            {
                button.gameObject.SetActive(false);
            }
            foreach (var button in _prevButtons)
            {
                button.gameObject.SetActive(false);
            }
            if (!VRCUrl.Equals(url, _urlInputField.GetUrl()))
            {
                _urlInputField.SetUrl(VRCUrl.Empty);
                var color = _urlInputField.placeholder.color;
                color.a = 0.5f;
                _urlInputField.placeholder.color = color;
                _currentUrlText.text = "";
            }
        }

        internal override void OnSlidenReady(VRCUrl url, uint maxPage, uint page)
        {
            base.OnSlidenReady(url, maxPage, page);
            _pageText.enabled = !VRCUrl.Empty.Equals(url);
            _pageText.text = GetPageText(maxPage, page);
            _messageText.enabled = false;

            var hasPages = maxPage > 0;
            var showUrl = !VRCUrl.Empty.Equals(url) && VRCUrl.Equals(url, _urlInputField.GetUrl());
            _urlInputField.SetUrl(VRCUrl.Empty);
            var color = _urlInputField.placeholder.color;
            color.a = showUrl ? 0 : 0.5f;
            _urlInputField.placeholder.color = color;
            _currentUrlText.text = showUrl ? url.ToString() : "";

            var pageEnabled = maxPage > 0;
            foreach (var button in _nextButtons)
            {
                button.gameObject.SetActive(pageEnabled);
                button.enabled = pageEnabled;
                button.interactable = CanNext;
            }
            foreach (var button in _prevButtons)
            {
                button.gameObject.SetActive(pageEnabled);
                button.enabled = pageEnabled;
                button.interactable = CanPrev;
            }
        }

        internal override void OnSlidenError(SlidenError error)
        {
            base.OnSlidenError(error);
            _messageText.enabled = true;
            _messageText.text = GetMessage(error);
            _pageText.enabled = false;
        }

        internal override void OnSlidenNavigatePage(uint page)
        {
            base.OnSlidenNavigatePage(page);
            _pageText.text = GetPageText(Sliden.MaxPage, page);

            foreach (var button in _nextButtons)
            {
                button.interactable = CanNext;
            }
            foreach (var button in _prevButtons)
            {
                button.interactable = CanPrev;
            }
        }

        internal override void OnSlidenCanLoad()
        {
            base.OnSlidenCanLoad();
            _resetButton.interactable = true;
        }
    }
}