
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components;
using System;

namespace Chikuwa.Sliden
{
    public enum SlidenStatus
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

        public SlidenStatus Status { get; private set; } = SlidenStatus.Initial;
        public SlidenError Error { get; private set; } = SlidenError.None;
        public uint Page { get; private set; } = 0;
        public uint MaxPage { get; private set; } = 0;
        public bool CanLoad
        {
            get
            {
                return _videoPlayer != null && Time.realtimeSinceStartup > _guardLoadTime;
            }
        }
        private bool _lastCanLoad;

        private VRCUnityVideoPlayer _videoPlayer;

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
        private uint _lastSeekPage;
        private float _pauseTime = float.PositiveInfinity;
        private float _postReadyTime = float.PositiveInfinity;
        private float _followUpSeekTime = float.PositiveInfinity;
        private bool _screenHidden;

        public bool CanNavigatePage
        {
            get
            {
                return _videoPlayer != null && _videoPlayer.IsReady && Status == SlidenStatus.Ready && _url != null && _url == _nextUrl;
            }
        }

        public bool ScreenHidden
        {
            get
            {
                return _screenHidden;
            }
        }

        private MeshRenderer _offscreenRenderer;

        private const float TAIL_ADJUSTMENT_TIME = 0f;

        void Start()
        {
            _videoPlayer = (VRCUnityVideoPlayer)GetComponent(typeof(VRCUnityVideoPlayer));

            _videoPlayer.Loop = false;
            _videoPlayer.EnableAutomaticResync = false;

            var reloadButton = (Button)transform.Find("MainPanel/MainInfo/MainReload").GetComponent(typeof(Button));
            reloadButton.interactable = false;
            _reloadButtons = ArrayUtils.Append(_reloadButtons, reloadButton);

            _guardLoadTime = Time.realtimeSinceStartup + WaitForFirstLoad;
            _needRefreshUI = true;

            _offscreenRenderer = transform.Find("Offscreen").GetComponent<MeshRenderer>();

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
            var now = Time.realtimeSinceStartup;
            _postReadyTime = now + 0.2f;
            _guardLoadTime = now + 5;
            _videoPlayer.Play();
        }

        public override void OnVideoError(VideoError videoError)
        {
            _guardLoadTime = Time.realtimeSinceStartup + 0.1f;
            Status = SlidenStatus.Error;
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
            switch (Status)
            {
                case SlidenStatus.Initial:
                    SetScreenTexture(DefaultScreen);
                    break;
                case SlidenStatus.Loading:
                    SetScreenTexture(LoadingScreen);
                    break;
                case SlidenStatus.Ready:
                    break;
                case SlidenStatus.Error:
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

            var now = Time.realtimeSinceStartup;
            var canLoad = CanLoad;
            if (canLoad != _lastCanLoad)
            {
                _lastCanLoad = canLoad;
                if (canLoad)
                {
                    OnSlidenCanLoad();
                }
                _needRefreshUI = true;
            }

            LoadIfNeeded(now);
            SetReadyIfNeeded(now);
            SeekIfNeeded(now);
            PauseIfNeeded(now);
            ActivateScreenIfNeeded();

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
                if (Status == SlidenStatus.Initial)
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

        private void SetScreenTexture(Texture texture)
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

        private void SetReadyIfNeeded(float now)
        {
            if (now < _postReadyTime)
            {
                return;
            }
            _postReadyTime = float.PositiveInfinity;

            var duration = _videoPlayer.GetDuration();
            var pageCount = (uint)Mathf.Floor(duration);

            MaxPage = pageCount - 1;
            _step = duration / pageCount;
            _overrun = Mathf.Abs(duration - pageCount);

            Status = SlidenStatus.Ready;
            Error = SlidenError.None;
            Page = _nextPage;

            var targetTime = _step * _nextPage;
            var targetOverrun = _overrun;
            var followUpSeekTime = now + targetOverrun + 0.2f;

            // Adjust target time to avoid landing on the tail end
            if (targetTime > _step && _nextPage == MaxPage)
            {
                targetTime -= _step;
                followUpSeekTime += _step / 2f;
            }

            _videoPlayer.Pause();
            _videoPlayer.SetTime(targetTime);
            _lastSeekPage = _nextPage;
            _followUpSeekTime = followUpSeekTime;

            if (_overrun > 0)
            {
                _videoPlayer.Play();
                _pauseTime = now + _overrun;
            }

            var pb = new MaterialPropertyBlock();
            _offscreenRenderer.GetPropertyBlock(pb);
            SetScreenTexture(pb.GetTexture("_MainTex"));

            OnSlidenReady(_url, MaxPage, Page);

            _needRefreshUI = true;
        }

        private void LoadIfNeeded(float now)
        {
            if (_guardLoadTime > now)
            {
                return;
            }

            if (_nextUrl == _url)
            {
                return;
            }

            if (VRCUrl.Equals(_nextUrl, _url))
            {
                _url = _nextUrl;
                return;
            }

            _videoPlayer.Stop();

            _pauseTime = float.PositiveInfinity;
            _url = _nextUrl;

            if (!VRCUrl.Empty.Equals(_url))
            {
                _guardLoadTime = now + 10;
                Status = SlidenStatus.Loading;
                Error = SlidenError.None;
                _videoPlayer.LoadURL(_url);
                OnSlidenLoad(_url);
            }
            else
            {
                _guardLoadTime = now + 5;
                Status = SlidenStatus.Initial;
                Error = SlidenError.None;
                MaxPage = 0;
                OnSlidenReady(_url, 0, 0);
            }

            _needRefreshUI = true;
        }

        private void SeekIfNeeded(float now)
        {
            if (Status != SlidenStatus.Ready || !_videoPlayer.IsReady || _url != _nextUrl)
            {
                return;
            }

            uint currentPage = now > _followUpSeekTime ? (uint)Mathf.Round(Mathf.Max(_videoPlayer.GetTime() - _overrun, 0) / _step) : _lastSeekPage;

            if (currentPage != _nextPage)
            {
                if (_videoPlayer.IsPlaying)
                {
                    _videoPlayer.Pause();
                    _pauseTime = float.PositiveInfinity;
                }
                var targetTime = _step * _nextPage;
                var targetOverrun = _overrun;

                _videoPlayer.SetTime(targetTime);
                _followUpSeekTime = now + targetOverrun + 0.2f;

                Page = _nextPage;
                _lastSeekPage = _nextPage;
                _needRefreshUI = true;

                if (targetOverrun > 0)
                {
                    _videoPlayer.Play();
                    _pauseTime = Time.realtimeSinceStartup + targetOverrun;
                }
                OnSlidenNavigatePage(Page);
                return;
            }
        }

        private void PauseIfNeeded(float now)
        {
            if (Status != SlidenStatus.Ready || !_videoPlayer.IsReady || !_videoPlayer.IsPlaying)
            {
                return;
            }

            if (now > _pauseTime)
            {
                _pauseTime = float.PositiveInfinity;
                _videoPlayer.Pause();
            }
        }

        private void ActivateScreenIfNeeded()
        {
            if (_screenHidden != _nextScreenHidden)
            {
                _screenHidden = _nextScreenHidden;
                foreach (var hidable in _hidables)
                {
                    hidable.SetActive(!_screenHidden);
                }
            }
        }
    }
}