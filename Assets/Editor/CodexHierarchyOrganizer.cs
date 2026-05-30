using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexHierarchyOrganizer
{
    private sealed class GroupRule
    {
        public string GroupName;
        public Regex NamePattern;
        public Func<GameObject, bool> ComponentMatch;
    }

    private static readonly string[] ManagedGroups =
    {
        "=== Environment_Terrain ===",
        "=== Environment_Props ===",
        "=== Buildings_Structures ===",
        "=== Gameplay_Player_XR ===",
        "=== Gameplay_Targets_Combat ===",
        "=== VFX_Magic ===",
        "=== Lighting_Cameras ===",
        "=== Systems_Managers ===",
        "=== Audio ===",
        "=== UI ==="
    };

    private static readonly GroupRule[] Rules =
    {
        new GroupRule
        {
            GroupName = "=== Gameplay_Player_XR ===",
            NamePattern = new Regex(@"(xr|oculus|ovr|player|controller|hand|camera\s*rig|rig|tracking|locomotion|interactor)", RegexOptions.IgnoreCase),
            ComponentMatch = go => HasAnyComponent(go, "XR", "OVR", "Locomotion", "Interactor", "CharacterController")
        },
        new GroupRule
        {
            GroupName = "=== Environment_Terrain ===",
            NamePattern = new Regex(@"(terrain|landscape|ground|hill|mountain|water|river|lake|grass|tree|shrub|bush|forest|rock|cliff|sky|cloud|openworld|open\s*world|OW\d*)", RegexOptions.IgnoreCase),
            ComponentMatch = go => go.GetComponent<Terrain>() != null
        },
        new GroupRule
        {
            GroupName = "=== Buildings_Structures ===",
            NamePattern = new Regex(@"(house|hut|building|village|castle|tower|wall|gate|bridge|fence|structure|temple|ruin|shop|blacksmith|inn)", RegexOptions.IgnoreCase),
            ComponentMatch = null
        },
        new GroupRule
        {
            GroupName = "=== Gameplay_Targets_Combat ===",
            NamePattern = new Regex(@"(target|dummy|enemy|monster|golem|combat|hit|damage|training|practice)", RegexOptions.IgnoreCase),
            ComponentMatch = go => HasAnyComponent(go, "Health", "Damage", "Enemy", "Target")
        },
        new GroupRule
        {
            GroupName = "=== VFX_Magic ===",
            NamePattern = new Regex(@"(magic|spell|fireball|lightning|bolt|particle|vfx|aura|portal|effect|beam|explosion|trail)", RegexOptions.IgnoreCase),
            ComponentMatch = go => go.GetComponent<ParticleSystem>() != null
        },
        new GroupRule
        {
            GroupName = "=== Lighting_Cameras ===",
            NamePattern = new Regex(@"(light|sun|camera|reflection|volume|post\s*process|global\s*volume)", RegexOptions.IgnoreCase),
            ComponentMatch = go => go.GetComponent<Light>() != null || go.GetComponent<Camera>() != null
        },
        new GroupRule
        {
            GroupName = "=== Systems_Managers ===",
            NamePattern = new Regex(@"(manager|system|event|input|spawn|pool|loader|network|gamecontroller|director)", RegexOptions.IgnoreCase),
            ComponentMatch = go => HasAnyComponent(go, "Manager", "System", "Input")
        },
        new GroupRule
        {
            GroupName = "=== Audio ===",
            NamePattern = new Regex(@"(audio|sound|music|ambience|sfx)", RegexOptions.IgnoreCase),
            ComponentMatch = go => go.GetComponent<AudioSource>() != null || go.GetComponent<AudioListener>() != null
        },
        new GroupRule
        {
            GroupName = "=== UI ===",
            NamePattern = new Regex(@"(canvas|ui|hud|menu|text|panel|button)", RegexOptions.IgnoreCase),
            ComponentMatch = go => go.GetComponent<Canvas>() != null
        },
        new GroupRule
        {
            GroupName = "=== Environment_Props ===",
            NamePattern = new Regex(@"(prop|barrel|crate|box|bench|table|chair|sign|lamp|torch|cart|wagon|weapon|sword|shield|well|statue)", RegexOptions.IgnoreCase),
            ComponentMatch = null
        }
    };

    public static void Snapshot()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects()
            .OrderBy(go => go.transform.GetSiblingIndex())
            .ToArray();

        Debug.Log($"[CodexHierarchyOrganizer] Active scene: {scene.path}");
        Debug.Log($"[CodexHierarchyOrganizer] Root count: {roots.Length}");

        foreach (GameObject root in roots)
        {
            string group = IsManagedGroup(root.name) ? root.name : Classify(root);
            int childCount = root.transform.childCount;
            int totalCount = root.GetComponentsInChildren<Transform>(true).Length;
            Debug.Log($"[CodexHierarchyOrganizer] ROOT | group='{group}' | children={childCount} | total={totalCount} | name='{root.name}'");
        }
    }

    public static void Organize()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects()
            .OrderBy(go => go.transform.GetSiblingIndex())
            .ToArray();

        HashSet<string> requiredGroupNames = new HashSet<string>();
        foreach (GameObject root in roots)
        {
            if (root == null || IsManagedGroup(root.name))
            {
                continue;
            }

            string groupName = Classify(root);
            if (!string.IsNullOrEmpty(groupName))
            {
                requiredGroupNames.Add(groupName);
            }
        }

        Dictionary<string, Transform> groups = EnsureGroups(scene, requiredGroupNames);
        List<string> moved = new List<string>();
        List<string> skipped = new List<string>();

        foreach (GameObject root in roots)
        {
            if (root == null || IsManagedGroup(root.name))
            {
                continue;
            }

            string groupName = Classify(root);
            if (string.IsNullOrEmpty(groupName))
            {
                skipped.Add(root.name);
                continue;
            }

            Undo.SetTransformParent(root.transform, groups[groupName], "Organize Open World Hierarchy");
            moved.Add($"{root.name} -> {groupName}");
        }

        OrderGroups(scene);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[CodexHierarchyOrganizer] Moved {moved.Count} root objects.");
        foreach (string entry in moved)
        {
            Debug.Log($"[CodexHierarchyOrganizer] MOVED | {entry}");
        }

        Debug.Log($"[CodexHierarchyOrganizer] Left unchanged {skipped.Count} ambiguous root objects.");
        foreach (string entry in skipped.Take(80))
        {
            Debug.Log($"[CodexHierarchyOrganizer] SKIPPED | {entry}");
        }
    }

    private static Dictionary<string, Transform> EnsureGroups(Scene scene, HashSet<string> requiredGroupNames)
    {
        Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        GameObject[] roots = scene.GetRootGameObjects();

        foreach (string groupName in ManagedGroups)
        {
            if (!requiredGroupNames.Contains(groupName))
            {
                continue;
            }

            GameObject group = roots.FirstOrDefault(go => go.name == groupName);
            if (group == null)
            {
                group = new GameObject(groupName);
                Undo.RegisterCreatedObjectUndo(group, "Create Open World Hierarchy Group");
                EditorSceneManager.MoveGameObjectToScene(group, scene);
            }

            groups[groupName] = group.transform;
        }

        return groups;
    }

    private static void OrderGroups(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < ManagedGroups.Length; i++)
        {
            GameObject group = roots.FirstOrDefault(go => go.name == ManagedGroups[i]);
            if (group != null)
            {
                group.transform.SetSiblingIndex(i);
            }
        }
    }

    private static string Classify(GameObject go)
    {
        foreach (GroupRule rule in Rules)
        {
            if (rule.NamePattern.IsMatch(go.name) || (rule.ComponentMatch != null && rule.ComponentMatch(go)))
            {
                return rule.GroupName;
            }
        }

        return null;
    }

    private static bool HasAnyComponent(GameObject go, params string[] tokens)
    {
        Component[] components = go.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().Name;
            if (tokens.Any(token => typeName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsManagedGroup(string name)
    {
        return ManagedGroups.Contains(name);
    }
}
