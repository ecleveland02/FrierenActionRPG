using System;
using System.Collections.Generic;
using Frieren.Core.Debugging;
using Frieren.Core.Persistence;
using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// An ordered list of things to do, and a note of which are done.
    /// </summary>
    /// <remarks>
    /// Not a quest system. Quests are a later milestone with authored content, branching and
    /// persistence of their own; this is the smallest thing that turns an area into a slice rather
    /// than a sandbox - somewhere to go, and a way to know you got there.
    ///
    /// Goals are completed by id from anywhere, so nothing has to know what completes them. A
    /// trigger volume, an interaction and a dead enemy all call the same method, which is what lets
    /// the level decide what counts without this component learning about any of it.
    ///
    /// Ordered, but not enforced. A player who levitates over the gate and skips straight to the
    /// tower has solved the level, not broken it - the design pillar is that problems have several
    /// answers, and refusing to tick a goal because it was reached the wrong way would contradict
    /// the entire point.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SliceObjectives : MonoBehaviour, IPersistentState
    {
        [Serializable]
        public sealed class Goal
        {
            [Tooltip("Stable id, used to complete it from elsewhere and written into saves.")]
            public string id;

            [TextArea(1, 2)]
            public string description;

            [Tooltip("Optional. Shown once the goal is done, so the level can say what changed.")]
            public string doneNote;
        }

        [Serializable]
        private sealed class ObjectiveState
        {
            public List<string> completed = new List<string>();
        }

        [SerializeField] private List<Goal> goals = new List<Goal>();

        [SerializeField]
        [Tooltip("Shown when every goal is done.")]
        private string completionMessage = "The tower is yours. Slice complete.";

        private readonly HashSet<string> completed = new HashSet<string>();

        public IReadOnlyList<Goal> Goals => goals;

        public bool IsComplete => completed.Count >= CountedGoals;

        /// <summary>Raised with the goal's id each time one is newly completed.</summary>
        public event Action<string> GoalCompleted;

        public event Action AllGoalsCompleted;

        public string StateKey => "objectives";

        private int CountedGoals
        {
            get
            {
                int total = 0;

                for (int i = 0; i < goals.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(goals[i].id))
                    {
                        total++;
                    }
                }

                return total;
            }
        }

        public bool IsDone(string goalId) => !string.IsNullOrEmpty(goalId) && completed.Contains(goalId);

        /// <summary>Marks a goal done. Returns false if it was already done or is not a goal here.</summary>
        public bool Complete(string goalId)
        {
            if (string.IsNullOrWhiteSpace(goalId) || !Knows(goalId) || !completed.Add(goalId))
            {
                return false;
            }

            GameLog.Info(LogChannel.Core, $"Objective complete: {goalId}.", this);
            GoalCompleted?.Invoke(goalId);

            if (IsComplete)
            {
                GameLog.Info(LogChannel.Core, completionMessage, this);
                AllGoalsCompleted?.Invoke();
            }

            return true;
        }

        private bool Knows(string goalId)
        {
            for (int i = 0; i < goals.Count; i++)
            {
                if (goals[i].id == goalId)
                {
                    return true;
                }
            }

            GameLog.Warn(LogChannel.Core, $"Nothing here has a goal called '{goalId}'.", this);
            return false;
        }

        public string CaptureState()
        {
            var state = new ObjectiveState();
            state.completed.AddRange(completed);
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<ObjectiveState>(json);

            if (state?.completed == null)
            {
                return;
            }

            completed.Clear();

            for (int i = 0; i < state.completed.Count; i++)
            {
                completed.Add(state.completed[i]);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle style;

        private void OnGUI()
        {
            if (goals.Count == 0)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

            var area = new Rect(Screen.width - 330f, 10f, 320f, 34f + goals.Count * 34f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(IsComplete ? completionMessage : "Objectives", style);

            for (int i = 0; i < goals.Count; i++)
            {
                Goal goal = goals[i];
                bool done = IsDone(goal.id);
                string text = done && !string.IsNullOrEmpty(goal.doneNote) ? goal.doneNote : goal.description;

                Color previous = GUI.color;
                GUI.color = done ? new Color(0.55f, 0.9f, 0.6f) : Color.white;
                GUILayout.Label($"{(done ? "x" : "-")} {text}", style);
                GUI.color = previous;
            }

            GUILayout.EndArea();
        }
#endif
    }
}
