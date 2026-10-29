using UnityEngine;

/// <summary>
/// 只升高音高、不改变时长（语速 = 1）。
/// 和 SoundTouch 的用法对齐：先重采样升高音高，再用叠窗把长度拉回原句。
/// 工程里还没有原生 SoundTouch，复读先走这里，以后可以原样换掉。
/// </summary>
public static class PitchShifter
{
    public static float[] ShiftPitch(float[] mono, float semitones, int sampleRate)
    {
        if (mono == null || mono.Length == 0)
            return System.Array.Empty<float>();

        float[] output;
        if (mono.Length < 64 || Mathf.Abs(semitones) < 0.05f)
        {
            output = (float[])mono.Clone();
        }
        else
        {
            var pitch = Mathf.Pow(2f, semitones / 12f);
            var shortLen = Mathf.Max(64, Mathf.RoundToInt(mono.Length / pitch));
            var shortened = Resample(mono, shortLen);
            output = TimeStretch(shortened, mono.Length);
        }

        FadeEdges(output, sampleRate);
        NormalizePeak(output, 0.85f);
        return output;
    }

    static float[] Resample(float[] input, int outLen)
    {
        outLen = Mathf.Max(1, outLen);
        var output = new float[outLen];
        if (input.Length == 0)
            return output;
        if (outLen == 1 || input.Length == 1)
        {
            output[0] = input[0];
            return output;
        }

        var step = (input.Length - 1) / (float)(outLen - 1);
        for (var i = 0; i < outLen; i++)
        {
            var pos = i * step;
            var i0 = (int)pos;
            var i1 = Mathf.Min(i0 + 1, input.Length - 1);
            var t = pos - i0;
            output[i] = input[i0] * (1f - t) + input[i1] * t;
        }

        return output;
    }

    static float[] TimeStretch(float[] input, int targetLength)
    {
        targetLength = Mathf.Max(1, targetLength);
        var grain = 768;
        if (input.Length < grain * 2)
        {
            grain = Mathf.Max(128, input.Length / 4);
            grain -= grain % 2;
            if (grain < 32)
                grain = 32;
        }

        var synthHop = Mathf.Max(1, grain / 2);
        var hops = Mathf.Max(1, Mathf.CeilToInt(targetLength / (float)synthHop));
        var usable = Mathf.Max(1, input.Length - grain);
        var analysisHop = Mathf.Max(1, usable / hops);
        var window = Hann(grain);
        var output = new float[targetLength];

        var read = 0f;
        var write = 0;
        for (var placed = 0; placed < hops + 2 && write < targetLength; placed++)
        {
            var baseIndex = (int)read;
            if (baseIndex >= input.Length)
                break;

            for (var i = 0; i < grain; i++)
            {
                var wi = write + i;
                if (wi >= targetLength)
                    break;
                var ri = baseIndex + i;
                var s = (ri >= 0 && ri < input.Length) ? input[ri] : 0f;
                output[wi] += s * window[i];
            }

            read += analysisHop;
            write += synthHop;
        }

        return output;
    }

    static float[] Hann(int n)
    {
        var w = new float[n];
        for (var i = 0; i < n; i++)
            w[i] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / n);
        return w;
    }

    static void FadeEdges(float[] pcm, int sampleRate)
    {
        if (pcm.Length < 8)
            return;
        var fade = Mathf.Clamp(sampleRate / 125, 8, pcm.Length / 2);
        for (var i = 0; i < fade; i++)
        {
            var g = i / (float)fade;
            pcm[i] *= g;
            pcm[pcm.Length - 1 - i] *= g;
        }
    }

    static void NormalizePeak(float[] pcm, float targetPeak)
    {
        var peak = 0f;
        for (var i = 0; i < pcm.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(pcm[i]));

        // 几乎没声音时不要把底噪抬起来
        if (peak < 0.02f)
            return;

        var gain = targetPeak / peak;
        for (var i = 0; i < pcm.Length; i++)
            pcm[i] = Mathf.Clamp(pcm[i] * gain, -1f, 1f);
    }
}
