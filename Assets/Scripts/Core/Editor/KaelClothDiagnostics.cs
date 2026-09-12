using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    /// <summary>
    /// Measures which coordinate space Unity actually reports cloth particles in.
    /// </summary>
    /// <remarks>
    /// <c>KaelClothBuilder</c> maps mesh vertices to cloth particles by nearest position, and
    /// refuses to proceed when the nearest match is further than a millimetre - correctly, because
    /// a wrong mapping silently assigns the wrong freedom to the wrong particle and produces cloth
    /// that looks plausible and behaves wrongly. When that guard fires, the question is which of
    /// several plausible assumptions about <c>Cloth.vertices</c> and <c>BakeMesh</c> is wrong.
    ///
    /// The Unity documentation is not precise enough to settle it by reading, so this measures it:
    /// every combination of mesh-side and particle-side space is scored against the same data, and
    /// the one that lines up is the correct one. Read-only - it creates a temporary instance and
    /// destroys it, and writes no assets.
    /// </remarks>
    internal static class KaelClothDiagnostics
    {
        private const string ModelPath = "Assets/Art/Characters/KaelMeshyCloth/KaelCloth.fbx";
        private const string ClothMeshName = "Kael_CoatCloth";

        private readonly struct Hypothesis
        {
            public Hypothesis(string name, Func<Vector3, Vector3> toWorld)
            {
                Name = name;
                ToWorld = toWorld;
            }

            public string Name { get; }

            public Func<Vector3, Vector3> ToWorld { get; }
        }

        [MenuItem("Frieren/Kael/Diagnose Cloth Mapping", priority = 73)]
        public static void Diagnose()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Kael Diag] Exit Play mode first.");
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            if (model == null)
            {
                Debug.LogError($"[Kael Diag] No model at {ModelPath}.");
                return;
            }

            GameObject instance = Object.Instantiate(model);

            try
            {
                SkinnedMeshRenderer skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(r => r.name == ClothMeshName);

                if (skin == null)
                {
                    Debug.LogError($"[Kael Diag] No SkinnedMeshRenderer named '{ClothMeshName}'. Found: " +
                        string.Join(", ", instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Select(r => r.name)));
                    return;
                }

                Report(skin);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void Report(SkinnedMeshRenderer skin)
        {
            Transform skinTransform = skin.transform;
            Transform rootBone = skin.rootBone != null ? skin.rootBone : skinTransform;

            var withoutScale = new Mesh();
            var withScale = new Mesh();

            try
            {
                skin.BakeMesh(withoutScale, false);
                skin.BakeMesh(withScale, true);

                Debug.Log(
                    "[Kael Diag] setup\n" +
                    $"  skin transform '{skinTransform.name}' pos {skinTransform.position} " +
                    $"rot {skinTransform.rotation.eulerAngles} lossyScale {skinTransform.lossyScale}\n" +
                    $"  root bone     '{rootBone.name}' pos {rootBone.position} " +
                    $"rot {rootBone.rotation.eulerAngles} lossyScale {rootBone.lossyScale}\n" +
                    $"  sharedMesh verts {skin.sharedMesh.vertexCount}, colors {skin.sharedMesh.colors.Length}\n" +
                    $"  baked verts (no scale) {withoutScale.vertexCount}, (scale) {withScale.vertexCount}");

                var cloth = skin.gameObject.AddComponent<Cloth>();
                Vector3[] particles = cloth.vertices;

                Debug.Log($"[Kael Diag] cloth particles {particles.Length}, coefficients {cloth.coefficients.Length}");

                var meshSides = new[]
                {
                    new Hypothesis("bake(noScale) -> skin.TransformPoint",
                        v => skinTransform.TransformPoint(v)),
                    new Hypothesis("bake(withScale) -> skin.rot*v + skin.pos",
                        v => skinTransform.rotation * v + skinTransform.position),
                    new Hypothesis("bake(noScale) raw (already world)", v => v),
                };

                var particleSides = new[]
                {
                    new Hypothesis("root.rot*p + root.pos  (builder's assumption)",
                        p => rootBone.rotation * p + rootBone.position),
                    new Hypothesis("rootBone.TransformPoint(p)", p => rootBone.TransformPoint(p)),
                    new Hypothesis("skin.TransformPoint(p)", p => skinTransform.TransformPoint(p)),
                    new Hypothesis("skin.rot*p + skin.pos", p => skinTransform.rotation * p + skinTransform.position),
                    new Hypothesis("p raw (already world)", p => p),
                };

                Vector3[] noScaleVerts = withoutScale.vertices;
                Vector3[] scaleVerts = withScale.vertices;

                string best = "none";
                float bestScore = float.PositiveInfinity;

                foreach (Hypothesis meshSide in meshSides)
                {
                    Vector3[] source = meshSide.Name.Contains("withScale") ? scaleVerts : noScaleVerts;
                    Vector3[] worldVerts = source.Select(meshSide.ToWorld).ToArray();

                    foreach (Hypothesis particleSide in particleSides)
                    {
                        float worst = 0f;
                        double total = 0d;

                        for (int i = 0; i < particles.Length; i++)
                        {
                            Vector3 p = particleSide.ToWorld(particles[i]);
                            float nearest = float.PositiveInfinity;

                            for (int j = 0; j < worldVerts.Length; j++)
                            {
                                float d = (p - worldVerts[j]).sqrMagnitude;

                                if (d < nearest)
                                {
                                    nearest = d;
                                }
                            }

                            nearest = Mathf.Sqrt(nearest);
                            total += nearest;
                            worst = Mathf.Max(worst, nearest);
                        }

                        float mean = (float)(total / Mathf.Max(1, particles.Length));
                        string label = $"{meshSide.Name}  vs  {particleSide.Name}";
                        Debug.Log($"[Kael Diag] mean {mean:F6}m  max {worst:F6}m   {label}");

                        if (worst < bestScore)
                        {
                            bestScore = worst;
                            best = label;
                        }
                    }
                }

                Debug.Log($"[Kael Diag] BEST: max error {bestScore:F6}m using {best}\n" +
                          (bestScore < 0.001f
                              ? "  Under 1mm - this is the correct pairing; the builder should use it."
                              : "  Still above 1mm for every pairing, so the mismatch is not a coordinate " +
                                "space at all. Most likely the baked pose differs from the pose the cloth " +
                                "particles were initialised from, which points at the FBX's bind pose."));

                Object.DestroyImmediate(cloth);
            }
            finally
            {
                Object.DestroyImmediate(withoutScale);
                Object.DestroyImmediate(withScale);
            }
        }
    }
}
