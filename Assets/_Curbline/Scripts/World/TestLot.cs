// The Phase 1 test lot: a walled asphalt pad with a row of parking bays, parked cars,
// a cone slalom, a parallel-parking gap and some lamp posts to bump into.
// Phase 2 replaces it with the training ground and Phase 3 with the city.
using System.Collections.Generic;
using UnityEngine;

namespace Curbline
{
    public class TestLot : MonoBehaviour
    {
        public static readonly Vector3 SpawnPos = new Vector3(0, 0, -38);
        public const float SpawnHeading = 0f;   // facing +z

        readonly List<(Transform t, Vector3 p, Quaternion r)> cones = new List<(Transform, Vector3, Quaternion)>();
        static Mesh coneMesh;

        public void Build()
        {
            var asphalt = Mats.Get(Mats.Hex("#3a3d42"), 0.25f);
            var grass = Mats.Get(Mats.Hex("#5d7a3a"), 0.1f);
            var paint = Mats.Get(Mats.Hex("#f1f1ec"), 0.3f);
            var curb = Mats.Get(Mats.Hex("#b9b7ae"), 0.2f);
            var bayFill = Mats.Get(new Color(0.24f, 0.81f, 0.56f), 0.2f, 0f, new Color(0.05f, 0.25f, 0.15f));
            var pole = Mats.Get(Mats.Hex("#5a6068"), 0.5f, 0.6f);

            // ground: the grass is underneath, the lot sits on top with a real collider for cones to rest on
            Mats.Prim(PrimitiveType.Cube, transform, new Vector3(0, -0.06f, 0), new Vector3(400, 0.1f, 400), grass, "Grass");
            var lot = Mats.Prim(PrimitiveType.Cube, transform, new Vector3(0, -0.05f, 0), new Vector3(120, 0.1f, 120), asphalt, "Lot");
            lot.AddComponent<BoxCollider>();

            // a low wall all round
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2; float s = i % 2 == 0 ? 1 : -1;
                var pos = alongX ? new Vector3(0, 0.4f, s * 60.4f) : new Vector3(s * 60.4f, 0.4f, 0);
                var size = alongX ? new Vector3(121.6f, 0.8f, 0.8f) : new Vector3(0.8f, 0.8f, 121.6f);
                Solid(PrimitiveType.Cube, pos, size, curb, "Wall");
            }

