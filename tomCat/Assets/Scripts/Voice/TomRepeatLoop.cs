using UnityEngine;

public enum RepeatPhase
{
    Idle,
    Listening,
    Talking,
    Poke
}

/// <summary>
/// Talking Tom 复读主循环：
/// 待机听着 → 开口进入正在听 → 停顿后关麦 → 升高音高再播放 →
/// 嘴和这句话一起结束 → 再开麦。播放中戳角色会打断。
/// </summary>
[DisallowMultipleComponent]
public class TomRepeatLoop : MonoBehaviour
{
    public static TomRepeatLoop Instance { get; private set; }
    public static Rect HudGuiRect;

    public string characterObjectName = "Robot";

    [Header("Pitch (semitones, tempo stays 1)")]
    [Range(0f, 12f)]
    public float semitones = 7f;

    [Header("Sentence gate")]
    [Range(0.015f, 0.12f)]
    public float startRms = 0.045f;
    public float silenceSeconds = 0.7f;
    public float maxSeconds = 8f;
    public float echoCooldown = 0.45f;

    public RepeatPhase Phase => _phase;

    UtteranceRecorder _recorder;
    AudioSource _source;
    RepeatPoseDriver _pose;
    RepeatPhase _phase = RepeatPhase.Idle;
    bool _armed;
    bool _permissionAsked;
    float _rearmAt;
    float _pokeUntil;
    float _talkStart;
    float _nextMicAttempt;
    float _lastDuration;
    bool _hasSentence;
    float[] _playPcm;
    int _playRate = 16000;
    string _blockReason;
    GUIStyle _label;
    GUIStyle _small;
    Texture2D _pixel;
    int _styleH = -1;

    public static TomRepeatLoop Ensure()
    {
        if (Instance != null)
            return Instance;

        var existing = FindObjectOfType<TomRepeatLoop>();
        if (existing != null)
            return existing;

        var go = new GameObject("VoiceRepeat");
        return go.AddComponent<TomRepeatLoop>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _recorder = new UtteranceRecorder();
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.loop = false;
        _source.volume = 1f;
        RequestPermission();
    }

    void Start()
    {
        if (FindObjectOfType<CharacterStageDirector>() == null)
            Arm();
    }

    void OnDestroy()
    {
        _recorder?.StopListening();
        if (Instance == this)
            Instance = null;
    }

    void OnApplicationPause(bool paused)
    {
        if (_recorder == null)
            return;

        if (paused)
        {
            if (_phase == RepeatPhase.Talking && _source != null)
                _source.Pause();
            else
            {
                _recorder.StopListening();
                if (_phase == RepeatPhase.Listening)
                    _phase = RepeatPhase.Idle;
            }

            return;
        }

        if (_phase == RepeatPhase.Talking && _source != null)
            _source.UnPause();
        else if (_armed && _phase != RepeatPhase.Poke)
            _rearmAt = Time.time + 0.1f;
    }

    public void Arm()
    {
        BindCharacter();
        if (_armed)
            return;
        _armed = true;
        _rearmAt = Time.time;
    }

    public bool TryInterrupt()
    {
        if (_phase != RepeatPhase.Talking)
            return false;

        if (_source != null)
            _source.Stop();
        if (_pose != null)
            _pose.MouthOpen = 0f;

        _phase = RepeatPhase.Poke;
        _pokeUntil = Time.time + 0.7f;
        _pose?.SetMode(PoseMode.Poke);
        return true;
    }

    public void NudgePoke()
    {
        if (TryInterrupt())
            return;
        _pokeUntil = Time.time + 0.7f;
        _pose?.SetMode(PoseMode.Poke);
    }

