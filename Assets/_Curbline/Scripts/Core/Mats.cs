// Materials made in code for the placeholder art. They use whatever shader the active render
// pipeline uses by default (URP Lit in this project), so they work in builds without extra setup.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Curbline
{
    public static class Mats
    {
        static Shader lit;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Shader Lit
        {
            get
            {
                if (lit != null) return lit;
                var rp = GraphicsSettings.currentRenderPipeline;
                if (rp != null && rp.defaultMaterial != null) lit = rp.defaultMaterial.shader;
                if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard");
                return lit;
            }
        }

        /// <summary>A lit material. Shared by key, so call it freely.</summary>
        public static Material Get(Color color, float smoothness = 0.4f, float metallic = 0f, Color? emission = null)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + smoothness + "_" + metallic + "_" + (emission.HasValue ? ColorUtility.ToHtmlStringRGBA(emission.Value) : "");
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = New(color, smoothness, metallic, emission);
            cache[key] = m;
            return m;
        }

        /// <summary>A material of its own, for things that change color at runtime (lights, paint).</summary>
        public static Material New(Color color, float smoothness = 0.4f, float metallic = 0f, Color? emission = null)
        {
            var m = new Material(Lit);
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.HasValue) SetEmission(m, emission.Value);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void SetEmission(Material m, Color c)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
        }

        /// <summary>A lit material with a tiled texture.</summary>
        public static Material Textured(Texture2D tex, Vector2 tiling, float smoothness = 0.2f)
        {
            var m = New(Color.white, smoothness);
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiling); }
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", tiling); }
            return m;
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        /// <summary>A primitive with no collider, parented and placed.</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // now, so it never joins a parent rigidbody
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
