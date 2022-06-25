
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Components.Video;
using System;
using System.Linq;

public class Sliden : UdonSharpBehaviour
{
    private readonly uint PageMask = 0xFFFF;

    public UnityEngine.UI.Text DebugText;
    public VRCUnityVideoPlayer VideoPlayer;
    public UnityEngine.UI.Button NextButton;
    public UnityEngine.UI.Button PrevButton;
    public UnityEngine.UI.Text[] PageTexts;
    public UnityEngine.UI.Text[] MessageTexts;
    public UnityEngine.UI.Button[] ReloadButtons;
    public UnityEngine.UI.Text[] ChannelTexts;
    public UnityEngine.UI.Text ChannelInputText;
    public UnityEngine.UI.Button ChannelEnterButton;
    public GameObject[] Screens;
    public RenderTexture RenderTexture;
    public float WaitForFirstLoad = 0;

    private GameObject _errorAccessDenied;
    private GameObject _errorURLPlayer;
    private GameObject _errorRateLimit;

    private uint _maxPage = 0;
    private string _message = "Sliden";
    private float _guardLoadTime = float.PositiveInfinity;
    private bool _needRefreshUI = false;
    private VideoError _videError = VideoError.Unknown;

    private VRCUrl[] _channelUrls = Enumerable.Range(0, 10000).Select((i) => new VRCUrl(string.Format("https://vrc-campus.com/video/new_{0}.mp4", i))).ToArray();

    [UdonSynced(UdonSyncMode.None)]
    private uint _nextLocation = 0xFFFF0000;

    private uint _channel = 0xFFFF;

    private uint _page = 0xFFFF;
    private uint _nextChannel
    {
        get { return _nextLocation >> 16; }
        set
        {
            if (_nextChannel == value)
            {
                return;
            }
            _nextLocation = value << 16;
        }
    }

    private uint _nextPage
    {
        get { return _nextLocation & PageMask; }
        set
        {
            _nextLocation = (_nextChannel << 16) | value;
        }
    }

    private bool _canNavigatePage
    {
        get { 
            return VideoPlayer.IsReady && _channel == _nextChannel; 
        }
    }

    void Start()
    {
        _errorAccessDenied = transform.Find("MainPanel/Image/Error/AccessDenied").gameObject;
        _errorURLPlayer = transform.Find("MainPanel/Image/Error/URLPlayer").gameObject;
        _errorRateLimit = transform.Find("MainPanel/Image/Error/RateLimit").gameObject;

        VideoPlayer.EnableAutomaticResync = false;
        VideoPlayer.Loop = false;

        _message = "Waiting for " + WaitForFirstLoad + " sec...";
        _guardLoadTime = Time.realtimeSinceStartup + WaitForFirstLoad;
        _needRefreshUI = true;

        SendCustomNetworkEvent(
            VRC.Udon.Common.Interfaces.NetworkEventTarget.All,
            nameof(InitializeSync)
        );
    }

    public void NextPage()
    {
        if (!_canNavigatePage)
        {
            return;
        }
        _nextPage++;
        SyncState();
    }

    public void PrevPage()
    {
        if (!_canNavigatePage)
        {
            return;
        }

        _nextPage--;
        SyncState();
    }

