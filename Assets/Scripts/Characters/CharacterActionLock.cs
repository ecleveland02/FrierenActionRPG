using System;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// Single-holder claim on a character's movement and actions, so two abilities cannot drive the
    /// same body at once.
    /// </summary>
    /// <remarks>
    /// Milestone 2 only has one claimant - the dodge - which is exactly why this is worth building
    /// now rather than later. Spell casting in Milestone 4 and attacks in Milestone 5 need the same
    /// guarantee, and the alternative is every ability checking a boolean on every other ability.
    /// That grows quadratically and is how "you can cast while dodging while being staggered" bugs
    /// happen.
    ///
    /// Deliberately not a queue or a priority system. If something needs to interrupt something
    /// else, that is a design decision, and it should be written down as one rather than falling out
    /// of a priority number.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterActionLock : MonoBehaviour
    {
        private object owner;

        public bool IsLocked => owner != null;

        /// <summary>Raised with the new owner when the lock is taken.</summary>
        public event Action<object> Acquired;

        /// <summary>Raised with the previous owner when the lock is released.</summary>
        public event Action<object> Released;

        public bool IsHeldBy(object candidate) => candidate != null && ReferenceEquals(owner, candidate);

        /// <summary>Takes the lock. Returns false if someone else already holds it.</summary>
        public bool TryAcquire(object candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (owner != null && !ReferenceEquals(owner, candidate))
            {
                return false;
            }

            bool wasFree = owner == null;
            owner = candidate;

            if (wasFree)
            {
                Acquired?.Invoke(owner);
            }

            return true;
        }

        /// <summary>Releases the lock. Ignored if <paramref name="candidate"/> is not the holder.</summary>
        public bool Release(object candidate)
        {
            if (candidate == null || !ReferenceEquals(owner, candidate))
            {
                return false;
            }

            object previous = owner;
            owner = null;
            Released?.Invoke(previous);
            return true;
        }

        /// <summary>
        /// Drops the lock regardless of holder. For death, scene transitions and other cases where
        /// the holder will never get the chance to release it itself.
        /// </summary>
        public void ForceRelease()
        {
            if (owner == null)
            {
                return;
            }

            object previous = owner;
            owner = null;
            Released?.Invoke(previous);
        }
    }
}
