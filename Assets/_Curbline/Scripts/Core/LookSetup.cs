// The "look" of a scene: sky, sun, fog, reflections, post-processing and camera quality.
// Everything here is free (no asset packs) and carries over once real models are in.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Curbline
{
    public static class LookSetup
    {
        static readonly Color Horizon = new Color(0.74f, 0.82f, 0.9f);

        public static void Apply(Camera cam, Light sun, Vector3 center, float size)
        {
            // sun: warm, fairly low, so shapes get long soft shadows
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(36, -40, 0);
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            // procedural sky, used for ambient light and reflections too
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_SunSizeConvergence", 6f);
                sky.SetFloat("_AtmosphereThickness", 0.85f);
                sky.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.75f));
                sky.SetColor("_GroundColor", new Color(0.36f, 0.36f, 0.34f));
                sky.SetFloat("_Exposure", 1.15f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            DynamicGI.UpdateEnvironment();

            // distance haze, so the edge of the world fades instead of ending
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 520f;

            // shadows reach far enough to cover the lot from the chase camera
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = 90f;
                urp.shadowCascadeCount = Mathf.Max(urp.shadowCascadeCount, 2);
            }

            // one reflection probe over the play area: shiny paint and glass pick up the sky and the lot
            var probeGo = new GameObject("Reflection Probe");
            probeGo.transform.position = center + Vector3.up * 3f;
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = new Vector3(size, 40f, size);
            probe.resolution = 256;
            probe.hdr = true;
            probe.RenderProbe();

            // camera: post-processing, anti-aliasing, a natural lens
            cam.allowHDR = true;
            cam.backgroundColor = Horizon;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            // color grading: filmic tone curve, gentle bloom on lights and sun glints, a light vignette
            var volGo = new GameObject("Post Processing");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(1.1f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.65f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f); ca.contrast.Override(14f); ca.saturation.Override(10f);
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.2f); vg.smoothness.Override(0.45f);
        }

        // ---------- generated surface textures (stand-ins until real materials) ----------

        /// <summary>Asphalt: dark grey with fine speckle and soft patches.</summary>
        public static Texture2D Asphalt() => Noise(512, 11, (n, fine) =>
        {
            float v = 0.2f + n * 0.07f + fine * 0.09f;
            if (fine > 0.93f) v += 0.12f;   // light aggregate stones
            return new Color(v, v * 1.01f, v * 1.04f);
        });

        /// <summary>Grass: mixed greens with darker clumps.</summary>
        public static Texture2D Grass() => Noise(256, 23, (n, fine) =>
            Color.Lerp(new Color(0.24f, 0.36f, 0.14f), new Color(0.42f, 0.52f, 0.22f), n * 0.7f + fine * 0.3f));

        /// <summary>Concrete for curbs and walls.</summary>
        public static Texture2D Concrete() => Noise(256, 5, (n, fine) =>
        {
            float v = 0.62f + n * 0.08f + fine * 0.06f;
            return new Color(v, v * 0.99f, v * 0.96f);
        });

        static Texture2D Noise(int N, int seed, System.Func<float, float, Color> shade)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[N * N];
            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 100f, oy = (float)rnd.NextDouble() * 100f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // tileable: sample noise on a torus-like wrap by blending the four corners
                    float u = (float)x / N, v = (float)y / N;
                    float n = Tile(u, v, 6f, ox, oy);
                    float fine = (float)rnd.NextDouble();
                    px[y * N + x] = shade(n, fine);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static float Tile(float u, float v, float f, float ox, float oy)
        {
            float a = Mathf.PerlinNoise(ox + u * f, oy + v * f);
            float b = Mathf.PerlinNoise(ox + (u - 1) * f, oy + v * f);
            float c = Mathf.PerlinNoise(ox + u * f, oy + (v - 1) * f);
            float d = Mathf.PerlinNoise(ox + (u - 1) * f, oy + (v - 1) * f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
