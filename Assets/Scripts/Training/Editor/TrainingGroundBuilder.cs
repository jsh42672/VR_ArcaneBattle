using UnityEngine;
using UnityEditor;

namespace ArcaneVR.Training.Editor
{
    public class TrainingGroundBuilder
    {
        private const string RootName = "TrainingGround_Root";

        private static Material CreateMaterial(string name, string hexColor)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hexColor, out color);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.name = name;
            mat.color = color;
            return mat;
        }

        [MenuItem("Tools/Build Training Ground")]
        public static void Build()
        {
            // 1. Cleanup
            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot);
            }

            // 2. Create Root
            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Training Ground");

            // 3. Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TutorialFloor";
            floor.transform.SetParent(root.transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3, 1, 3);
            floor.GetComponent<Renderer>().material = CreateMaterial("Mat_Floor", "#13112a");

            // 4. Walls
            Material wallMat = CreateMaterial("Mat_Wall", "#252240");
            CreateWall("Wall_N", new Vector3(0, 2, 15), new Vector3(30, 4, 0.5f), wallMat, root.transform);
            CreateWall("Wall_S_L", new Vector3(-8, 2, -15), new Vector3(14, 4, 0.5f), wallMat, root.transform);
            CreateWall("Wall_S_R", new Vector3(8, 2, -15), new Vector3(14, 4, 0.5f), wallMat, root.transform);
            CreateWall("Wall_E", new Vector3(15, 2, 0), new Vector3(0.5f, 4, 30), wallMat, root.transform);
            CreateWall("Wall_W", new Vector3(-15, 2, 0), new Vector3(0.5f, 4, 30), wallMat, root.transform);

            // 5. Pillars
            CreatePillar("Pillar_NW", new Vector3(-10, 2, 10), wallMat, root.transform);
            CreatePillar("Pillar_NE", new Vector3(10, 2, 10), wallMat, root.transform);
            CreatePillar("Pillar_SW", new Vector3(-10, 2, -10), wallMat, root.transform);
            CreatePillar("Pillar_SE", new Vector3(10, 2, -10), wallMat, root.transform);

            // 6. Zone 1 - Shooting
            GameObject zone1 = new GameObject("Zone1_Target");
            zone1.transform.SetParent(root.transform);
            zone1.transform.position = new Vector3(-8, 0, 6);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "MovingTarget";
            target.transform.SetParent(zone1.transform);
            target.transform.position = new Vector3(-8, 1, 6);
            target.transform.localScale = new Vector3(1, 0.1f, 1);
            target.GetComponent<Renderer>().material = CreateMaterial("Mat_Target", "#A78BFA");

            CreateLight("Zone1_Light", new Vector3(-8, 4, 6), "#7C3AED", 3, 15, zone1.transform);

            // 7. Zone 2 - Defense
            GameObject zone2 = new GameObject("Zone2_Defense");
            zone2.transform.SetParent(root.transform);
            zone2.transform.position = new Vector3(8, 0, 6);

            GameObject spawner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawner.name = "ProjectileSpawner";
            spawner.transform.SetParent(zone2.transform);
            spawner.transform.position = new Vector3(8, 2, 12);
            spawner.transform.localScale = Vector3.one;
            spawner.GetComponent<Renderer>().material = CreateMaterial("Mat_Spawner", "#1D4ED8");

            GameObject defZone = new GameObject("DefenseZone");
            defZone.transform.SetParent(zone2.transform);
            defZone.transform.position = new Vector3(8, 1, 4);
            var bc = defZone.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(4, 3, 2);

            CreateLight("Zone2_Light", new Vector3(8, 4, 6), "#2563EB", 3, 15, zone2.transform);

            // 8. Zone 3 - Movement
            GameObject zone3 = new GameObject("Zone3_Movement");
            zone3.transform.SetParent(root.transform);
            zone3.transform.position = new Vector3(0, 0, -5);

            CreateWaypoint("MoveStart", new Vector3(0, 0.1f, -3), "#34D399", zone3.transform);
            CreateWaypoint("MoveCheckPoint", new Vector3(0, 0.1f, -7), "#6EE7B7", zone3.transform);
            CreateWaypoint("MoveEnd", new Vector3(0, 0.1f, -11), "#059669", zone3.transform);

            CreateLight("Zone3_Light", new Vector3(0, 4, -7), "#059669", 3, 15, zone3.transform);

            // 9. Other
            GameObject spawnPoint = new GameObject("PlayerSpawnPoint");
            spawnPoint.transform.SetParent(root.transform);
            spawnPoint.transform.position = new Vector3(0, 0, 2);

            GameObject dirLightGo = new GameObject("Directional Light");
            dirLightGo.transform.SetParent(root.transform);
            dirLightGo.transform.rotation = Quaternion.Euler(50, 30, 0);
            Light l = dirLightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            Color dirColor;
            ColorUtility.TryParseHtmlString("#C4B5FD", out dirColor);
            l.color = dirColor;
            l.intensity = 0.5f;

            Debug.Log("Training Ground built under root successfully.");
        }

        private static void CreateWall(string n, Vector3 p, Vector3 s, Material m, Transform parent)
        {
            GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = n;
            w.transform.SetParent(parent);
            w.transform.position = p;
            w.transform.localScale = s;
            w.GetComponent<Renderer>().material = m;
        }

        private static void CreatePillar(string n, Vector3 p, Material m, Transform parent)
        {
            GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = n;
            o.transform.SetParent(parent);
            o.transform.position = p;
            o.transform.localScale = new Vector3(1, 4, 1);
            o.GetComponent<Renderer>().material = m;
        }

        private static void CreateLight(string n, Vector3 p, string hex, float intensity, float range, Transform parent)
        {
            GameObject lgo = new GameObject(n);
            lgo.transform.SetParent(parent);
            lgo.transform.position = p;
            Light l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            l.color = c;
            l.intensity = intensity;
            l.range = range;
        }

        private static void CreateWaypoint(string n, Vector3 p, string hex, Transform parent)
        {
            GameObject wp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wp.name = n;
            wp.transform.SetParent(parent);
            wp.transform.position = p;
            wp.transform.localScale = new Vector3(1, 0.05f, 1);
            wp.GetComponent<Renderer>().material = CreateMaterial("Mat_" + n, hex);
            wp.GetComponent<Collider>().isTrigger = true;
            Object.DestroyImmediate(wp.GetComponent<Collider>());
            var bc = wp.AddComponent<BoxCollider>();
            bc.isTrigger = true;
        }
    }
}
