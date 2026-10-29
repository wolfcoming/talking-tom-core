using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 麦克风能量切句：超过门槛才开始，静音约 700ms 结束，最长 8 秒。
/// 呼吸和很短的杂音会被丢掉。一句就绪后立刻关麦。
/// </summary>
public sealed class UtteranceRecorder
{
    public float startRms = 0.045f;
    public float silenceSeconds = 0.7f;
    public float maxSeconds = 8f;
    public float minVoicedSeconds = 0.45f;
    public float prerollSeconds = 0.18f;
    public float sustainToStart = 0.08f;

    public int SampleRate { get; private set; } = 16000;
    public bool IsListening { get; private set; }
    public bool IsRecording { get; private set; }
    public bool HasUtterance { get; private set; }
    public float CurrentRms { get; private set; }
    public string LastError { get; private set; }

    AudioClip _clip;
    string _device;
    bool _micReady;
    int _lastPos;
    float _above;
    float _silence;
    float _recordTime;

    float[] _preroll;
    int _prerollWrite;
    int _prerollFilled;
    readonly List<float> _capture = new List<float>(16000 * 4);
    float[] _interleaved = new float[2048];
    float[] _mono = new float[2048];
    float[] _pending;
    float _pendingDuration;

    float ReleaseRms => startRms * 0.45f;

    public bool StartListening()
    {
        StopListening();
        HasUtterance = false;
        _pending = null;
        LastError = null;
        CurrentRms = 0f;

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            LastError = "没有麦克风。请在系统设置里允许本应用使用麦克风。";
            return false;
        }

        _device = null;
        Microphone.GetDeviceCaps(_device, out var minF, out var maxF);
        var rate = 16000;
        if (minF != 0 || maxF != 0)
        {
            if (rate < minF)
                rate = minF;
            if (maxF > 0 && rate > maxF)
                rate = maxF;
        }

        SampleRate = rate;
        _clip = Microphone.Start(_device, true, 12, SampleRate);
        if (_clip == null)
        {
            LastError = "麦克风打开失败";
            return false;
        }

