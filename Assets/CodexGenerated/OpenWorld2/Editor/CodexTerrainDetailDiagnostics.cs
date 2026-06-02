using UnityEditor;
using UnityEngine;

public static class CodexTerrainDetailDiagnostics
{
    public static void Execute()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        TerrainData data = terrain.terrainData;
        Debug.Log("Terrain detailResolution=" + data.detailResolution
            + " detailPatchCount=" + data.detailPatchCount
            + " prototypes=" + data.detailPrototypes.Length
            + " drawInstanced=" + terrain.drawInstanced
            + " detailObjectDistance=" + terrain.detailObjectDistance
            + " detailObjectDensity=" + terrain.detailObjectDensity);

        DetailPrototype[] prototypes = data.detailPrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            DetailPrototype p = prototypes[i];
            string textureName = p.prototypeTexture != null ? AssetDatabase.GetAssetPath(p.prototypeTexture) : "null";
            string meshName = p.prototype != null ? AssetDatabase.GetAssetPath(p.prototype) : "null";
            int[,] layer = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i);
            int total = 0;
            for (int y = 0; y < data.detailHeight; y++)
            {
                for (int x = 0; x < data.detailWidth; x++)
                {
                    total += layer[y, x];
                }
            }

            Debug.Log("Detail[" + i + "] renderMode=" + p.renderMode
                + " usePrototypeMesh=" + p.usePrototypeMesh
                + " texture=" + textureName
                + " mesh=" + meshName
                + " minWidth=" + p.minWidth
                + " maxWidth=" + p.maxWidth
                + " minHeight=" + p.minHeight
                + " maxHeight=" + p.maxHeight
                + " density=" + p.density
                + " totalPainted=" + total);
        }
    }
}
