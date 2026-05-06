// Assets/Editor/MeshyAutoFix.cs 로 저장

using UnityEngine;
using UnityEditor;

public class MeshyAutoFix : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        // Meshy 파일에만 적용
        if (assetPath.Contains("Meshy"))
        {
            ModelImporter importer = assetImporter as ModelImporter;
            importer.bakeAxisConversion = true;
        }
    }
}