using UnityEngine;

namespace Frieren.Characters.Animation
{
    /// <summary>Presentation-only wind for character cloth. Movement remains owned by the motor.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Cloth))]
    public sealed class CharacterClothWind : MonoBehaviour
    {
        [SerializeField] private Vector3 windAcceleration = new Vector3(0.6f, 0f, 0.3f);
        [SerializeField, Min(0f)] private float gustStrength = 0.35f;
        [SerializeField, Min(0f)] private float gustFrequency = 0.6f;
        [SerializeField, Min(0.1f)] private float teleportDistance = 3f;
        private Cloth fabric;
        private Vector3 previousPosition;

        private void Awake() => fabric = GetComponent<Cloth>();

        private void OnEnable()
        {
            if (fabric == null) fabric = GetComponent<Cloth>();
            previousPosition = transform.position;
            fabric.ClearTransformMotion();
        }

        private void Update()
        {
            if ((transform.position - previousPosition).sqrMagnitude > teleportDistance * teleportDistance)
                fabric.ClearTransformMotion();
            previousPosition = transform.position;
            float gust = Mathf.Sin(Time.time * gustFrequency * Mathf.PI * 2f) * gustStrength;
            fabric.externalAcceleration = windAcceleration + new Vector3(gust, 0f, gust * 0.4f);
        }

        // Weather can supply acceleration directly; this does not discover WindZone objects.
        public void SetWind(Vector3 acceleration) => windAcceleration = acceleration;
    }
}
