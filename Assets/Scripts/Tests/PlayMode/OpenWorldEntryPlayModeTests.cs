using System.Collections;
using Frieren.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    public sealed class OpenWorldEntryPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerSpawnsAboveOpenWorldTerrainAndRemainsGrounded()
        {
            SceneManager.LoadScene("FrierenOpenWorld", LoadSceneMode.Single);
            float deadline = Time.realtimeSinceStartup + 15f;
            PlayerSpawner spawner = null;
            while ((spawner = Object.FindFirstObjectByType<PlayerSpawner>()) == null || spawner.SpawnedPlayer == null)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Open world player did not spawn.");
                yield return null;
            }

            GameObject player = spawner.SpawnedPlayer;
            float initialY = player.transform.position.y;
            yield return new WaitForSeconds(1f);
            Assert.Greater(player.transform.position.y, 0f, "Player fell below the world.");
            Assert.Less(Mathf.Abs(player.transform.position.y - initialY), 1f,
                "A grounded spawn should settle, not continue falling.");
        }
    }
}
