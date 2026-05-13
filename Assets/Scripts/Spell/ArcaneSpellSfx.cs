using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneVR.Spell
{
    public enum ArcaneSpellSfxCue
    {
        ElementArm,
        VoiceConfirm,
        SpellCast,
        ComboReady,
        ComboCast
    }

    public static class ArcaneSpellSfx
    {
        private const int SampleRate = 44100;
        private const string ResourceSfxPath = "ArcaneVR/Sfx";
        private static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>();
        private static readonly HashSet<string> ResourceLoadLogs = new HashSet<string>();
        private static readonly HashSet<string> ResourceMissingLogs = new HashSet<string>();

        public static float Play(AudioSource audioSource, ElementType element, ArcaneSpellSfxCue cue, float volume = 1f)
        {
            if (audioSource == null || element == ElementType.None)
                return 0f;

            var clip = GetClip(element, cue);
            if (clip == null)
                return 0f;

            audioSource.pitch = 1f;
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            return clip.length;
        }

        public static void PlayCombo(AudioSource audioSource, SpellId spellId, ArcaneSpellSfxCue cue, float volume = 1f)
        {
            if (audioSource == null ||
                !SpellHitData.TryGetComboElements(spellId, out var first, out var second))
            {
                return;
            }

            Play(audioSource, first, cue, volume * 0.82f);
            Play(audioSource, second, cue, volume * 0.74f);
        }

        private static AudioClip GetClip(ElementType element, ArcaneSpellSfxCue cue)
        {
            var key = $"{element}_{cue}";
            if (ClipCache.TryGetValue(key, out var cachedClip) && cachedClip != null)
            {
                if (!IsGeneratedFallbackClip(cachedClip))
                    return cachedClip;

                var resourceClip = LoadResourceClip(element, cue);
                if (resourceClip != null)
                {
                    ClipCache[key] = resourceClip;
                    return resourceClip;
                }

                return cachedClip;
            }

            var clip = LoadResourceClip(element, cue) ?? CreateClip(element, cue);
            ClipCache[key] = clip;
            return clip;
        }

        private static AudioClip LoadResourceClip(ElementType element, ArcaneSpellSfxCue cue)
        {
            foreach (var resourceName in ResolveResourceClipNames(element, cue))
            {
                if (string.IsNullOrEmpty(resourceName))
                    continue;

                var clip = Resources.Load<AudioClip>($"{ResourceSfxPath}/{resourceName}");
                if (clip != null)
                {
                    LogResourceLoaded(element, cue, resourceName);
                    return clip;
                }
            }

            LogResourceMissing(element, cue);
            return null;
        }

        private static IEnumerable<string> ResolveResourceClipNames(ElementType element, ArcaneSpellSfxCue cue)
        {
            var elementName = element switch
            {
                ElementType.Fire => "Fire",
                ElementType.Ice => "Ice",
                ElementType.Thunder => "Thunder",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(elementName))
                yield break;

            var cueName = IsCastCue(cue) ? "Cast" : "Declare";
            yield return $"{elementName}_{cueName}";
            yield return $"{elementName}{cueName}";
            yield return $"{elementName}_{cue}";
            yield return $"{elementName}{cue}";
        }

        private static bool IsGeneratedFallbackClip(AudioClip clip)
        {
            return clip != null &&
                   clip.name.StartsWith("Arcane_", StringComparison.Ordinal);
        }

        private static void LogResourceLoaded(ElementType element, ArcaneSpellSfxCue cue, string resourceName)
        {
            var key = $"{element}_{cue}";
            if (!ResourceLoadLogs.Add(key))
                return;

            Debug.Log($"[ArcaneSpellSfx] Loaded Resources/{ResourceSfxPath}/{resourceName}");
        }

        private static void LogResourceMissing(ElementType element, ArcaneSpellSfxCue cue)
        {
            var key = $"{element}_{cue}";
            if (!ResourceMissingLogs.Add(key))
                return;

            Debug.LogWarning($"[ArcaneSpellSfx] Missing mp3 for {element}/{cue} under Resources/{ResourceSfxPath}. Using generated fallback.");
        }

        private static AudioClip CreateClip(ElementType element, ArcaneSpellSfxCue cue)
        {
            var length = ResolveLength(element, cue);
            var sampleCount = Mathf.CeilToInt(SampleRate * length);
            var samples = new float[sampleCount];
            var seed = ResolveSeed(element, cue);

            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / SampleRate;
                var normalized = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                var sample = element switch
                {
                    ElementType.Fire => SampleFire(cue, t, normalized, i, seed),
                    ElementType.Ice => SampleIce(cue, t, normalized, i, seed),
                    ElementType.Thunder => SampleThunder(cue, t, normalized, i, seed),
                    _ => 0f
                };

                samples[i] = SoftLimit(sample * ResolveGain(cue));
            }

            var clip = AudioClip.Create($"Arcane_{element}_{cue}", sampleCount, 1, SampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static float SampleFire(ArcaneSpellSfxCue cue, float t, float n, int sampleIndex, int seed)
        {
            var attack = cue == ArcaneSpellSfxCue.ElementArm ? 0.018f : 0.025f;
            var envelope = AttackDecay(n, attack, 0.82f);
            var noise = LayeredNoise(sampleIndex, seed);
            var ignition = Mathf.Sin(Tau * Mathf.Lerp(105f, 240f, n) * t) * 0.18f;
            var ember = Mathf.Sin(Tau * Mathf.Lerp(260f, 430f, n) * t) * 0.08f;
            var crackGate = Mathf.Abs(Noise(sampleIndex * 17, seed + 19)) > Mathf.Lerp(0.9f, 0.78f, n) ? 1f : 0f;
            var crackle = Noise(sampleIndex * 37, seed + 31) * crackGate * 0.34f * (1f - n);
            var whoosh = noise * Mathf.Lerp(0.34f, 0.12f, n);

            if (IsCastCue(cue))
            {
                whoosh *= 1.35f;
                ignition += Mathf.Sin(Tau * Mathf.Lerp(80f, 170f, n) * t) * 0.2f;
            }
            else if (cue == ArcaneSpellSfxCue.VoiceConfirm || cue == ArcaneSpellSfxCue.ComboReady)
            {
                ember += Mathf.Sin(Tau * Mathf.Lerp(520f, 760f, n) * t) * 0.11f * (1f - n);
            }

            return (whoosh + ignition + ember + crackle) * envelope;
        }

        private static float SampleIce(ArcaneSpellSfxCue cue, float t, float n, int sampleIndex, int seed)
        {
            var envelope = AttackDecay(n, 0.006f, cue == ArcaneSpellSfxCue.ElementArm ? 0.74f : 0.9f);
            var baseFrequency = cue switch
            {
                ArcaneSpellSfxCue.ElementArm => 880f,
                ArcaneSpellSfxCue.VoiceConfirm => 1040f,
                ArcaneSpellSfxCue.SpellCast => 1240f,
                _ => 960f
            };
            var glide = Mathf.Lerp(baseFrequency * 1.15f, baseFrequency * 0.82f, n);
            var bell =
                Mathf.Sin(Tau * glide * t) * 0.38f +
                Mathf.Sin(Tau * glide * 1.502f * t) * 0.23f +
                Mathf.Sin(Tau * glide * 2.01f * t) * 0.14f +
                Mathf.Sin(Tau * glide * 2.76f * t) * 0.08f;
            var shimmerGate = Mathf.Abs(Noise(sampleIndex * 23, seed + 7)) > 0.82f ? 1f : 0f;
            var shimmer = Noise(sampleIndex * 41, seed + 13) * shimmerGate * 0.12f * (1f - n);

            if (IsCastCue(cue))
                shimmer += Mathf.Sin(Tau * Mathf.Lerp(1800f, 2500f, n) * t) * 0.07f * (1f - n);

            return (bell + shimmer) * envelope;
        }

        private static float SampleThunder(ArcaneSpellSfxCue cue, float t, float n, int sampleIndex, int seed)
        {
            var envelope = AttackDecay(n, 0.004f, cue == ArcaneSpellSfxCue.ElementArm ? 0.62f : 0.82f);
            var snapEnvelope = Mathf.Clamp01(1f - n / 0.18f);
            var buzzFrequency = IsCastCue(cue)
                ? Mathf.Lerp(150f, 520f, n)
                : Mathf.Lerp(220f, 680f, n);
            var buzz = Mathf.Sign(Mathf.Sin(Tau * buzzFrequency * t)) * 0.18f;
            var electricTone = Mathf.Sin(Tau * buzzFrequency * 2.4f * t) * 0.16f;
            var rumble = Mathf.Sin(Tau * Mathf.Lerp(54f, 82f, n) * t) * 0.16f * (1f - n);
            var crackGate = Mathf.Abs(Noise(sampleIndex * 29, seed + 11)) > 0.52f ? 1f : 0f;
            var crack = Noise(sampleIndex * 61, seed + 5) * crackGate * 0.36f * snapEnvelope;

            if (cue == ArcaneSpellSfxCue.VoiceConfirm || cue == ArcaneSpellSfxCue.ComboReady)
                electricTone += Mathf.Sin(Tau * Mathf.Lerp(900f, 1250f, n) * t) * 0.1f * (1f - n);

            return (buzz + electricTone + rumble + crack) * envelope;
        }

        private static float ResolveLength(ElementType element, ArcaneSpellSfxCue cue)
        {
            return cue switch
            {
                ArcaneSpellSfxCue.ElementArm => element == ElementType.Ice ? 0.32f : 0.24f,
                ArcaneSpellSfxCue.VoiceConfirm => element == ElementType.Ice ? 0.62f : 0.52f,
                ArcaneSpellSfxCue.SpellCast => element == ElementType.Fire ? 0.46f : 0.38f,
                ArcaneSpellSfxCue.ComboReady => 0.28f,
                ArcaneSpellSfxCue.ComboCast => 0.56f,
                _ => 0.3f
            };
        }

        private static float ResolveGain(ArcaneSpellSfxCue cue)
        {
            return cue switch
            {
                ArcaneSpellSfxCue.ElementArm => 0.65f,
                ArcaneSpellSfxCue.VoiceConfirm => 0.95f,
                ArcaneSpellSfxCue.SpellCast => 1.05f,
                ArcaneSpellSfxCue.ComboReady => 0.78f,
                ArcaneSpellSfxCue.ComboCast => 1.12f,
                _ => 0.75f
            };
        }

        private static bool IsCastCue(ArcaneSpellSfxCue cue)
        {
            return cue == ArcaneSpellSfxCue.SpellCast ||
                   cue == ArcaneSpellSfxCue.ComboCast;
        }

        private static int ResolveSeed(ElementType element, ArcaneSpellSfxCue cue)
        {
            return ((int)element + 17) * 397 ^ ((int)cue + 31) * 911;
        }

        private static float AttackDecay(float normalized, float attack, float releaseStart)
        {
            var attackPart = Mathf.Clamp01(normalized / Mathf.Max(0.001f, attack));
            var releasePart = 1f - Mathf.SmoothStep(releaseStart, 1f, normalized);
            return attackPart * releasePart;
        }

        private static float LayeredNoise(int sampleIndex, int seed)
        {
            return Noise(sampleIndex, seed) * 0.55f +
                   Noise(sampleIndex / 2, seed + 3) * 0.3f +
                   Noise(sampleIndex / 5, seed + 5) * 0.15f;
        }

        private static float Noise(int sampleIndex, int seed)
        {
            unchecked
            {
                var x = (uint)(sampleIndex * 747796405) ^ (uint)(seed * 289133645);
                x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return x / (float)uint.MaxValue * 2f - 1f;
            }
        }

        private static float SoftLimit(float value)
        {
            return (float)Math.Tanh(value * 1.35f) * 0.92f;
        }

        private const float Tau = 6.28318530718f;
    }
}
