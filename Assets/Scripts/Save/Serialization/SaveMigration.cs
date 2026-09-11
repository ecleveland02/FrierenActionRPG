using UnityEngine;

namespace Frieren.Save.Serialization
{
    /// <summary>
    /// Upgrades save files written by older builds to <see cref="SaveGameData.CurrentVersion"/>.
    /// </summary>
    /// <remarks>
    /// There is nothing to migrate yet - version 1 is the first format. The type exists now so the
    /// call site in <c>SaveService</c> is already in place: when the format changes, add a step
    /// here instead of scattering version checks through gameplay code.
    /// </remarks>
    public static class SaveMigration
    {
        /// <summary>
        /// Attempts to bring <paramref name="data"/> up to the current version in place.
        /// </summary>
        /// <returns><c>false</c> if the file cannot be used, e.g. it was written by a newer build.</returns>
        public static bool TryMigrate(SaveGameData data, out string failureReason)
        {
            failureReason = null;

            if (data == null)
            {
                failureReason = "Save data was null.";
                return false;
            }

            if (data.Version > SaveGameData.CurrentVersion)
            {
                failureReason =
                    $"Save file version {data.Version} is newer than this build supports " +
                    $"({SaveGameData.CurrentVersion}).";
                return false;
            }

            while (data.Version < SaveGameData.CurrentVersion)
            {
                int before = data.Version;

                switch (data.Version)
                {
                    // case 1: MigrateV1ToV2(data); data.Version = 2; break;
                    default:
                        failureReason = $"No migration step defined for save version {data.Version}.";
                        return false;
                }

                if (data.Version == before)
                {
                    failureReason = $"Migration step for version {before} did not advance the version.";
                    return false;
                }
            }

            return true;
        }
    }
}
