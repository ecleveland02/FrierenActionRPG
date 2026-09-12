using System;
using System.Collections;
using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Core.Debugging;
using UnityEngine;

namespace Frieren.Magic
{
    /// <summary>
    /// Casts spells for a character. The brief calls this CharacterMagic.
    /// </summary>
    /// <remarks>
    /// Reads no input. The player's input layer and, in Milestone 5, an enemy's behaviour tree both
    /// call <see cref="TryCast"/>, so casting works identically for both without this component
    /// knowing which is driving it.
    ///
    /// A cast holds <see cref="CharacterActionLock"/> for its duration, so locomotion stands down
    /// and a dodge cannot start mid-cast. That is the same lock the dodge takes, which is why it was
    /// worth building in Milestone 2 with only one claimant.
    ///
    /// Mana is spent when the cast begins, not when it lands. Spending at the end would let a player
    /// interrupt every cast a frame before release and never pay for anything.
    /// </remarks>
    [RequireComponent(typeof(CharacterMana))]
    [RequireComponent(typeof(CharacterActionLock))]
    [DisallowMultipleComponent]
    public sealed class CharacterSpellcaster : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Where casts originate and aim from. Falls back to the main camera, then to this transform.")]
        private Transform aimSource;

        [SerializeField]
        [Tooltip("Height above the character's feet that casts originate from when there is no aim source.")]
        private float castHeight = 1.4f;

        private CharacterMana mana;
        private CharacterActionLock actionLock;
        private ICharacterAnimation characterAnimation;
        private readonly SpellCooldownTracker cooldowns = new SpellCooldownTracker();
        private Coroutine castRoutine;
        private bool channelReleaseRequested;

        /// <summary>Raised when a cast begins, before mana is spent.</summary>
        public event Action<SpellDefinition> CastStarted;

        /// <summary>Raised when a cast resolves, with whether any effect reported doing something.</summary>
        public event Action<SpellDefinition, bool> CastCompleted;

        /// <summary>Raised when a cast is refused, with a short reason for feedback.</summary>
        public event Action<SpellDefinition, string> CastRefused;

        public bool IsCasting => castRoutine != null;

        /// <summary>True while a channelled spell is running and has not yet been released.</summary>
        public bool IsChannelling { get; private set; }

        /// <summary>Raised each time a channelled spell re-applies its effects.</summary>
        public event Action<SpellDefinition> ChannelTicked;

        /// <summary>Raised when a channel ends, with why: released, out of mana, or timed out.</summary>
        public event Action<SpellDefinition, string> ChannelEnded;

        public SpellDefinition CurrentSpell { get; private set; }

        private void Awake()
        {
            mana = GetComponent<CharacterMana>();
            actionLock = GetComponent<CharacterActionLock>();
            characterAnimation = GetComponent<ICharacterAnimation>();
        }

        private void OnDisable() => CancelCast();

        public float CooldownRemaining(SpellDefinition spell) =>
            spell == null ? 0f : cooldowns.RemainingFor(spell.Id, Time.time);

        /// <summary>
        /// Attempts to cast. Returns false and reports a reason when it cannot, rather than failing
        /// silently - a spell that does nothing with no explanation is indistinguishable from a bug.
        /// </summary>
        public bool TryCast(SpellDefinition spell)
        {
            if (spell == null)
            {
                return Refuse(null, "no spell selected");
            }

            if (IsCasting)
            {
                return Refuse(spell, "already casting");
            }

            if (!cooldowns.IsReady(spell.Id, Time.time))
            {
                return Refuse(spell, $"on cooldown for {cooldowns.RemainingFor(spell.Id, Time.time):0.0}s");
            }

            if (!spell.HasEffects)
            {
                return Refuse(spell, "the spell has no effects assigned");
            }

            if (!mana.CanAfford(spell.ManaCost))
            {
                return Refuse(spell, $"needs {spell.ManaCost:0} mana, has {mana.Current:0}");
            }

            if (spell.HoldsActionLock && !actionLock.TryAcquire(this))
            {
                return Refuse(spell, "busy with another action");
            }

            if (!mana.TrySpend(spell.ManaCost))
            {
                // Should be unreachable given CanAfford above, but a silent free cast is worse than
                // a loud contradiction.
                if (spell.HoldsActionLock)
                {
                    actionLock.Release(this);
                }

                return Refuse(spell, "mana was spent by something else this frame");
            }

            CurrentSpell = spell;

            // Cleared here, not in Channel: the cast time runs before the channel starts, and a
            // release during it must survive to end the channel on its first check. Clearing later
            // discarded it, so a quick click began a channel nothing would ever stop.
            channelReleaseRequested = false;
            cooldowns.Begin(spell.Id, Time.time, spell.Cooldown);
            CastStarted?.Invoke(spell);
            characterAnimation?.PlayAction(CharacterAction.CastStart);
            castRoutine = StartCoroutine(CastRoutine(spell));
            return true;
        }

