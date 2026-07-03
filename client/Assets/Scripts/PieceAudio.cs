using UnityEngine;

// Distinct, procedurally generated board sounds (no audio assets needed). Each event has its own
// recognizable cue, the way chess.com distinguishes move/capture/check/etc. Percussive "tocks" are a
// sine thump mixed with a short noise burst and an exponential decay, which reads as a wood click.
[RequireComponent(typeof(AudioSource))]
public sealed class PieceAudio : MonoBehaviour
{
    private AudioSource _source;
    private AudioClip _select, _move, _capture, _castle, _check, _promote, _illegal, _gameEnd, _correct, _wrong;

    public static PieceAudio Attach(GameObject host)
    {
        var existing = host.GetComponent<PieceAudio>();
        if (existing != null) return existing;
        return host.AddComponent<PieceAudio>();
    }

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;

        _select  = Tock(330f, 0.05f, 0.10f, 0.30f);
        _move    = Tock(210f, 0.09f, 0.28f, 0.55f);
        _capture = Tock(150f, 0.13f, 0.60f, 0.70f);
        _castle  = Sequence(new[] { (200f, 0f), (200f, 0.10f) }, 0.09f, 0.30f, 0.55f);
        _check   = Sequence(new[] { (520f, 0f), (740f, 0.09f) }, 0.12f, 0.05f, 0.45f);
        _promote = Sequence(new[] { (523f, 0f), (659f, 0.08f), (784f, 0.16f) }, 0.14f, 0.03f, 0.42f);
        _illegal = Tock(110f, 0.18f, 0.15f, 0.45f);
        _gameEnd = Sequence(new[] { (392f, 0f), (523f, 0.12f), (659f, 0.24f) }, 0.2f, 0.02f, 0.4f);
        _correct = Sequence(new[] { (659f, 0f), (880f, 0.09f) }, 0.13f, 0.03f, 0.40f);
        _wrong   = Sequence(new[] { (220f, 0f), (150f, 0.11f) }, 0.17f, 0.20f, 0.45f);
    }

    public void PlaySelect()  => Play(_select);
    public void PlayMove()    => Play(_move);
    public void PlayCapture() => Play(_capture);
    public void PlayCastle()  => Play(_castle);
    public void PlayCheck()   => Play(_check);
    public void PlayPromote() => Play(_promote);
    public void PlayIllegal() => Play(_illegal);
    public void PlayGameEnd() => Play(_gameEnd);
    public void PlayCorrect() => Play(_correct);
    public void PlayWrong()   => Play(_wrong);

    private void Play(AudioClip clip)
    {
        if (KaissaSettings.Sound && clip != null)
            _source.PlayOneShot(clip);
    }

    private const int SampleRate = 44100;

    private static AudioClip Tock(float freq, float duration, float noise, float volume)
    {
        int samples = Mathf.Max(1, (int)(SampleRate * duration));
        var data = new float[samples];
        var rng = new System.Random(unchecked((int)(freq * 1000f) + samples));
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env = Mathf.Exp(-6f * t);
            float sine = Mathf.Sin(2f * Mathf.PI * freq * i / SampleRate);
            float noiseSample = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = (sine * (1f - noise) + noiseSample * noise) * env * volume;
        }
        var clip = AudioClip.Create("tock", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Layered hits at given (frequency, startSeconds) offsets; each hit is a decaying tone.
    private static AudioClip Sequence((float freq, float start)[] hits, float hitDur, float noise, float volume)
    {
        float total = 0f;
        foreach (var h in hits) total = Mathf.Max(total, h.start + hitDur);
        int samples = Mathf.Max(1, (int)(SampleRate * total));
        var data = new float[samples];
        var rng = new System.Random(hits.Length * 7919);

        foreach (var (freq, start) in hits)
        {
            int offset = (int)(SampleRate * start);
            int len = (int)(SampleRate * hitDur);
            for (int i = 0; i < len && offset + i < samples; i++)
            {
                float t = (float)i / len;
                float env = Mathf.Exp(-6f * t);
                float sine = Mathf.Sin(2f * Mathf.PI * freq * i / SampleRate);
                float noiseSample = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[offset + i] += (sine * (1f - noise) + noiseSample * noise) * env * volume;
            }
        }

        for (int i = 0; i < samples; i++)
            data[i] = Mathf.Clamp(data[i], -1f, 1f);

        var clip = AudioClip.Create("seq", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