    public void SyncState()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        RequestSerialization();
    }

    public override void OnVideoReady()
    {
        _message = "";
        _maxPage = (uint)(Math.Ceiling(VideoPlayer.GetDuration()) - 1);
        _guardLoadTime = Time.realtimeSinceStartup + 5;
        _page = 0;

        // Workaround for 7th page issue
        VideoPlayer.Pause();
        VideoPlayer.SetTime(1);
        VideoPlayer.SetTime(0);

        _needRefreshUI = true;
    }

    public override void OnVideoError(VideoError videoError)
    {
        _message = videoError.ToString();
        _guardLoadTime = Time.realtimeSinceStartup + 5;
        _videError = videoError;
        _needRefreshUI = true;
    }

    private void RefreshUI()
    {
        uint page = _page;
        uint channel = _channel;
        bool canNavigatePage = _canNavigatePage;
        bool canLoad = Time.realtimeSinceStartup > _guardLoadTime;
        _needRefreshUI = false;
        PrevButton.enabled = canNavigatePage;
        NextButton.enabled = canNavigatePage;
        PrevButton.interactable = canNavigatePage && page > 0;
        NextButton.interactable = canNavigatePage && page < _maxPage;

        bool channelEnabled = channel != 0xFFFF;
        string channelText = string.Format("#{0:D4}", channel);
        foreach (UnityEngine.UI.Text text in ChannelTexts)
        {
            text.enabled = channelEnabled;
            text.text = channelText;
        }

        string pageText = (page + 1) + "/" + (_maxPage + 1);
        foreach (UnityEngine.UI.Text text in PageTexts)
        {
            text.enabled = canNavigatePage;
            text.text = pageText;
        }

        bool textEnabled = !canNavigatePage && _message.Length > 0;
        foreach (UnityEngine.UI.Text text in MessageTexts)
        {
            text.enabled = textEnabled;
            text.text = _message;
        }

        ChannelEnterButton.enabled = canLoad;
        ChannelEnterButton.interactable = canLoad;

        foreach (UnityEngine.UI.Button button in ReloadButtons)
        {
            button.enabled = canLoad;
            button.interactable = canLoad;
        }

        if (DebugText != null)
        {
            DebugText.text = VideoPlayer.GetTime() + "/" + VideoPlayer.GetDuration();
        }

        _errorAccessDenied.SetActive(_videError == VideoError.AccessDenied);
        _errorURLPlayer.SetActive(_videError == VideoError.InvalidURL || _videError == VideoError.PlayerError);
        _errorRateLimit.SetActive(_videError == VideoError.RateLimited);
    }

    public void Update()
    {
        bool canLoadChannel = Time.realtimeSinceStartup > _guardLoadTime && _nextChannel != 0xFFFF;

        if (_guardLoadTime != 0 && canLoadChannel)
        {
            _guardLoadTime = 0;
            _needRefreshUI = true;
        }

        if (canLoadChannel && _nextChannel != _channel)
        {
            _message = "Loading...";
            _guardLoadTime = float.PositiveInfinity;
            _channel = _nextChannel;
            _videError = VideoError.Unknown;

            VideoPlayer.Stop();
            RenderTexture.Release();
            VRCUrl slideUrl = _channelUrls[_nextChannel];
            VideoPlayer.LoadURL(slideUrl);

            _needRefreshUI = true;
        }

        if (_canNavigatePage)
        {
            float time = VideoPlayer.GetTime();
            float offset = (float)Math.Min(_nextPage - (double)time, 1.0);
            if (offset != 0)
            {
                VideoPlayer.SetTime(time + offset);
                _page = (uint)(time + offset);
                _needRefreshUI = true;
            }
        }

        if (_needRefreshUI)
        {
            RefreshUI();
        }
    }
                                                                
    public void ReloadLocal()
    {
        _message = "Loading...";
        _channel = 0xFFFF; // Reload on update
        _needRefreshUI = true;
    }

    public void InitializeSync()
    {
        if (Networking.IsOwner(gameObject))
        {
            if (_nextChannel == 0xFFFF)
            {
                _nextChannel = 0;
            }
            SyncState();
            return;
        }
        RequestSerialization();
    }

    private void InputChannel(uint num)
    {
        string text = ChannelInputText.text;
        if (text.Length >= 4)
        {
            return;
        }
        ChannelInputText.text = text + num.ToString();
    }

    public void InputChannel0()
    {
        InputChannel(0);
    }

    public void InputChannel1()
    {
        InputChannel(1);
    }

    public void InputChannel2()
    {
        InputChannel(2);
    }

    public void InputChannel3()
    {
        InputChannel(3);
    }

    public void InputChannel4()
    {
        InputChannel(4);
    }

    public void InputChannel5()
    {
        InputChannel(5);
    }

    public void InputChannel6()
    {
        InputChannel(6);
    }

    public void InputChannel7()
    {
        InputChannel(7);
    }

    public void InputChannel8()
    {
        InputChannel(8);
    }

    public void InputChannel9()
    {
        InputChannel(9);
    }

    public void InputChannel10()
    {
        InputChannel(10);
    }

    public void InputChannelEnter()
    {
        string text = ChannelInputText.text;
        if (text.Length == 0)
        {
            return;
        }
        _nextChannel = UInt32.Parse(text);
        ChannelInputText.text = "";

        SyncState();
    }

    public void InputChannelClear()
    {
        ChannelInputText.text = "";
    }

    public void SwitchAllScreenOn()
    {
        foreach (GameObject screen in Screens)
        {
            screen.SetActive(true);
        }
    }

    public void SwitchAllScreenOff()
    {
        foreach (GameObject screen in Screens)
        {
            screen.SetActive(false);
        }
    }
}
