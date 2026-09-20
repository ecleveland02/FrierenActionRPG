using UnityEngine;

namespace Frieren.Player
{
    /// <summary>Feeds the cloak shader from the authoritative sprint state.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLocomotion))]
    public sealed class SprintCloakWind : MonoBehaviour
    {
        private static readonly int SprintWind = Shader.PropertyToID("_SprintWind");
        private static readonly int WindTime = Shader.PropertyToID("_WindTime");
        [SerializeField, Min(0f)] private float response = 5f;
        private MaterialPropertyBlock properties;
        private PlayerLocomotion locomotion;
        private Renderer cloakRenderer;
        private float amount;

        private void Awake()
        {
            locomotion = GetComponent<PlayerLocomotion>();
            properties = new MaterialPropertyBlock();
            foreach (var candidate in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (candidate.sharedMaterial != null && candidate.sharedMaterial.HasProperty(SprintWind))
                {
                    cloakRenderer = candidate;
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            if (cloakRenderer == null) return;
            float target = locomotion.IsSprinting ? Mathf.Clamp01(locomotion.NormalizedSpeed) : 0f;
            amount = Mathf.MoveTowards(amount, target, response * Time.deltaTime);
            cloakRenderer.GetPropertyBlock(properties);
            properties.SetFloat(SprintWind, amount);
            properties.SetFloat(WindTime, Time.time);
            cloakRenderer.SetPropertyBlock(properties);
        }
    }
}
