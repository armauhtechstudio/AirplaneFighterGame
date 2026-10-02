using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Synthesizes the UI / feedback sound effects into Assets/AirStrike/Resources/Sfx (16-bit mono WAV),
// played by GameSfx: click, popup, win, fail, star, checkpoint, score, tick, revive, heart_lost.
public static class SfxGenerator
{
    public const string Folder = "Assets/AirStrike/Resources/Sfx";
    const int Rate = 44100;

    [MenuItem("Tools/AirStrike/Generate Sound Effects")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/AirStrike/Resources")) AssetDatabase.CreateFolder("Assets/AirStrike", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/AirStrike/Resources", "Sfx");

        Write("click", Click());
        Write("popup", Popup());
        Write("win", Win());
        Write("fail", Fail());
        Write("star", Bell(880f, 0.9f));
        Write("checkpoint", Checkpoint());
        Write("score", Score());
        Write("tick", Tick());
        Write("revive", Revive());
        Write("heart_lost", HeartLost());

        AssetDatabase.Refresh();
        foreach (string f in Directory.GetFiles(Folder, "*.wav"))
        {
            var importer = AssetImporter.GetAtPath(f.Replace('\\', '/')) as AudioImporter;
            if (importer == null) continue;
            importer.forceToMono = true;
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad; // short clips: no latency
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            importer.defaultSampleSettings = s;
            importer.SaveAndReimport();
        }
        Debug.Log("[SfxGenerator] Sound effects written to " + Folder);
    }

    // ---------------------------------------------------------------- sounds

    // Short metallic tick: a few inharmonic partials + a noise transient, very fast decay
    static float[] Click()
    {
        var b = Buffer(0.07f);
        var rng = new System.Random(1);
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.Exp(-t * 70f);
            float tone = Sin(1900f, t) * 0.5f + Sin(2850f, t) * 0.3f + Sin(4300f, t) * 0.2f;
            float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 400f);
            b[i] = (tone * env + noise * 0.5f) * 0.8f;
        }
        return b;
    }

    // Soft whoosh: noise through a sweeping resonant filter + a gentle rising chirp
    static float[] Popup()
    {
        var b = Buffer(0.32f);
        var rng = new System.Random(2);
        float lp = 0f, bp = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate, p = t / 0.32f;
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(p, 0.6f));
            // state-variable filter sweeping 500 -> 2500 Hz
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Lerp(500f, 2500f, p) / Rate);
            float x = (float)(rng.NextDouble() * 2 - 1);
            lp += f * bp;
            float hp = x - lp - 0.35f * bp;
            bp += f * hp;
            float chirp = Sin(Mathf.Lerp(320f, 640f, p * p) * 1f, t, phaseAccum: true, index: i);
            b[i] = (bp * 0.55f + chirp * 0.18f) * env;
        }
        return b;
    }

    // Rising brass-like arpeggio C5 E5 G5 -> C6 held, with a little vibrato
    static float[] Win()
    {
        var b = Buffer(1.35f);
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        float[] starts = { 0f, 0.12f, 0.24f, 0.38f };
        float[] lengths = { 0.18f, 0.18f, 0.2f, 0.95f };
        for (int n = 0; n < notes.Length; n++)
            AddBrass(b, notes[n], starts[n], lengths[n], n == 3 ? 0.55f : 0.45f);
        AddBell(b, 2093f, 0.38f, 0.8f, 0.12f); // sparkle on the last note
        return Normalize(b, 0.85f);
    }

    // Descending "wah wah wah" G4 E4 C4 (last one slides down), soft square-ish tone
    static float[] Fail()
    {
        var b = Buffer(1.25f);
        float[] notes = { 392f, 329.63f, 261.63f };
        float[] starts = { 0f, 0.26f, 0.52f };
        for (int n = 0; n < 3; n++)
        {
            bool last = n == 2;
            int s0 = (int)(starts[n] * Rate), len = (int)((last ? 0.7f : 0.24f) * Rate);
            double phase = 0;
            for (int i = 0; i < len && s0 + i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = notes[n] * (last ? Mathf.Lerp(1f, 0.82f, t / 0.7f) : 1f);
                phase += 2 * Math.PI * f / Rate;
                float tone = (float)(Math.Sin(phase) + Math.Sin(phase * 3) / 3 * 0.6 + Math.Sin(phase * 5) / 5 * 0.4);
                float wah = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * (last ? 6f : 4f));
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * (last ? 2.5f : 6f));
                b[s0 + i] += tone * env * wah * 0.45f;
            }
        }
        return Normalize(b, 0.75f);
    }

    // Two-note bell chime C6 -> G6
    static float[] Checkpoint()
    {
        var b = Buffer(0.9f);
        AddBell(b, 1046.5f, 0f, 0.5f, 0.55f);
        AddBell(b, 1568f, 0.11f, 0.75f, 0.6f);
        return Normalize(b, 0.8f);
    }

    // Quick bright blip, pitch sweeping up
    static float[] Score()
    {
        var b = Buffer(0.16f);
        double phase = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate, p = t / 0.16f;
            phase += 2 * Math.PI * Mathf.Lerp(880f, 1760f, Mathf.Sqrt(p)) / Rate;
            float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 22f);
            b[i] = (float)(Math.Sin(phase) * 0.7 + Math.Sin(phase * 2) * 0.2) * env;
        }
        return Normalize(b, 0.7f);
    }

    // Wood-block tick
    static float[] Tick()
    {
        var b = Buffer(0.09f);
        var rng = new System.Random(3);
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate;
            float tone = Sin(1250f, t) * 0.7f + Sin(2600f, t) * 0.2f;
            float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 600f) * 0.4f;
            b[i] = tone * Mathf.Exp(-t * 55f) + noise;
        }
        return Normalize(b, 0.75f);
    }

    // Rising power-up sweep with sparkles
    static float[] Revive()
    {
        var b = Buffer(0.9f);
        double phase = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate, p = t / 0.9f;
            phase += 2 * Math.PI * Mathf.Lerp(300f, 1200f, p * p) / Rate;
            float env = Mathf.Min(1f, t / 0.03f) * (1f - p);
            b[i] = (float)(Math.Sin(phase) * 0.5 + Math.Sin(phase * 2) * 0.2 + Math.Sin(phase * 3) * 0.1) * env;
        }
        AddBell(b, 1568f, 0.35f, 0.5f, 0.25f);
        AddBell(b, 2093f, 0.5f, 0.4f, 0.25f);
        return Normalize(b, 0.8f);
    }

    // Low thud: falling sine + a bit of noise
    static float[] HeartLost()
    {
        var b = Buffer(0.45f);
        var rng = new System.Random(4);
        double phase = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate;
            phase += 2 * Math.PI * Mathf.Lerp(150f, 60f, t / 0.45f) / Rate;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 9f);
            float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 60f) * 0.25f;
            b[i] = (float)Math.Sin(phase) * env + noise;
        }
        return Normalize(b, 0.85f);
    }

    // Single bell (star rating; pitched per star at runtime)
    static float[] Bell(float freq, float length)
    {
        var b = Buffer(length);
        AddBell(b, freq, 0f, length, 0.8f);
        return Normalize(b, 0.75f);
    }

    // ---------------------------------------------------------------- building blocks

    static void AddBell(float[] b, float freq, float start, float length, float amp)
    {
        int s0 = (int)(start * Rate), len = (int)(length * Rate);
        float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
        float[] gains = { 1f, 0.45f, 0.22f, 0.1f };
        float[] decays = { 5f, 8f, 13f, 20f };
        for (int i = 0; i < len && s0 + i < b.Length; i++)
        {
            float t = (float)i / Rate, v = 0f;
            for (int k = 0; k < ratios.Length; k++) v += Sin(freq * ratios[k], t) * gains[k] * Mathf.Exp(-t * decays[k]);
            b[s0 + i] += v * amp * Mathf.Min(1f, t / 0.002f);
        }
    }

    static void AddBrass(float[] b, float freq, float start, float length, float amp)
    {
        int s0 = (int)(start * Rate), len = (int)((length + 0.25f) * Rate);
        double phase = 0;
        for (int i = 0; i < len && s0 + i < b.Length; i++)
        {
            float t = (float)i / Rate;
            float vibrato = 1f + 0.006f * Mathf.Sin(t * 2f * Mathf.PI * 5.5f) * Mathf.Clamp01(t / 0.2f);
            phase += 2 * Math.PI * freq * vibrato / Rate;
            double v = 0;
            for (int h = 1; h <= 6; h++) v += Math.Sin(phase * h) / h * (h == 1 ? 1 : 0.7);
            float attack = Mathf.Min(1f, t / 0.03f);
            float release = t > length ? Mathf.Exp(-(t - length) * 14f) : 1f;
            b[s0 + i] += (float)v * attack * release * amp * 0.5f;
        }
    }

    static float Sin(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);

    static double popupPhase;
    static float Sin(float freq, float t, bool phaseAccum, int index)
    {
        if (index == 0) popupPhase = 0;
        popupPhase += 2 * Math.PI * freq / Rate;
        return (float)Math.Sin(popupPhase);
    }

    static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

    static float[] Normalize(float[] b, float peak)
    {
        float max = 0.0001f;
        foreach (float v in b) max = Mathf.Max(max, Mathf.Abs(v));
        for (int i = 0; i < b.Length; i++) b[i] = b[i] / max * peak;
        // tiny fade-out so clips never end with a click
        int fade = Mathf.Min(b.Length, Rate / 200);
        for (int i = 0; i < fade; i++) b[b.Length - 1 - i] *= (float)i / fade;
        return b;
    }

    // ---------------------------------------------------------------- wav

    static void Write(string name, float[] samples)
    {
        string path = Path.Combine(Folder, name + ".wav");
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // mono
            w.Write(Rate);
            w.Write(Rate * 2);      // byte rate
            w.Write((short)2);      // block align
            w.Write((short)16);     // bits
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
