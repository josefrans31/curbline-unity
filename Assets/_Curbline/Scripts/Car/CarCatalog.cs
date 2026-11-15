// The 24 Curbline cars, ported from CARS and PROFILES in the web game's index.html.
// All original, unbranded designs with invented names.
using System.Collections.Generic;
using UnityEngine;

namespace Curbline
{
    /// <summary>Body proportions shared by a class of car (meters).</summary>
    public struct BodyProfile
    {
        public float L, W, H;   // length, width, height
        public float B;         // ground clearance to the bottom of the body
        public float WheelR;    // wheel radius
        public float RearAxle;  // rear axle distance from the rear bumper
        public float FrontAxle; // front axle distance from the rear bumper

        public BodyProfile(float l, float w, float h, float b, float wr, float rw, float fw)
        { L = l; W = w; H = h; B = b; WheelR = wr; RearAxle = rw; FrontAxle = fw; }

        public float Wheelbase => FrontAxle - RearAxle;
    }

    public class CarSpec
    {
        public string Id, Name, Class;
        public int Price;
        public Color Color;
        public float Accel;      // m/s² at full throttle from rest
        public float TopSpeed;   // m/s
        public float SteerLock;  // radians at full lock
        public BodyProfile Body;
        public int Cylinders;    // 0 = electric
        public string ClassLabel => CarCatalog.ClassLabels.TryGetValue(Class, out var s) ? s : Class;
    }

    public static class CarCatalog
    {
        static readonly Dictionary<string, BodyProfile> Profiles = new Dictionary<string, BodyProfile>
        {
            //                       L     W     H     b     wr    rw    fw
            { "hatch",   new BodyProfile(4.10f, 1.80f, 1.45f, 0.20f, 0.33f, 0.66f, 3.32f) },
            { "rally",   new BodyProfile(4.15f, 1.84f, 1.50f, 0.30f, 0.35f, 0.68f, 3.35f) },
            { "sedan",   new BodyProfile(4.75f, 1.85f, 1.45f, 0.19f, 0.34f, 1.00f, 3.85f) },
            { "ev",      new BodyProfile(4.80f, 1.90f, 1.42f, 0.16f, 0.35f, 0.95f, 3.90f) },
            { "wagon",   new BodyProfile(4.80f, 1.85f, 1.50f, 0.20f, 0.34f, 0.95f, 3.85f) },
            { "suv",     new BodyProfile(4.75f, 1.95f, 1.75f, 0.32f, 0.40f, 0.85f, 3.75f) },
            { "pickup",  new BodyProfile(5.40f, 2.00f, 1.85f, 0.36f, 0.42f, 1.00f, 4.40f) },
            { "van",     new BodyProfile(4.90f, 1.95f, 1.85f, 0.22f, 0.36f, 0.80f, 3.95f) },
            { "coupe",   new BodyProfile(4.40f, 1.85f, 1.30f, 0.15f, 0.34f, 0.80f, 3.50f) },
            { "muscle",  new BodyProfile(4.85f, 1.95f, 1.35f, 0.16f, 0.36f, 0.95f, 3.85f) },
            { "classic", new BodyProfile(4.60f, 1.80f, 1.38f, 0.18f, 0.34f, 0.90f, 3.75f) },
            { "roadster",new BodyProfile(4.00f, 1.75f, 1.20f, 0.14f, 0.32f, 0.70f, 3.20f) },
            { "gt",      new BodyProfile(4.90f, 1.95f, 1.30f, 0.14f, 0.36f, 0.95f, 3.95f) },
            { "super",   new BodyProfile(4.50f, 2.00f, 1.15f, 0.12f, 0.35f, 0.85f, 3.60f) },
            { "hyper",   new BodyProfile(4.70f, 2.05f, 1.10f, 0.10f, 0.36f, 0.90f, 3.80f) },
            { "wedge",   new BodyProfile(4.30f, 1.90f, 1.12f, 0.13f, 0.34f, 0.85f, 3.45f) },
        };

        public static readonly Dictionary<string, string> ClassLabels = new Dictionary<string, string>
        {
            { "hatch", "Hot hatch" }, { "rally", "Rally hatch" }, { "sedan", "Sedan" }, { "ev", "Electric sedan" },
            { "wagon", "Wagon" }, { "suv", "SUV" }, { "pickup", "Pickup" }, { "van", "Minivan" },
            { "coupe", "Sport coupe" }, { "muscle", "Muscle coupe" }, { "classic", "Classic coupe" },
            { "roadster", "Roadster" }, { "gt", "Grand tourer" }, { "super", "Mid-engine supercar" },
            { "hyper", "Hypercar" }, { "wedge", "Wedge supercar" },
        };

        static readonly Dictionary<string, int> CylindersByClass = new Dictionary<string, int>
        {
            { "hatch", 4 }, { "rally", 4 }, { "sedan", 4 }, { "wagon", 4 }, { "van", 4 }, { "roadster", 4 },
            { "suv", 6 }, { "coupe", 6 }, { "pickup", 8 }, { "classic", 8 }, { "muscle", 8 }, { "gt", 8 },
            { "super", 10 }, { "wedge", 12 }, { "hyper", 12 }, { "ev", 0 },
        };

