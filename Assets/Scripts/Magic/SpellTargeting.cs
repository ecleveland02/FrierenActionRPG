namespace Frieren.Magic
{
    /// <summary>
    /// How a spell decides where it lands.
    /// </summary>
    /// <remarks>
    /// All of these resolve instantly. A travelling projectile is a presentation concern - the bolt
    /// you watch fly is a visual played along the resolved line - and adding one later changes when
    /// effects fire, not how targeting works.
    /// </remarks>
    public enum SpellTargeting
    {
        /// <summary>Lands on the caster. Barriers, buffs, self-heals.</summary>
        Self = 0,

        /// <summary>A ray along the aim direction, landing on the first thing hit or at maximum range.</summary>
        Ray = 1,

        /// <summary>Centred on the caster, for effects with their own radius.</summary>
        AroundCaster = 2
    }
}