        /// <summary>
        /// Asks a channelled spell to stop at its next tick. Called when the cast input is released.
        /// </summary>
        /// <remarks>
        /// A request rather than an immediate stop, so the channel always unwinds through one path -
        /// releasing the action lock, clearing state, raising its event - no matter whether it ended
        /// because of the player, the mana running out or the duration cap.
        /// </remarks>
        public void ReleaseChannel() => channelReleaseRequested = true;

        /// <summary>Stops a cast in progress. The mana is not refunded; it was spent to begin.</summary>
        public void CancelCast()
        {
            if (castRoutine != null)
            {
                StopCoroutine(castRoutine);
                castRoutine = null;
            }

            CurrentSpell = null;
            IsChannelling = false;
            channelReleaseRequested = false;
            actionLock.Release(this);
        }

        private IEnumerator CastRoutine(SpellDefinition spell)
        {
            if (spell.CastTime > 0f)
            {
                yield return new WaitForSeconds(spell.CastTime);
            }

            characterAnimation?.PlayAction(spell.CastAnimation);

            bool affectedSomething = spell.IsChannelled
                ? false
                : ApplyEffects(spell, BuildContext(spell), log: true);

            if (spell.IsChannelled)
            {
                yield return Channel(spell);
                affectedSomething = true;
            }

            castRoutine = null;
            CurrentSpell = null;
            IsChannelling = false;
            channelReleaseRequested = false;
            actionLock.Release(this);
            CastCompleted?.Invoke(spell, affectedSomething);
        }

        /// <summary>
        /// Runs a channelled spell until the player releases, the mana runs out, or the cap is hit.
        /// </summary>
        /// <remarks>
        /// Targeting is re-resolved every tick rather than locked in at the start, so a channelled
        /// spell follows the aim - which is what makes holding levitation on a block feel like
        /// holding it rather than having thrown something at it.
        ///
        /// Mana is charged per tick from a per-second rate, and a tick that cannot be paid for ends
        /// the channel instead of running free. TrySpend is all-or-nothing, so there is no partial
        /// tick to reason about.
        /// </remarks>
        private IEnumerator Channel(SpellDefinition spell)
        {
            IsChannelling = true;

            float started = Time.time;
            bool firstTick = true;
            float tick = spell.ChannelTickInterval;
            float costPerTick = spell.ManaPerSecond * tick;
            string reason = "released";

            while (!channelReleaseRequested)
            {
                if (spell.MaxChannelSeconds > 0f && Time.time - started >= spell.MaxChannelSeconds)
                {
                    reason = "reached its duration limit";
                    break;
                }

                if (costPerTick > 0f && !mana.TrySpend(costPerTick))
                {
                    reason = "ran out of mana";
                    break;
                }

                bool affected = ApplyEffects(spell, BuildContext(spell), log: false);

                if (firstTick)
                {
                    // One line per channel rather than ten a second, but enough to tell a spell
                    // that never started from one that started and reached nothing.
                    firstTick = false;
                    SpellContext probe = BuildContext(spell);
                    GameLog.Info(LogChannel.Magic,
                        $"{name} began channelling {spell.Id} ({spell.Targeting}) on " +
                        $"'{(probe.Target != null ? probe.Target.name : "nothing")}': " +
                        $"{(affected ? "something responded" : "nothing responded")}.", this);
                }

                ChannelTicked?.Invoke(spell);

                yield return new WaitForSeconds(tick);
            }

            // The cooldown starts when the channel ends, not when it began; otherwise a long channel
            // would come off cooldown while still running.
            cooldowns.Begin(spell.Id, Time.time, spell.Cooldown);

            GameLog.Info(LogChannel.Magic,
                $"{name} stopped channelling {spell.Id} after {Time.time - started:0.0}s: {reason}.", this);
            ChannelEnded?.Invoke(spell, reason);
        }

