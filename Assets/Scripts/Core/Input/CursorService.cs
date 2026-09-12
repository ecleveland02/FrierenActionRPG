using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Core.Input
{
    /// <summary>
    /// The single authority over whether the hardware pointer is locked and hidden.
    /// </summary>
    /// <remarks>
    /// A claim registry rather than a flag, for the same reason <c>TimeScaleService</c> is: more
    /// than one thing wants the pointer, and they overlap. The spell wheel wants it while it is
    /// open, the pause menu wants it while the game is paused, and an inventory screen will want it
    /// later. If each of them assigned <c>Cursor.lockState</c> directly, closing the wheel while
    /// the pause menu was still up would re-lock the pointer out from under the menu, and the only
    /// way to find out would be to open two things at once and notice.
    ///
    /// Each claimant passes itself as the owner, so a claim can only ever be released by whoever
    /// filed it. The pointer is free while at least one claim stands and captured otherwise, which
    /// makes the gameplay default - locked and hidden - the state with no claims at all rather than
    /// something anyone has to remember to restore.
    ///
    /// <see cref="Apply"/> is separate from the claim calls only so it can be re-run without
    /// changing anything, which is what regaining window focus needs: Unity releases the lock when
    /// the player alt-tabs, and nothing tells the game the lock is gone. Re-applying every frame
    /// would be the obvious alternative and is wrong - it fights the operating system and traps
    /// the pointer in the window.
    /// </remarks>
    public sealed class CursorService
    {
        // A list rather than a set: there are two or three of these at most, so the scan is
        // cheaper than hashing, and reference equality is what "the same owner" means here.
        private readonly List<object> claims = new List<object>();

        /// <summary>True while something wants a visible pointer.</summary>
        public bool PointerWanted => claims.Count > 0;

        public int ClaimCount => claims.Count;

        /// <summary>Raised when the pointer becomes free or captured, not on every claim.</summary>
        public event Action<bool> PointerWantedChanged;

        public bool HasClaim(object owner) => owner != null && claims.Contains(owner);

        /// <summary>Asks for a visible pointer. Idempotent: claiming twice still needs one release.</summary>
        public bool RequestPointer(object owner)
        {
            if (owner == null || claims.Contains(owner))
            {
                return false;
            }

            bool wasWanted = PointerWanted;
            claims.Add(owner);
            Settle(wasWanted);
            return true;
        }

        /// <summary>Drops one owner's claim. Releasing a claim nobody filed does nothing.</summary>
        public bool ReleasePointer(object owner)
        {
            if (owner == null)
            {
                return false;
            }

            bool wasWanted = PointerWanted;

            if (!claims.Remove(owner))
            {
                return false;
            }

            Settle(wasWanted);
            return true;
        }

        /// <summary>
        /// Drops every claim. For scene changes, where the objects holding claims are about to be
        /// destroyed and would otherwise leave the pointer free with nothing left to free it.
        /// </summary>
        public void ReleaseAll()
        {
            if (claims.Count == 0)
            {
                return;
            }

            claims.Clear();
            Settle(true);
        }

        /// <summary>
        /// Clears every claim and hands the pointer back, free and visible. For shutting down.
        /// </summary>
        /// <remarks>
        /// Not <see cref="ReleaseAll"/>, which drops the claims and then captures the pointer
        /// because no-claims is the gameplay default. Leaving play mode that way locks the cursor
        /// to a game view that is no longer running, and the editor stays unusable until something
        /// else releases it.
        /// </remarks>
        public void ReleaseAndShow()
        {
            claims.Clear();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        /// <summary>Pushes the current decision at Unity. Safe to call at any time.</summary>
        public void Apply()
        {
            bool wanted = PointerWanted;
            UnityEngine.Cursor.lockState = wanted ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = wanted;
        }

        private void Settle(bool wasWanted)
        {
            Apply();

            if (PointerWanted != wasWanted)
            {
                PointerWantedChanged?.Invoke(PointerWanted);
            }
        }
    }
}