    void Update()
    {
        if (!_armed)
            return;

        if (!HasMicPermission())
        {
            _blockReason = "需要麦克风权限，用来把你说的话变调后复读";
            RequestPermission();
            return;
        }

        if (_phase == RepeatPhase.Poke)
        {
            _pose?.SetMode(PoseMode.Poke);
            if (Time.time >= _pokeUntil)
            {
                _phase = RepeatPhase.Idle;
                _pose?.SetMode(PoseMode.FollowIdle);
                _rearmAt = Time.time + 0.12f;
            }

            return;
        }

        if (_phase == RepeatPhase.Talking)
        {
            _pose?.SetMode(Time.time < _pokeUntil ? PoseMode.Poke : PoseMode.Talk);
            UpdateMouth();
            if (Time.time > _talkStart + 0.08f && (_source == null || !_source.isPlaying))
                FinishTalk();
            return;
        }

        if (Time.time < _rearmAt)
        {
            _pose?.SetMode(Time.time < _pokeUntil ? PoseMode.Poke : PoseMode.FollowIdle);
            return;
        }

        if (!_recorder.IsListening)
            BeginListening();

        if (!_recorder.IsListening)
        {
            _pose?.SetMode(Time.time < _pokeUntil ? PoseMode.Poke : PoseMode.FollowIdle);
            return;
        }

        _blockReason = null;
        _recorder.startRms = startRms;
        _recorder.silenceSeconds = silenceSeconds;
        _recorder.maxSeconds = maxSeconds;
        _recorder.Pump();

        if (_recorder.TryTake(out var pcm, out var duration))
        {
            Speak(pcm, duration);
            return;
        }

        _phase = _recorder.IsRecording ? RepeatPhase.Listening : RepeatPhase.Idle;
        if (Time.time < _pokeUntil)
            _pose?.SetMode(PoseMode.Poke);
        else
            _pose?.SetMode(_phase == RepeatPhase.Listening ? PoseMode.Listen : PoseMode.FollowIdle);
    }

    void BeginListening()
    {
        if (Time.time < _nextMicAttempt)
            return;

        _recorder.startRms = startRms;
        _recorder.silenceSeconds = silenceSeconds;
        _recorder.maxSeconds = maxSeconds;
        if (!_recorder.StartListening())
        {
            if (_blockReason != _recorder.LastError)
                Debug.LogWarning("[Repeat] " + _recorder.LastError);
            _blockReason = _recorder.LastError;
            _nextMicAttempt = Time.time + 1.2f;
            return;
        }

        _blockReason = null;
        _phase = RepeatPhase.Idle;
    }

    void Speak(float[] pcm, float duration)
    {
        _phase = RepeatPhase.Talking;
        _lastDuration = duration;
        _hasSentence = true;
        _playRate = Mathf.Max(8000, _recorder.SampleRate);
        _playPcm = PitchShifter.ShiftPitch(pcm, semitones, _playRate);

        if (_source.clip != null)
            Destroy(_source.clip);

        var clip = AudioClip.Create("repeat", _playPcm.Length, 1, _playRate, false);
        clip.SetData(_playPcm, 0);
        _source.clip = clip;
        _source.Play();
        _talkStart = Time.time;
        _pose?.SetMode(PoseMode.Talk);
        Debug.Log($"[Repeat] 一句 {_lastDuration:0.00}s，音高 +{semitones:0.0} 半音", this);
    }

    void FinishTalk()
    {
        _phase = RepeatPhase.Idle;
        if (_pose != null)
            _pose.MouthOpen = 0f;
        _pose?.SetMode(PoseMode.FollowIdle);
        _rearmAt = Time.time + echoCooldown;
    }

    void UpdateMouth()
    {
        if (_pose == null || _playPcm == null || _playPcm.Length == 0 || _source == null)
            return;

        var start = Mathf.Clamp(_source.timeSamples, 0, _playPcm.Length - 1);
        var win = Mathf.Max(1, _playRate / 40);
        var end = Mathf.Min(_playPcm.Length, start + win);
        double sum = 0;
        var n = 0;
        for (var i = start; i < end; i++)
        {
            var s = _playPcm[i];
            sum += s * s;
            n++;
        }

        var rms = n == 0 ? 0f : Mathf.Sqrt((float)(sum / n));
        _pose.MouthOpen = Mathf.Clamp01((rms - 0.015f) / 0.12f);
    }

