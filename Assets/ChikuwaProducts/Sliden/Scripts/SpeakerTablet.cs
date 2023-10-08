
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

        private AudioSource _chime;
        private UdonBehaviour _hideButton;
        private VRCUrlInputField _urlInputField;
        private Text _currentUrlText;
        private Button _resetButton;
        private Text _pageText;
        private Text _messageText;
        private Button[] _nextButtons = Array.Empty<Button>();
        private Button[] _prevButtons = Array.Empty<Button>();

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

            _nextButtons = ArrayUtils.Append(_nextButtons, (Button)transform.Find("Canvas/Next").GetComponent(typeof(Button)));
            _nextButtons = ArrayUtils.Append(_nextButtons, (Button)transform.Find("Canvas/SpeakerControl/Next").GetComponent(typeof(Button)));

            _prevButtons = ArrayUtils.Append(_prevButtons, (Button)transform.Find("Canvas/Prev").GetComponent(typeof(Button)));
            _prevButtons = ArrayUtils.Append(_prevButtons, (Button)transform.Find("Canvas/SpeakerControl/Prev").GetComponent(typeof(Button)));

            Lock = false;

            InitializeSyncTablet();
        }

        public void ToggleHide()
        {
            Sliden.SetScreenHidden(!Sliden.ScreenHidden);
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
            _hideButton.SendCustomEvent(Sliden.ScreenHidden ? "SetOff" : "SetOn");
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

        public override void OnSlidenLoad(VRCUrl url)
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

        public override void OnSlidenReady(VRCUrl url, uint maxPage, uint page)
        {
            base.OnSlidenReady(url, maxPage, page);
            _pageText.enabled = !VRCUrl.Empty.Equals(url);
            _pageText.text = GetPageText(maxPage, page);
            _messageText.enabled = false;

            var showUrl = !VRCUrl.Empty.Equals(url) && Equals(url, _urlInputField.GetUrl());
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

        public override void OnSlidenError(SlidenError error)
        {
            base.OnSlidenError(error);
            _messageText.enabled = true;
            _messageText.text = GetMessage(error);
            _pageText.enabled = false;
        }

        public override void OnSlidenNavigatePage(uint page)
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

        public override void OnSlidenCanLoad()
        {
            base.OnSlidenCanLoad();
            _resetButton.interactable = true;
        }

        public override void OnSlidenScreenHiddenChanged(bool hidden)
        {
            _hideButton.SendCustomEvent(Sliden.ScreenHidden ? "SetOff" : "SetOn");
        }

        public void Unlock()
        {           
            Lock = false;
        }

        public override void OnPickup()
        {
            base.OnPickup();
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Unlock));
        }
    }
}