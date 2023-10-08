
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Components.Video;
using System;

namespace Chikuwa.Sliden
{
    public enum SlidenState
    {
        Initial,
        Ready,
        Loading,
        Error
    }

    public enum SlidenError
    {
        None,
        Unknown,
        AccessDenied,
        RateLimit,
        InvalidURL,
        Player
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class Sliden : UdonSharpBehaviour
    {
        public Texture2D DefaultScreen;
        public Texture2D LoadingScreen;
        public Texture2D AccessDeniedErrorScreen;
        public Texture2D RateLimitErrorScreen;
        public Texture2D URLPlayerErrorScreen;
        public float WaitForFirstLoad = 0;
        public VRCUrl InitialUrl;

        public SlidenState State { get; private set; } = SlidenState.Initial;
        public SlidenError Error { get; private set; } = SlidenError.None;
        public uint Page { get; private set; } = 0;
        public uint MaxPage { get; private set; } = 0;
        public bool CanReload { get; private set; } = false;

        private VRCAVProVideoPlayer _videoPlayer;

        private float _guardLoadTime = float.PositiveInfinity;
        private bool _needRefreshUI = false;

        private GameObject[] _hidables = Array.Empty<GameObject>();
        private Material[] _screens = Array.Empty<Material>();
        private Button[] _reloadButtons = Array.Empty<Button>();
        private SlidenListener[] _listeners = Array.Empty<SlidenListener>();

        [UdonSynced]
        private uint _nextPage;
        [UdonSynced]
        private VRCUrl _nextUrl = VRCUrl.Empty;
        [UdonSynced]
        private bool _nextScreenHidden;

        private VRCUrl _url;
        private float _step;
        private float _overrun;
        private float _pauseTime = float.PositiveInfinity;
        private bool _screenHidden;

        public bool CanNavigatePage
        {
            get
            {
                return _videoPlayer != null && _videoPlayer.IsReady && State == SlidenState.Ready && _url != null && _url == _nextUrl;
            }
        }

        public bool ScreenHidden
        {
            get
            {
                return _screenHidden;
            }
        }

        void Start()
        {
            _videoPlayer = (VRCAVProVideoPlayer)GetComponent(typeof(VRCAVProVideoPlayer));

            _videoPlayer.Loop = false;
            _videoPlayer.EnableAutomaticResync = false;

            var reloadButton = (Button)transform.Find("MainPanel/MainInfo/MainReload").GetComponent(typeof(Button));
            reloadButton.interactable = false;
            _reloadButtons = ArrayUtils.Append(_reloadButtons, reloadButton);

            _guardLoadTime = Time.realtimeSinceStartup + WaitForFirstLoad;
            _needRefreshUI = true;

            SendCustomNetworkEvent(
                VRC.Udon.Common.Interfaces.NetworkEventTarget.All,
                nameof(InitializeSync)
            );
        }

        public void NextPage()
        {
            if (!CanNavigatePage || _nextPage >= MaxPage)
            {
                return;
            }
            _nextPage++;
            SyncState();
        }

        public void PrevPage()
        {
            if (!CanNavigatePage || _nextPage <= 0)
            {
                return;
            }

            _nextPage--;
            SyncState();
        }

        public void SyncState()
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            RequestSerialization();
        }

        public override void OnVideoReady()
        {
            var duration = _videoPlayer.GetDuration();
            var pageCount = (uint)Mathf.Floor(duration);

            MaxPage = pageCount - 1;
            Page = 0;
            _step = duration / pageCount;
            _overrun = Mathf.Abs(duration - pageCount);
            _guardLoadTime = Time.realtimeSinceStartup + 5;

            State = SlidenState.Ready;
            Error = SlidenError.None;

            _videoPlayer.Pause();

            OnSlidenReady(_url, MaxPage, Page);
            _needRefreshUI = true;
        }

        public override void OnVideoError(VideoError videoError)
        {
            _guardLoadTime = Time.realtimeSinceStartup + 0.1f;
            State = SlidenState.Error;
            switch (videoError)
            {
                case VideoError.AccessDenied:
                    Error = SlidenError.AccessDenied;
                    break;
                case VideoError.RateLimited:
                    Error = SlidenError.RateLimit;
                    _guardLoadTime = Time.realtimeSinceStartup + 5;
                    ReloadLocal();
                    break;
                case VideoError.InvalidURL:
                    Error = SlidenError.InvalidURL;
                    break;
                case VideoError.PlayerError:
                    Error = SlidenError.Player;
                    break;
                default:
                    Error = SlidenError.Unknown;
                    break;
            }

            OnSlidenError(Error);
            _needRefreshUI = true;
        }

        public void RefreshUI()
        {
            _needRefreshUI = false;
            switch (State)
            {
                case SlidenState.Initial:
                    SetScreenTexture(DefaultScreen);
                    break;
                case SlidenState.Loading:
                    SetScreenTexture(LoadingScreen);
                    break;
                case SlidenState.Ready:
                    break;
                case SlidenState.Error:
                    switch (Error)
                    {
                        case SlidenError.None:
                            /* NOP */
                            break;
                        case SlidenError.AccessDenied:
                            SetScreenTexture(AccessDeniedErrorScreen);
                            break;
                        case SlidenError.RateLimit:
                            SetScreenTexture(RateLimitErrorScreen);
                            break;
                        case SlidenError.InvalidURL:
                            SetScreenTexture(URLPlayerErrorScreen);
                            break;
                        case SlidenError.Player:
                            SetScreenTexture(URLPlayerErrorScreen);
                            break;
                        case SlidenError.Unknown:
                        default:
                            SetScreenTexture(URLPlayerErrorScreen);
                            break;
                    }
                    break;
            }
        }

