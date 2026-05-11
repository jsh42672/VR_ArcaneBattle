using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor script to replace all GameObjects containing "PT_Pine_Tree" in their name
/// with the "fantasy_tree1" prefab, preserving transform data.
/// </summary>
public static class ReplaceTreesToFantasyTree
{
    [MenuItem("Tools/Replace Trees To Fantasy Tree")]
    public static void ReplaceTrees()
    {
        // 1. Find all GameObjects in the current scene that have "PT_Pine_Tree" in their name
        List<GameObject> treesToReplace = new List<GameObject>();
        
        // Use Scene roots to ensure we only target the active scene and avoid non-runtime objects
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            FindTreesRecursive(root, treesToReplace);
        }

        if (treesToReplace.Count == 0)
        {
            EditorUtility.DisplayDialog("Replace Trees", "No GameObjects with 'PT_Pine_Tree' found in the current scene.", "OK");
            return;
        }

        // 11. Add a confirmation dialog before deleting
        if (!EditorUtility.DisplayDialog("Replace Trees", 
            $"Found {treesToReplace.Count} trees to replace.\n\nThis will delete the original trees and instantiate 'fantasy_tree1' in their place. This action can be undone.", 
            "Replace All", "Cancel"))
        {
            return;
        }

        // 4. Load the prefab named "fantasy_tree1" from the Assets folder using AssetDatabase
        // Path provided: Assets/Art/Models/fantasy_tree1.prefab
        string prefabPath = "Assets/Art/Models/fantasy_tree1.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogError($"[ReplaceTrees] Prefab 'fantasy_tree1' not found at: {prefabPath}");
            EditorUtility.DisplayDialog("Error", $"Could not find prefab at {prefabPath}. Please verify the path.", "OK");
            return;
        }

        // 8. Register all changes with Undo so it can be undone with Ctrl+Z
        // Group operations for a single Undo step
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Replace Trees To Fantasy Tree");
        int undoGroup = Undo.GetCurrentGroup();

        int count = 1;
        // Iterate through collected trees to perform replacement
        foreach (GameObject oldTree in treesToReplace)
        {
            // 2. Store their positions, rotations, and scales before deleting them
            Vector3 position = oldTree.transform.position;
            Quaternion rotation = oldTree.transform.rotation;
            Vector3 scale = oldTree.transform.localScale;

            // 5. Instantiate the "fantasy_tree1" prefab at each of the stored positions and rotations
            // Using PrefabUtility.InstantiatePrefab to maintain the link in the Editor
            GameObject newTree = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (newTree == null) continue;

            newTree.transform.position = position;
            newTree.transform.rotation = rotation;
            
            // 6. Keep the same scale as the original trees
            newTree.transform.localScale = scale;

            // 7. Name each instantiated object "Fantasy_Tree_[number]"
            newTree.name = $"Fantasy_Tree_{count}";

            // 8. Register all changes with Undo
            Undo.RegisterCreatedObjectUndo(newTree, "Replace Trees");

            // 3. Delete all of those GameObjects
            Undo.DestroyObjectImmediate(oldTree);
            
            count++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Successfully replaced {count - 1} trees with 'fantasy_tree1'.");
        EditorUtility.DisplayDialog("Replace Trees", $"Successfully replaced {count - 1} trees.", "OK");
    }

    /// <summary>
    /// Recursively searches for GameObjects with "PT_Pine_Tree" in their name.
    /// </summary>
    private static void FindTreesRecursive(GameObject obj, List<GameObject> results)
    {
        if (obj.name.Contains("PT_Pine_Tree"))
        {
            results.Add(obj);
        }

        foreach (Transform child in obj.transform)
        {
            FindTreesRecursive(child.gameObject, results);
        }
    }
}

