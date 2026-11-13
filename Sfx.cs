using System;
using System.IO;
using UnityEngine;

/// <summary>Звуковые эффекты, синтезируемые кодом (аудиофайлы в проекте не нужны).</summary>
public static class Sfx
{
    static AudioSource src;
    public static AudioClip Coin, Crystal, Bonus, Hit, Win, Lose, Click, Place;

    public static void Init(GameObject host)
    {
        src = host.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (Coin != null) return;
        Coin = Seq("coin", 988, 0.07f, 1319, 0.16f);
        Crystal = Seq("crystal", 784, 0.07f, 1046, 0.07f, 1568, 0.2f);
        Bonus = Seq("bonus", 523, 0.08f, 659, 0.08f, 784, 0.08f, 1046, 0.08f, 1318, 0.25f);
        Win = Seq("win", 523, 0.14f, 659, 0.14f, 784, 0.14f, 1046, 0.4f);
        Lose = Seq("lose", 392, 0.2f, 330, 0.2f, 262, 0.45f);
        Click = Seq("click", 700, 0.05f);
        Place = Seq("place", 440, 0.06f, 660, 0.12f);
        Hit = Sweep("hit", 260, 70, 0.3f);
    }

    public static void Play(AudioClip c)
    {
        if (src != null && c != null) src.PlayOneShot(c, 0.8f);
    }

    // Последовательность тонов: пары (частота, длительность).
    static AudioClip Seq(string name, params float[] fd)
    {
        const int sr = 44100;
        int total = 0;
        for (int k = 1; k < fd.Length; k += 2) total += (int)(fd[k] * sr);
        var data = new float[total];
        int i = 0;
        for (int k = 0; k < fd.Length; k += 2)
        {
            int n = (int)(fd[k + 1] * sr);
            for (int j = 0; j < n; j++)
            {
                float t = j / (float)sr;
                float env = Mathf.Exp(-3.5f * j / n) * Mathf.Min(1f, j / 200f);
                float s = Mathf.Sin(2f * Mathf.PI * fd[k] * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * fd[k] * t) * 0.15f;
                data[i++] = s * env * 0.5f;
            }
        }
        var clip = AudioClip.Create(name, total, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Sweep(string name, float f0, float f1, float dur)
    {
        const int sr = 44100;
        int n = (int)(dur * sr);
        var data = new float[n];
        float phase = 0;
        for (int j = 0; j < n; j++)
        {
            float u = j / (float)n;
            float f = Mathf.Lerp(f0, f1, u);
            phase += 2f * Mathf.PI * f / sr;
            float sq = Mathf.Sign(Mathf.Sin(phase)) * 0.35f + Mathf.Sin(phase) * 0.4f;
            data[j] = sq * (1f - u) * 0.6f;
        }
        var clip = AudioClip.Create(name, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>Сохранение снимка экрана в галерею Android (или в папку приложения).</summary>
public static class Gallery
{
    public static string Save(byte[] png, string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllBytes(path, png);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var res = act.Call<AndroidJavaObject>("getContentResolver"))
            using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
            {
                string url = media.CallStatic<string>("insertImage", res, path, fileName, "AR screenshot");
                if (!string.IsNullOrEmpty(url)) return "галерею (Pictures)";
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Gallery save failed: " + e);
        }
#endif
        return path;
    }
}