        public void Update()
        {
            if (_videoPlayer == null)
            {
                return;
            }
            var canReload = Time.realtimeSinceStartup > _guardLoadTime;
            if (CanReload != canReload)
            {
                CanReload = canReload;
                if (canReload)
                {
                    OnSlidenCanLoad();
                }
                _needRefreshUI = true;
            }

            if (CanReload && _nextUrl != _url)
            {
                if (VRCUrl.Equals(_nextUrl, _url))
                {
                    _url = _nextUrl;
                }
                else
                {
                    if (_videoPlayer.IsPlaying)
                    {
                        _pauseTime = float.PositiveInfinity;
                    }
                    _videoPlayer.Stop();

                    _url = _nextUrl;

                    OnSlidenLoad(_url);
                    if (!VRCUrl.Empty.Equals(_url))
                    {
                        _guardLoadTime = float.PositiveInfinity;
                        State = SlidenState.Loading;
                        Error = SlidenError.None;
                        _videoPlayer.LoadURL(_url);
                    }
                    else
                    {
                        _guardLoadTime = Time.realtimeSinceStartup + 5;
                        State = SlidenState.Initial;
                        Error = SlidenError.None;
                        MaxPage = 0;
                        OnSlidenReady(_url, 0, 0);
                    }

                    _needRefreshUI = true;
                }
            }

            if (_videoPlayer && _videoPlayer.IsReady && _url == _nextUrl)
            {
                uint page = (uint)Mathf.Round(Mathf.Max(_videoPlayer.GetTime() - _overrun, 0) / _step);
                if (page != _nextPage)
                {
                    if (_videoPlayer.IsPlaying)
                    {
                        _videoPlayer.Pause();
                        _pauseTime = float.PositiveInfinity;
                    }
                    var targetPage = _nextPage;
                    _videoPlayer.SetTime(_step * targetPage);
                    Page = _nextPage;
                    _needRefreshUI = true;
                    if (targetPage == _nextPage && _overrun > 0 && !_videoPlayer.IsPlaying)
                    {
                        _videoPlayer.Play();
                        _pauseTime = Time.realtimeSinceStartup + _overrun;
                    }
                    OnSlidenNavigatePage(targetPage);
                }
            }
            if (_pauseTime < Time.realtimeSinceStartup)
            {
                _pauseTime = float.PositiveInfinity;
                if (_videoPlayer.IsPlaying)
                {
                    _videoPlayer.Pause();
                }
            }
            if (_screenHidden != _nextScreenHidden)
            {
                _screenHidden = _nextScreenHidden;
                foreach (var hidable in _hidables)
                {
                    hidable.SetActive(!_screenHidden);
                }
            }

            if (_needRefreshUI)
            {
                RefreshUI();
            }
        }

        public void Load(VRCUrl url)
        {
            if (url == null || Equals(url, VRCUrl.Empty))
            {
                return;
            }

            _nextUrl = url;
            _nextPage = 0;

            _needRefreshUI = true;
            SyncState();
        }

        public void ReloadLocal()
        {
            _url = null;
            _needRefreshUI = true;
        }

        public void InitializeSync()
        {
            if (Networking.IsOwner(gameObject))
            {
                if (State == SlidenState.Initial)
                {
                    _nextUrl = InitialUrl;
                    _nextPage = 0;
                }
                SyncState();
                return;
            }
            RequestSerialization();
        }

        public void ResetUrl()
        {
            _nextUrl = InitialUrl;
            _nextPage = 0;
            _needRefreshUI = true;

            SyncState();
        }

        internal void AddHidable(GameObject hidable)
        {
            hidable.SetActive(!_screenHidden);
            _hidables = ArrayUtils.Append(_hidables, hidable);
            _needRefreshUI = true;
        }

        internal void AddScreen(GameObject screen)
        {
            var renderer = (Renderer)screen.GetComponent(typeof(Renderer));
            _screens = ArrayUtils.Append(_screens, renderer.material);
            _needRefreshUI = true;
        }

        private void SetScreenTexture(Texture2D texture)
        {
            foreach (var screen in _screens)
            {
                screen.SetTexture("_MainTex", texture);
            }
        }

        internal void AddListener(SlidenListener listener)
        {
            _listeners = ArrayUtils.Append(_listeners, listener);
        }

        internal void RemoveListener(SlidenListener listener)
        {
            _listeners = ArrayUtils.Remove(_listeners, listener);
        }

        private void OnSlidenLoad(VRCUrl url)
        {
            foreach (var listener in _listeners)
            {
                listener.OnSlidenLoad(url);
            }
            foreach (var button in _reloadButtons)
            {
                button.interactable = false;
            }
        }

        private void OnSlidenReady(VRCUrl url, uint maxPage, uint page)
        {
            foreach (var listener in _listeners)
            {
                listener.OnSlidenReady(url, maxPage, page);
            }
        }

        private void OnSlidenError(SlidenError error)
        {
            foreach (var listener in _listeners)
            {
                listener.OnSlidenError(error);
            }
            foreach (var button in _reloadButtons)
            {
                button.interactable = false;
            }
        }

        private void OnSlidenNavigatePage(uint page)
        {
            foreach (var listener in _listeners)
            {
                listener.OnSlidenNavigatePage(page);
            }
        }

        private void OnSlidenCanLoad()
        {
            foreach (var listener in _listeners)
            {
                listener.OnSlidenCanLoad();
            }
            foreach (var button in _reloadButtons)
            {
                button.interactable = true;
            }
        }

        public void SetScreenHidden(bool screenHidden)
        {
            _nextScreenHidden = screenHidden;
            SyncState();
        }
    }
}