using UnityEngine;

namespace Frieren.Core.Magic
{
    /// <summary>
    /// One delivery of magical influence to a point in the world.
    /// </summary>
    /// <remarks>
    /// Deliberately says nothing about which spell produced it. A receiver that asked "was this
    /// Fire?" would have to be updated for every new spell that ought to work on it, which is the
    /// coupling this whole design exists to avoid.
    ///
    /// <see cref="Magnitude"/> is in arbitrary units chosen per element and compared against
    /// thresholds on the receiver. A crate that needs 10 Heat to catch is tuned against the Heat
    /// its spells emit, so the units only have to be consistent, not physical.
    /// </remarks>
    public readonly struct MagicPulse
    {
        public MagicPulse(MagicElement element, float magnitude, Vector3 point,
            Vector3 direction = default, GameObject source = null)
        {
            Element = element;
            Magnitude = Mathf.Max(0f, magnitude);
            Point = point;
            Direction = direction;
            Source = source;
        }

        public MagicElement Element { get; }

        /// <summary>Strength of the effect. Receivers compare this against their own thresholds.</summary>
        public float Magnitude { get; }

        /// <summary>Where the magic landed, for effects that care about the point of contact.</summary>
        public Vector3 Point { get; }

        /// <summary>Which way it was travelling, for effects that push. Zero when undirected.</summary>
        public Vector3 Direction { get; }

        /// <summary>Who cast it, if anyone. Null for environmental sources.</summary>
        public GameObject Source { get; }

        public override string ToString() => $"{Element} {Magnitude:0.#} at {Point}";
    }
}