        static List<CarSpec> all;
        public static IReadOnlyList<CarSpec> All => all ??= Build();

        public static CarSpec Get(string id)
        {
            foreach (var c in All) if (c.Id == id) return c;
            return All[0];
        }

        // Overrides use the same keys as the web game's "o" field. Only the ones that matter for driving are kept.
        static CarSpec Car(string id, string name, string cls, int price, string hex, float acc, float top, float lockRad,
            float? L = null, float? W = null, float? H = null, float? fw = null, float? b = null, float? wr = null)
        {
            var p = Profiles[cls];
            if (L.HasValue) p.L = L.Value;
            if (W.HasValue) p.W = W.Value;
            if (H.HasValue) p.H = H.Value;
            if (fw.HasValue) p.FrontAxle = fw.Value;
            if (b.HasValue) p.B = b.Value;
            if (wr.HasValue) p.WheelR = wr.Value;
            ColorUtility.TryParseHtmlString(hex, out var col);
            return new CarSpec
            {
                Id = id, Name = name, Class = cls, Price = price, Color = col,
                Accel = acc, TopSpeed = top, SteerLock = lockRad, Body = p, Cylinders = CylindersByClass[cls],
            };
        }

        static List<CarSpec> Build() => new List<CarSpec>
        {
            Car("kestrel",  "Kestrel GT",     "hatch",    0,    "#c8202f", 4.2f, 52, 0.62f),
            Car("pico",     "Pico City",      "hatch",    400,  "#2a9fb8", 3.2f, 42, 0.66f, L: 3.70f, W: 1.70f, H: 1.52f, fw: 2.98f),
            Car("okto",     "Okto Van",       "van",      800,  "#e8e6e1", 2.8f, 44, 0.58f),
            Car("marlin",   "Marlin S",       "sedan",    600,  "#aab3bb", 3.4f, 50, 0.58f),
            Car("brisa",    "Brisa Touring",  "wagon",    900,  "#2f5d46", 3.4f, 50, 0.58f),
            Car("rook",     "Rook",           "suv",      1100, "#d8692b", 3.2f, 46, 0.60f, L: 4.40f, H: 1.68f, fw: 3.50f),
            Car("bramble",  "Bramble Trail",  "wagon",    1500, "#b59f7a", 3.1f, 45, 0.58f, b: 0.34f, wr: 0.38f, H: 1.62f),
            Car("torvo",    "Torvo 4x4",      "suv",      1600, "#5a6142", 3.0f, 44, 0.56f),
            Car("ferro",    "Ferro Rally",    "rally",    1800, "#f1f1ee", 4.6f, 54, 0.64f),
            Car("haulix",   "Haulix 2500",    "pickup",   2000, "#22489a", 3.0f, 46, 0.54f),
            Car("strada",   "Strada GT",      "sedan",    2200, "#1c2c4f", 4.8f, 60, 0.58f, b: 0.15f, H: 1.40f),
            Car("ember",    "Ember Coupe",    "coupe",    2400, "#f2c230", 4.9f, 60, 0.60f),
            Car("lumen",    "Lumen EV",       "ev",       2600, "#f4f4f0", 5.6f, 58, 0.60f),
            Car("saber",    "Saber Roadster", "roadster", 2600, "#1f4a35", 4.8f, 56, 0.64f),
            Car("comet",    "Comet 64",       "classic",  2800, "#e9dfc4", 3.8f, 50, 0.56f),
            Car("vanta",    "Vanta RS",       "muscle",   3000, "#b3141c", 5.4f, 64, 0.56f),
            Car("tsurugi",  "Tsurugi R",      "coupe",    3200, "#5b3b8c", 5.3f, 64, 0.62f, L: 4.50f, b: 0.13f),
            Car("thunder",  "Thunderline 70", "muscle",   3400, "#e0661d", 5.2f, 62, 0.54f, H: 1.32f),
            Car("monolith", "Monolith",       "suv",      3800, "#121417", 4.4f, 56, 0.54f, L: 5.10f, W: 2.00f, H: 1.80f, fw: 4.05f),
            Car("ghost",    "Ghostline",      "gt",       4500, "#6f7780", 5.8f, 70, 0.58f),
            Car("arcwing",  "Arcwing",        "super",    6000, "#8bd12a", 6.6f, 80, 0.58f),
            Car("halo",     "Halo Wedge",     "wedge",    5200, "#f4f3ef", 6.0f, 74, 0.58f),
            Car("scorpa",   "Scorpa Spyder",  "super",    7000, "#ff7a1a", 6.8f, 82, 0.58f, H: 1.08f),
            Car("nyx",      "Nyx V12",        "hyper",    9000, "#0b0c0e", 7.4f, 90, 0.56f),
        };
    }
}
