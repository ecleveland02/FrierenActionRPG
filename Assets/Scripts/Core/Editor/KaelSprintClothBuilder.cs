using System;
using System.IO;
using Frieren.Characters.Animation;
using Frieren.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    public static class KaelSprintClothBuilder
    {
        private const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string TexturePath = "Assets/Art/Characters/Kael/Models/KaelRigged/tripo_5de71a06_7047_4e68_ab76_910a53510aad_Clone_BaseColor.png";
        private const string MaterialPath = "Assets/Art/Characters/Kael/Models/KaelRigged/KaelSprintWind.mat";

        [MenuItem("Frieren/Kael/Add Sprint Cloth Wind", priority = 66)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before configuring Kael's cloak wind.");
            Shader shader = Shader.Find("Frieren/Kael Sprint Cloak Wind");
            if (shader == null) throw new InvalidOperationException("Kael sprint cloak shader did not compile.");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) throw new FileNotFoundException(TexturePath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Kael Sprint Wind" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.mainTexture = texture;
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Glossiness", .2f);
            EditorUtility.SetDirty(material);

            GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                SkinnedMeshRenderer skin = LargestRenderer(prefab);
                Cloth cloth = skin.GetComponent<Cloth>();
                if (cloth != null) Object.DestroyImmediate(cloth);
                CharacterClothWind clothWind = skin.GetComponent<CharacterClothWind>();
                if (clothWind != null) Object.DestroyImmediate(clothWind);
                Component oldDriver = skin.GetComponent("CharacterCloakWind");
                if (oldDriver != null) Object.DestroyImmediate(oldDriver);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(skin.gameObject);
                SprintCloakWind sprintWind = prefab.GetComponent<SprintCloakWind>();
                if (sprintWind == null) sprintWind = prefab.AddComponent<SprintCloakWind>();
                if (sprintWind == null) throw new InvalidOperationException("Could not attach the sprint cloak driver.");
                skin.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Kael Cloth] Added direct sprint-responsive cloak lift and flutter.");
        }

        private static SkinnedMeshRenderer LargestRenderer(GameObject root)
        {
            SkinnedMeshRenderer best = null;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null && (best == null || renderer.sharedMesh.vertexCount > best.sharedMesh.vertexCount))
                    best = renderer;
            return best != null ? best : throw new InvalidOperationException("Kael has no skinned mesh renderer.");
        }
    }
}
