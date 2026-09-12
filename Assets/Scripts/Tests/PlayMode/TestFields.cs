using System;
using System.Reflection;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Sets a private serialized field on a component or asset, for building test fixtures.
    /// </summary>
    /// <remarks>
    /// The production types keep their tuning in private <c>[SerializeField]</c> fields, which is
    /// correct - a public setter on every one of them would be a worse codebase in exchange for
    /// easier tests. The editor writes them through the inspector; a runtime test has to reach them
    /// some other way, and reflection is the least invasive option available.
    ///
    /// The one thing that matters is that it fails loudly. A misspelled field name that silently did
    /// nothing would leave a test passing against default values while claiming to test something
    /// else, which is worse than no test at all. So a missing field throws.
    /// </remarks>
    public static class TestFields
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static T Set<T>(T target, string fieldName, object value)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            FieldInfo field = Find(target.GetType(), fieldName);

            if (field == null)
            {
                throw new MissingFieldException(
                    $"{target.GetType().Name} has no field '{fieldName}'. It was probably renamed - " +
                    "fix the test rather than deleting it, or it will pass against a default value.");
            }

            field.SetValue(target, value);
            return target;
        }

        public static object Get(object target, string fieldName)
        {
            FieldInfo field = Find(target.GetType(), fieldName);

            if (field == null)
            {
                throw new MissingFieldException($"{target.GetType().Name} has no field '{fieldName}'.");
            }

            return field.GetValue(target);
        }

        /// <summary>Walks up the hierarchy, since a field may live on a base class such as CharacterResource.</summary>
        private static FieldInfo Find(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, Flags);

                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