        private bool ApplyEffects(SpellDefinition spell, SpellContext context, bool log)
        {
            bool affectedSomething = false;

            foreach (SpellEffect effect in spell.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                try
                {
                    affectedSomething |= effect.Apply(context);
                }
                catch (Exception exception)
                {
                    // One broken effect must not abandon the cast mid-list, leaving the action lock
                    // held and the character frozen.
                    GameLog.Error(LogChannel.Magic,
                        $"Effect '{effect.name}' on spell '{spell.Id}' threw: {exception.Message}", effect);
                }
            }

            if (log)
            {
                string outcome = affectedSomething
                    ? "something responded"
                    : context.Target != null
                        ? $"'{context.Target.name}' does not respond to this spell"
                        : "nothing was hit";

                GameLog.Info(LogChannel.Magic, $"{name} cast {spell.Id} at {context.Point}: {outcome}.", this);
            }

            return affectedSomething;
        }

        private SpellContext BuildContext(SpellDefinition spell)
        {
            Transform source = ResolveAimSource();
            Vector3 origin = source != null ? source.position : transform.position + Vector3.up * castHeight;
            Vector3 direction = source != null ? source.forward : transform.forward;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            switch (spell.Targeting)
            {
                case SpellTargeting.Self:
                    return new SpellContext(gameObject, spell, origin, direction,
                        transform.position, gameObject, Vector3.up);

                case SpellTargeting.AroundCaster:
                    return new SpellContext(gameObject, spell, origin, direction,
                        transform.position, null, Vector3.up);

                default:
                    return BuildRayContext(spell, origin, direction);
            }
        }

        private SpellContext BuildRayContext(SpellDefinition spell, Vector3 origin, Vector3 direction)
        {
            bool hit = Physics.Raycast(origin, direction, out RaycastHit info, spell.Range,
                spell.AimMask, QueryTriggerInteraction.Ignore);

            if (!hit)
            {
                return new SpellContext(gameObject, spell, origin, direction,
                    origin + direction * spell.Range, null, -direction);
            }

            // A ray from a camera behind the character can hit the character first. Ignoring the
            // caster's own colliders is cheaper and more predictable than juggling layers.
            if (info.collider != null && info.collider.transform.IsChildOf(transform))
            {
                Vector3 past = info.point + direction * 0.05f;

                if (Physics.Raycast(past, direction, out RaycastHit second,
                        spell.Range, spell.AimMask, QueryTriggerInteraction.Ignore))
                {
                    info = second;
                }
                else
                {
                    return new SpellContext(gameObject, spell, origin, direction,
                        origin + direction * spell.Range, null, -direction);
                }
            }

            return new SpellContext(gameObject, spell, origin, direction,
                info.point, info.collider != null ? info.collider.gameObject : null, info.normal);
        }

        private Transform ResolveAimSource()
        {
            if (aimSource != null)
            {
                return aimSource;
            }

            UnityEngine.Camera main = UnityEngine.Camera.main;
            return main != null ? main.transform : null;
        }

        /// <summary>Points casts at a transform, normally the camera. Called by the spawner.</summary>
        public void SetAimSource(Transform source) => aimSource = source;

        private bool Refuse(SpellDefinition spell, string reason)
        {
            GameLog.Info(LogChannel.Magic, $"{name} could not cast {(spell != null ? spell.Id : "nothing")}: {reason}.", this);
            CastRefused?.Invoke(spell, reason);
            return false;
        }
    }
}