            // a row of eight bays along the north side, facing south; bay 5 is the one to aim for
            const float bayW = 2.9f, bayL = 5.6f, rowZ = 45f;
            float x0 = -bayW * 4;
            for (int i = 0; i <= 8; i++)
                Mats.Prim(PrimitiveType.Cube, transform, new Vector3(x0 + i * bayW, 0.006f, rowZ), new Vector3(0.12f, 0.01f, bayL), paint, "BayLine");
            Mats.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.006f, rowZ + bayL / 2), new Vector3(bayW * 8, 0.01f, 0.12f), paint, "BayBack");
            string[] parked = { "marlin", "rook", null, "pico", null, "okto", "brisa", "haulix" };
            for (int i = 0; i < 8; i++)
            {
                float cx = x0 + bayW * (i + 0.5f);
                if (parked[i] != null) ParkedCar(parked[i], new Vector3(cx, 0, rowZ + 0.1f), i % 3 == 0 ? 180f : 0f);
                else if (i == 4) Mats.Prim(PrimitiveType.Cube, transform, new Vector3(cx, 0.004f, rowZ), new Vector3(bayW - 0.3f, 0.005f, bayL - 0.3f), bayFill, "TargetBay");
            }

            // cone slalom down the west side, and a stop box at its end
            for (int i = 0; i < 7; i++) Cone(new Vector3(-30, 0, -35 + i * 8));
            foreach (var x in new[] { -32.2f, -27.8f }) for (int i = 0; i < 4; i++) Cone(new Vector3(x, 0, 25 + i * 1.6f));

            // parallel parking along the east wall: two cars with a gap between them, cones for the curb
            ParkedCar("marlin", new Vector3(52.5f, 0, 6.5f), 0);
            ParkedCar("kestrel", new Vector3(52.5f, 0, -6.5f), 0);
            for (int i = 0; i < 12; i++) Cone(new Vector3(54.8f, 0, -9 + i * 1.6f));
            Mats.Prim(PrimitiveType.Cube, transform, new Vector3(52.5f, 0.004f, 0), new Vector3(2.3f, 0.005f, 6.6f), bayFill, "TargetSlot");

            // lamp posts in the middle of the lot
            foreach (var p in new[] { new Vector3(-12, 0, 0), new Vector3(12, 0, 0), new Vector3(-12, 0, 24), new Vector3(12, 0, 24) })
            {
                var post = Solid(PrimitiveType.Cylinder, p + Vector3.up * 3, new Vector3(0.22f, 3, 0.22f), pole, "LampPost");
                var lamp = new GameObject("Lamp").AddComponent<Light>();
                lamp.transform.SetParent(post.transform, false);
                lamp.transform.position = p + Vector3.up * 5.8f;
                lamp.type = LightType.Point; lamp.range = 16; lamp.intensity = 2.2f; lamp.color = new Color(1f, 0.85f, 0.65f);
                lamp.shadows = LightShadows.None;
            }

            // painted arrows so you can tell which way is forward from the start
            for (int i = 0; i < 3; i++)
                Mats.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.006f, -30 + i * 6), new Vector3(0.25f, 0.01f, 3f), paint, "CenterLine");
        }

        public void ResetCones()
        {
            foreach (var (t, p, r) in cones)
            {
                if (t == null) continue;
                var rb = t.GetComponent<Rigidbody>();
                rb.position = p; rb.rotation = r;
                t.SetPositionAndRotation(p, r);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            }
        }

        GameObject Solid(PrimitiveType type, Vector3 pos, Vector3 scale, Material m, string name)
        {
            var go = GameObject.CreatePrimitive(type);   // keeps its collider: a fixed obstacle
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        void ParkedCar(string id, Vector3 pos, float yawDeg)
        {
            var spec = CarCatalog.Get(id); var b = spec.Body;
            var go = new GameObject("Parked " + spec.Name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yawDeg, 0));
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(b.W, b.H - b.B, b.L);
            col.center = new Vector3(0, b.B + (b.H - b.B) / 2, 0);
            go.AddComponent<PlaceholderCar>().Build(spec);
        }

        void Cone(Vector3 pos)
        {
            var go = new GameObject("Cone");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var orange = Mats.Get(Mats.Hex("#ff6a13"), 0.5f);
            var band = Mats.Get(Mats.Hex("#f4f4f0"), 0.4f);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = orange;
            Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.02f, 0), new Vector3(0.42f, 0.04f, 0.42f), orange, "Base");
            Mats.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.42f, 0), new Vector3(0.27f, 0.05f, 0.27f), band, "Band");
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.4f, 0.72f, 0.4f); col.center = new Vector3(0, 0.36f, 0);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f; rb.linearDamping = 0.6f; rb.angularDamping = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            cones.Add((go.transform, go.transform.position, go.transform.rotation));
        }

        static Mesh ConeMesh()
        {
            if (coneMesh != null) return coneMesh;
            const int seg = 16; const float r = 0.2f, h = 0.72f;
            var v = new List<Vector3>(); var t = new List<int>();
            v.Add(new Vector3(0, h, 0));
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2 / seg; v.Add(new Vector3(Mathf.Sin(a) * r, 0.04f, Mathf.Cos(a) * r)); }
            for (int i = 1; i <= seg; i++) { t.Add(0); t.Add(i); t.Add(i + 1); }
            coneMesh = new Mesh { name = "Cone" };
            coneMesh.SetVertices(v); coneMesh.SetTriangles(t, 0);
            coneMesh.RecalculateNormals(); coneMesh.RecalculateBounds();
            return coneMesh;
        }
    }
}
