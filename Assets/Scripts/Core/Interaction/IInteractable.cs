using UnityEngine;

namespace Frieren.Core.Interaction
{
    /// <summary>
    /// Something the player can deliberately act on at close range, typically with the Interact input.
    /// </summary>
    /// <remarks>
    /// This lives in Core rather than in the player or interaction assemblies on purpose. The player
    /// needs to call it and the objects that implement it belong to entirely unrelated systems -
    /// doors, chests, NPCs, the environmental puzzle objects arriving in Milestone 6. Putting the
    /// contract in the lowest layer lets both sides depend on it without depending on each other.
    ///
    /// This is deliberately separate from how a spell affects an object. Pressing a button on a
    /// lever and hitting it with a fire spell are different verbs with different rules, and
    /// collapsing them into one interface would force every flammable crate to pretend it has an
    /// interaction prompt.
    /// </remarks>
    public interface IInteractable
    {
        /// <summary>Where the prompt should appear and what the player measures distance against.</summary>
        Transform InteractionPoint { get; }

        /// <summary>Verb shown in the prompt, e.g. "Open", "Read", "Talk".</summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// Whether <paramref name="actor"/> may interact right now. A locked door still exists and
        /// is still worth prompting about, so returning false here should mean "not offerable at
        /// all", not "will fail".
        /// </summary>
        bool CanInteract(GameObject actor);

        void Interact(GameObject actor);
    }
}