    void BindCharacter()
    {
        var go = GameObject.Find(characterObjectName);
        if (go == null)
            go = GameObject.Find("LittleGirl");
        if (go == null)
            return;

        var idle = go.GetComponent<TomCatIdleMotion>();
        _pose = go.GetComponent<RepeatPoseDriver>();
        if (_pose == null)
            _pose = go.AddComponent<RepeatPoseDriver>();
        _pose.Capture(idle);
    }

    void RequestPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_permissionAsked)
            return;
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            return;
        _permissionAsked = true;
        UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
    }

    static bool HasMicPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
        return true;
#endif
    }

    void OnGUI()
    {
        var h = Mathf.Clamp(Screen.height * 0.16f, 168f, 250f);
        var safe = Screen.safeArea;
        var bottom = Screen.height - Mathf.Max(8f, safe.y + 8f);
        var left = Mathf.Max(12f, safe.x + 8f);
        var width = Mathf.Max(120f, safe.width - 16f);
        HudGuiRect = new Rect(left, bottom - h, width, h);
        EnsureStyles();

        GUILayout.BeginArea(HudGuiRect, GUI.skin.box);
        GUILayout.Label(PhaseText(), _label);

        if (_hasSentence)
            GUILayout.Label($"一句结束  {_lastDuration:0.00} 秒", _small);

        if (_phase == RepeatPhase.Talking || _phase == RepeatPhase.Poke)
            GUILayout.Label("麦克风已关闭", _small);
        else if (!string.IsNullOrEmpty(_blockReason))
            GUILayout.Label(_blockReason, _small);
        else
            DrawLevel();

        GUILayout.Label($"音高  +{semitones:0.0} 半音", _small);
        semitones = GUILayout.HorizontalSlider(semitones, 0f, 12f);
        GUILayout.Label($"门槛  {startRms:0.000}    静音 {silenceSeconds:0.0}s", _small);
        startRms = GUILayout.HorizontalSlider(startRms, 0.015f, 0.12f);
        GUILayout.EndArea();
    }

    void DrawLevel()
    {
        var bar = GUILayoutUtility.GetRect(20f, 16f);
        var rms = _recorder != null ? _recorder.CurrentRms : 0f;
        var span = 0.15f;
        var t = Mathf.Clamp01(rms / span);
        DrawRect(bar, new Color(0f, 0f, 0f, 0.35f));
        var fill = new Rect(bar.x, bar.y, bar.width * t, bar.height);
        var hot = rms >= startRms;
        DrawRect(fill, hot ? new Color(0.95f, 0.42f, 0.18f) : new Color(0.35f, 0.75f, 0.42f));
        var markX = bar.x + bar.width * Mathf.Clamp01(startRms / span);
        DrawRect(new Rect(markX, bar.y, 2f, bar.height), Color.white);
    }

    void DrawRect(Rect rect, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Pixel());
        GUI.color = prev;
    }

    Texture2D Pixel()
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        return _pixel;
    }

    string PhaseText()
    {
        if (!_armed)
            return "准备中";
        switch (_phase)
        {
            case RepeatPhase.Listening: return "正在听";
            case RepeatPhase.Talking: return "复读中";
            case RepeatPhase.Poke: return "被戳了一下";
            default: return "待机";
        }
    }

    void EnsureStyles()
    {
        if (_label != null && _styleH == Screen.height)
            return;

        _styleH = Screen.height;
        var size = Mathf.Clamp(Screen.height / 28, 20, 34);
        _label = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        _small = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(16, size - 6),
            alignment = TextAnchor.MiddleLeft
        };
    }
}
