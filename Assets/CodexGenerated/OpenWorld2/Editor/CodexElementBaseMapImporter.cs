using UnityEditor;
using UnityEngine;

public static class CodexElementBaseMapImporter
{
    public static void Execute()
    {
        string[] paths =
        {
            "Assets/CodexGenerated/OpenWorld2/ElementBaseMaps/Fire_BaseMap_Alpha.png",
            "Assets/CodexGenerated/OpenWorld2/ElementBaseMaps/Ice_BaseMap_Alpha.png",
            "Assets/CodexGenerated/OpenWorld2/ElementBaseMaps/Electric_BaseMap_Alpha.png"
        };

        AssetDatabase.Refresh();
        for (int i = 0; i < paths.Length; i++)
        {
            TextureImporter importer = AssetImporter.GetAtPath(paths[i]) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("Texture importer not found: " + paths[i]);
                continue;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            Debug.Log("Configured alpha texture import: " + paths[i]);
        }
    }
}