        IsListening = true;
        IsRecording = false;
        _micReady = false;
        Debug.Log("[Repeat] 麦克风已打开 " + SampleRate + "Hz");
        _lastPos = 0;
        _above = 0f;
        _silence = 0f;
        _recordTime = 0f;
        _capture.Clear();
        EnsurePreroll();
        return true;
    }

    public void StopListening()
    {
        IsListening = false;
        IsRecording = false;
        if (Microphone.IsRecording(_device))
            Microphone.End(_device);
        _clip = null;
        _micReady = false;
    }

    public bool TryTake(out float[] pcm, out float duration)
    {
        if (!HasUtterance || _pending == null)
        {
            pcm = null;
            duration = 0f;
            return false;
        }

        pcm = _pending;
        duration = _pendingDuration;
        _pending = null;
        HasUtterance = false;
        return true;
    }

    public void Pump()
    {
        if (!IsListening || _clip == null)
            return;

        var pos = Microphone.GetPosition(_device);
        if (pos < 0)
            return;

        var length = _clip.samples;
        if (length <= 0)
            return;

        if (!_micReady)
        {
            if (pos <= 0)
                return;
            _lastPos = pos;
            _micReady = true;
            return;
        }

        var count = pos >= _lastPos ? pos - _lastPos : (length - _lastPos) + pos;
        if (count <= 0)
            return;

        // 卡顿时丢掉过长的一段，避免把旧缓冲整段算成一句话
        if (count > SampleRate)
        {
            _lastPos = pos;
            return;
        }

        var channels = Mathf.Max(1, _clip.channels);
        // GetData 会按数组长度读取，必须刚好等于本段采样数
        EnsureScratch(count, channels);

        if (pos >= _lastPos)
        {
            if (!_clip.GetData(_interleaved, _lastPos))
            {
                _lastPos = pos;
                return;
            }
        }
        else
        {
            var firstFrames = length - _lastPos;
            var first = new float[firstFrames * channels];
            if (!_clip.GetData(first, _lastPos))
            {
                _lastPos = pos;
                return;
            }

            Copy(first, 0, _interleaved, 0, first.Length);
            if (pos > 0)
            {
                var second = new float[pos * channels];
                if (_clip.GetData(second, 0))
                    Copy(second, 0, _interleaved, first.Length, second.Length);
            }
        }

        _lastPos = pos;
        Downmix(_interleaved, count, channels, _mono);
        Consume(_mono, count);
    }

    void Consume(float[] mono, int count)
    {
        var frame = Mathf.Max(1, SampleRate / 50);
        var offset = 0;
        while (offset < count)
        {
            var n = Mathf.Min(frame, count - offset);
            var rms = Rms(mono, offset, n);
            Feed(rms, mono, offset, n, n / (float)SampleRate);
            offset += n;
        }
    }

    void Feed(float rms, float[] mono, int offset, int count, float dt)
    {
        if (!IsListening)
            return;

        CurrentRms = rms;
        if (!IsRecording)
        {
            PushPreroll(mono, offset, count);
            if (rms >= startRms)
                _above += dt;
            else
                _above = 0f;

            if (_above >= sustainToStart)
            {
                IsRecording = true;
                _silence = 0f;
                _recordTime = 0f;
                _capture.Clear();
                FlushPreroll(_capture);
                _recordTime += _prerollFilled / (float)SampleRate;
            }

            return;
        }

        for (var i = 0; i < count; i++)
            _capture.Add(mono[offset + i]);
        _recordTime += dt;

        if (rms < ReleaseRms)
            _silence += dt;
        else
            _silence = 0f;

        if (_silence >= silenceSeconds || _recordTime >= maxSeconds)
            CompleteUtterance();
    }

    void CompleteUtterance()
    {
        var voiced = _recordTime - _silence;
        IsRecording = false;
        _above = 0f;
        _silence = 0f;

        if (voiced < minVoicedSeconds || _capture.Count < SampleRate / 10)
        {
            _capture.Clear();
            return;
        }

        var trimmed = TrimTail(_capture);
        _capture.Clear();
        if (trimmed.Length < SampleRate / 10)
            return;

        // 先关麦，再把句子交出去，避免复读被录进去
        StopListening();
        _pending = trimmed;
        _pendingDuration = trimmed.Length / (float)SampleRate;
        HasUtterance = true;
    }

    float[] TrimTail(List<float> src)
    {
        var n = src.Count;
        var win = Mathf.Max(1, SampleRate / 50);
        var i = n;
        while (i - win >= 0)
        {
            if (Rms(src, i - win, win) >= ReleaseRms)
                break;
            i -= win;
        }

        var tail = (int)(SampleRate * 0.12f);
        var end = Mathf.Clamp(i + tail, 1, n);
        if (end < Mathf.Min(n, SampleRate / 4))
            end = n;

        var arr = new float[end];
        src.CopyTo(0, arr, 0, end);
        return arr;
    }

    void EnsurePreroll()
    {
        var n = Mathf.Max(1, (int)(SampleRate * prerollSeconds));
        if (_preroll != null && _preroll.Length == n)
        {
            _prerollWrite = 0;
            _prerollFilled = 0;
            return;
        }

        _preroll = new float[n];
        _prerollWrite = 0;
        _prerollFilled = 0;
    }

    void PushPreroll(float[] src, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _preroll[_prerollWrite] = src[offset + i];
            _prerollWrite = (_prerollWrite + 1) % _preroll.Length;
            if (_prerollFilled < _preroll.Length)
                _prerollFilled++;
        }
    }

    void FlushPreroll(List<float> dst)
    {
        var start = (_prerollWrite - _prerollFilled + _preroll.Length) % _preroll.Length;
        for (var i = 0; i < _prerollFilled; i++)
            dst.Add(_preroll[(start + i) % _preroll.Length]);
    }

    void EnsureScratch(int frames, int channels)
    {
        var n = frames * channels;
        if (_interleaved.Length != n)
            _interleaved = new float[n];
        if (_mono.Length < frames)
            _mono = new float[frames];
    }

    static void Downmix(float[] interleaved, int frames, int channels, float[] mono)
    {
        if (channels == 1)
        {
            for (var i = 0; i < frames; i++)
                mono[i] = interleaved[i];
            return;
        }

        for (var i = 0; i < frames; i++)
        {
            var s = 0f;
            var baseIndex = i * channels;
            for (var c = 0; c < channels; c++)
                s += interleaved[baseIndex + c];
            mono[i] = s / channels;
        }
    }

    static void Copy(float[] src, int srcOffset, float[] dst, int dstOffset, int count)
    {
        for (var i = 0; i < count; i++)
            dst[dstOffset + i] = src[srcOffset + i];
    }

    static float Rms(float[] src, int offset, int count)
    {
        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            var s = src[offset + i];
            sum += s * s;
        }

        return Mathf.Sqrt((float)(sum / Mathf.Max(1, count)));
    }

    static float Rms(List<float> src, int offset, int count)
    {
        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            var s = src[offset + i];
            sum += s * s;
        }

        return Mathf.Sqrt((float)(sum / Mathf.Max(1, count)));
    }
}
