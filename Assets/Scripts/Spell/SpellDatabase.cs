using System;
using System.Collections.Generic;
using ArcaneVR.Input;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// ScriptableObject that stores spell data per SpellId and prototype hand pose.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellDatabase", menuName = "ArcaneVR/SpellDatabase")]
    public class SpellDatabase : ScriptableObject
    {
        [Serializable]
        public class SpellData
        {
            public SpellId spellId;
            public ElementType element;
            public GameObject prefab;
            public float damage = 10f;
            public int manaCost = 1;
            public float projectileSpeed = 15f;
            public StatusEffect statusEffect;
            public float statusDuration = 3f;
            public float statusMagnitude = 1f;
            public float statusTickInterval = 0.5f;
        }

        [Serializable]
        public class PoseSpellData
        {
            public PoseType pose;
            public ElementType element;
            public StatusEffect statusEffect;
            public float damage = 10f;
            public float statusDuration = 3f;
            public float statusMagnitude = 1f;
            public float statusTickInterval = 0.5f;
            public float projectileSpeed = 10f;
            public GameObject prefab;
        }

        public List<SpellData> spells = new List<SpellData>();
        public List<PoseSpellData> poseSpells = new List<PoseSpellData>();

        public IReadOnlyList<SpellData> Spells => spells;
        public IReadOnlyList<PoseSpellData> PoseSpells => poseSpells;

        public SpellData Get(SpellId id)
        {
            var data = spells != null ? spells.Find(spell => spell.spellId == id) : null;
            if (data != null)
                ApplyMissingRuntimeDefaults(data);

            return data;
        }

        public bool TryGet(SpellId id, out SpellData data)
        {
            data = Get(id);
            return data != null;
        }

        public PoseSpellData Get(PoseType pose)
        {
            var data = poseSpells != null ? poseSpells.Find(spell => spell.pose == pose) : null;
            if (data != null)
                ApplyMissingRuntimeDefaults(data);

            return data;
        }

        public bool TryGet(PoseType pose, out PoseSpellData data)
        {
            data = Get(pose);
            return data != null;
        }

        public void EnsureDefaultSpells(bool overwriteExistingValues = false)
        {
            if (spells == null)
                spells = new List<SpellData>();

            EnsureDefaultSpell(CreateDefaultSpell(
                SpellId.Single_Pointer,
                ElementType.Fire,
                10f,
                1,
                18f,
                StatusEffect.Burn,
                4f),
                overwriteExistingValues);

            EnsureDefaultSpell(CreateDefaultSpell(
                SpellId.Single_Wave,
                ElementType.Ice,
                8f,
                1,
                12f,
                StatusEffect.Slow,
                3f),
                overwriteExistingValues);

            EnsureDefaultSpell(CreateDefaultSpell(
                SpellId.Single_Strike,
                ElementType.Thunder,
                14f,
                1,
                20f,
                StatusEffect.Stagger,
                1f),
                overwriteExistingValues);

            EnsureDefaultSpell(CreateDefaultSpell(
                SpellId.Combo_FireIce,
                ElementType.Fire,
                24f,
                2,
                16f,
                StatusEffect.Stagger,
                3f,
                1.2f,
                0f),
                overwriteExistingValues);

            EnsureDefaultSpell(CreateDefaultSpell(
                SpellId.Combo_IceThunder,
                ElementType.Ice,
                18f,
                2,
                17f,
                StatusEffect.Slow,
                4f,
                1f,
                0f),
                overwriteExistingValues);

            EnsureDefaultSpell(CreateDefaultSpell(
                SpellId.Combo_ThunderFire,
                ElementType.Thunder,
                34f,
                2,
                19f,
                StatusEffect.Burn,
                5f,
                3.5f,
                0.75f),
                overwriteExistingValues);

            RemoveDuplicateEntries();
            EnsureDefaultPoseSpells(overwriteExistingValues);
        }

        private void Reset()
        {
            spells = new List<SpellData>();
            poseSpells = new List<PoseSpellData>();
            EnsureDefaultSpells(true);
        }

        private void OnValidate()
        {
            EnsureDefaultSpells(false);
        }

        private void EnsureDefaultSpell(SpellData defaultData, bool overwriteExistingValues)
        {
            var existing = Get(defaultData.spellId);
            if (existing == null)
            {
                spells.Add(defaultData);
                return;
            }

            if (!overwriteExistingValues)
                return;

            var prefab = existing.prefab;
            CopyValues(defaultData, existing);
            existing.prefab = prefab;
        }

        private void RemoveDuplicateEntries()
        {
            var seen = new HashSet<SpellId>();
            for (var i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                if (spell == null || spell.spellId == SpellId.None)
                    continue;

                if (!seen.Add(spell.spellId))
                {
                    spells.RemoveAt(i);
                    i--;
                }
            }
        }

        private void EnsureDefaultPoseSpells(bool overwriteExistingValues)
        {
            if (poseSpells == null)
                poseSpells = new List<PoseSpellData>();

            EnsureDefaultPoseSpell(CreateDefaultPoseSpell(
                PoseType.OpenPalm,
                ElementType.Fire,
                StatusEffect.Burn,
                10f,
                3f,
                10f),
                overwriteExistingValues);

            EnsureDefaultPoseSpell(CreateDefaultPoseSpell(
                PoseType.Fist,
                ElementType.Ice,
                StatusEffect.Slow,
                8f,
                3f,
                10f),
                overwriteExistingValues);

            EnsureDefaultPoseSpell(CreateDefaultPoseSpell(
                PoseType.ThumbsUp,
                ElementType.Thunder,
                StatusEffect.Stagger,
                12f,
                1f,
                12f),
                overwriteExistingValues);

            RemoveDuplicatePoseEntries();
        }

        private void EnsureDefaultPoseSpell(PoseSpellData defaultData, bool overwriteExistingValues)
        {
            var existing = Get(defaultData.pose);
            if (existing == null)
            {
                poseSpells.Add(defaultData);
                return;
            }

            if (!overwriteExistingValues)
                return;

            var prefab = existing.prefab;
            CopyPoseValues(defaultData, existing);
            existing.prefab = prefab;
        }

        private void RemoveDuplicatePoseEntries()
        {
            var seen = new HashSet<PoseType>();
            for (var i = 0; i < poseSpells.Count; i++)
            {
                var spell = poseSpells[i];
                if (spell == null || spell.pose == PoseType.None)
                    continue;

                if (!seen.Add(spell.pose))
                {
                    poseSpells.RemoveAt(i);
                    i--;
                }
            }
        }

        private static SpellData CreateDefaultSpell(
            SpellId spellId,
            ElementType element,
            float damage,
            int manaCost,
            float projectileSpeed,
            StatusEffect statusEffect,
            float statusDuration,
            float statusMagnitude = -1f,
            float statusTickInterval = -1f)
        {
            return new SpellData
            {
                spellId = spellId,
                element = element,
                damage = damage,
                manaCost = manaCost,
                projectileSpeed = projectileSpeed,
                statusEffect = statusEffect,
                statusDuration = statusDuration,
                statusMagnitude = statusMagnitude >= 0f ? statusMagnitude : ResolveDefaultMagnitude(statusEffect),
                statusTickInterval = statusTickInterval >= 0f ? statusTickInterval : ResolveDefaultTickInterval(statusEffect)
            };
        }

        private static void CopyValues(SpellData source, SpellData destination)
        {
            destination.spellId = source.spellId;
            destination.element = source.element;
            destination.damage = source.damage;
            destination.manaCost = source.manaCost;
            destination.projectileSpeed = source.projectileSpeed;
            destination.statusEffect = source.statusEffect;
            destination.statusDuration = source.statusDuration;
            destination.statusMagnitude = source.statusMagnitude;
            destination.statusTickInterval = source.statusTickInterval;
        }

        private static PoseSpellData CreateDefaultPoseSpell(
            PoseType pose,
            ElementType element,
            StatusEffect statusEffect,
            float damage,
            float statusDuration,
            float projectileSpeed,
            float statusMagnitude = -1f,
            float statusTickInterval = -1f)
        {
            return new PoseSpellData
            {
                pose = pose,
                element = element,
                statusEffect = statusEffect,
                damage = damage,
                statusDuration = statusDuration,
                statusMagnitude = statusMagnitude >= 0f ? statusMagnitude : ResolveDefaultMagnitude(statusEffect),
                statusTickInterval = statusTickInterval >= 0f ? statusTickInterval : ResolveDefaultTickInterval(statusEffect),
                projectileSpeed = projectileSpeed
            };
        }

        private static void CopyPoseValues(PoseSpellData source, PoseSpellData destination)
        {
            destination.pose = source.pose;
            destination.element = source.element;
            destination.statusEffect = source.statusEffect;
            destination.damage = source.damage;
            destination.statusDuration = source.statusDuration;
            destination.statusMagnitude = source.statusMagnitude;
            destination.statusTickInterval = source.statusTickInterval;
            destination.projectileSpeed = source.projectileSpeed;
        }

        private static void ApplyMissingRuntimeDefaults(SpellData data)
        {
            if (data.statusMagnitude <= 0f)
                data.statusMagnitude = ResolveDefaultMagnitude(data.statusEffect);

            if (data.statusTickInterval < 0f)
                data.statusTickInterval = 0f;

            if (data.statusTickInterval <= 0f && data.statusEffect == StatusEffect.Burn)
                data.statusTickInterval = ResolveDefaultTickInterval(data.statusEffect);
        }

        private static void ApplyMissingRuntimeDefaults(PoseSpellData data)
        {
            if (data.statusMagnitude <= 0f)
                data.statusMagnitude = ResolveDefaultMagnitude(data.statusEffect);

            if (data.statusTickInterval < 0f)
                data.statusTickInterval = 0f;

            if (data.statusTickInterval <= 0f && data.statusEffect == StatusEffect.Burn)
                data.statusTickInterval = ResolveDefaultTickInterval(data.statusEffect);
        }

        private static float ResolveDefaultMagnitude(StatusEffect effect)
        {
            return effect switch
            {
                StatusEffect.Burn => 2f,
                StatusEffect.Slow => 0.45f,
                StatusEffect.Stagger => 1f,
                _ => 0f
            };
        }

        private static float ResolveDefaultTickInterval(StatusEffect effect)
        {
            return effect == StatusEffect.Burn ? 1f : 0f;
        }
    }
}
