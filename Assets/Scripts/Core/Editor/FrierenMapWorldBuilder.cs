using System;
using System.IO;
using Frieren.Core.Input;
using Frieren.Player;
using Frieren.Player.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    /// <summary>Creates a deliberately broad, low-density terrain foundation for later regions.</summary>
    public static class FrierenMapWorldBuilder
    {
        private const string ScenePath = "Assets/Scenes/FrierenOpenWorld.unity";
        private const string Output = "Assets/Art/World/FrierenMap";
        private const int TilesPerAxis = 3;
        private const float TileSize = 4000f;
        private const float WorldSize = TilesPerAxis * TileSize;
        private const float TerrainHeight = 1200f;
        private static TerrainLayer[] layers;
        private static GameObject forestPine;
        private static GameObject forestBroadleaf;

        [MenuItem("Frieren/World/Build Frieren Open World (12 km)", priority = 31)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before building the open world.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("ArtSource/WorldBackups");
            if (File.Exists(ScenePath)) File.Copy(ScenePath,
                "ArtSource/WorldBackups/FrierenOpenWorld-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity", false);
            EnsureFolder(Output);
            EnsureFolder(Output + "/Terrain");
            EnsureFolder(Output + "/Materials");
            layers = CreateLayers();
            forestPine = EldenbrookLandscape.TreePrototype(Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green.prefab"), 16f);
            forestBroadleaf = EldenbrookLandscape.TreePrototype(Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab"), 13f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Frieren Open World (12 km)");
            BuildLighting(root.transform);
            BuildOcean(root.transform);

            var terrains = new Terrain[TilesPerAxis, TilesPerAxis];
            for (int z = 0; z < TilesPerAxis; z++)
            for (int x = 0; x < TilesPerAxis; x++)
                terrains[x, z] = BuildTile(root.transform, x, z);
            for (int z = 0; z < TilesPerAxis; z++)
            for (int x = 0; x < TilesPerAxis; x++)
                terrains[x, z].SetNeighbors(x > 0 ? terrains[x - 1, z] : null,
                    z < TilesPerAxis - 1 ? terrains[x, z + 1] : null,
                    x < TilesPerAxis - 1 ? terrains[x + 1, z] : null,
                    z > 0 ? terrains[x, z - 1] : null);
            EldenbrookLandscape.BuildWaterAndBridge(root.transform);
            BuildScenicProps(root.transform);
            BuildLandmarks(root.transform);
            BuildPlayerStart(root.transform);
            StarterVillageBuilder.Build(root.transform, SurfaceHeight(1150f, 50f));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created Frieren Open World: 12 x 12 km (55.6 square miles), 9 terrain tiles. Open Assets/Scenes/FrierenOpenWorld.unity to explore.");
        }

        [MenuItem("Frieren/World/Render Frieren Open World Preview", priority = 32)]
        public static void RenderPreview()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateLandscape();
            RenderView("FrierenOpenWorld", new Vector3(1210f,126f,-23f), new Vector3(1150f,80f,50f));
            RenderView("EldenbrookForest", new Vector3(840f,SurfaceHeight(840f,160f)+95f,160f), new Vector3(450f,SurfaceHeight(450f,500f)+10f,500f));
            RenderView("EldenbrookBridge", new Vector3(1480f,108f,-5f), new Vector3(1430f,79f,50f));
        }

        private static void ValidateLandscape()
        {
            int trees = 0;
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain.GetComponent<TerrainCollider>() == null) throw new InvalidOperationException("Terrain collider missing.");
                trees += terrain.terrainData.treeInstanceCount;
            }
            if (trees < 3000) throw new InvalidOperationException("Forest tree population unexpectedly low: " + trees);
            for (float z=-1800f;z<1800f;z+=50f)
                if (SurfaceHeight(EldenbrookLandscape.RiverX(z),z)>=EldenbrookLandscape.WaterHeight(z))
                    throw new InvalidOperationException("River bed above water at " + z);
            var spawn=Object.FindFirstObjectByType<PlayerSpawner>();
            if(spawn==null || Mathf.Abs(spawn.transform.position.y-SurfaceHeight(spawn.transform.position.x,spawn.transform.position.z))>1f)
                throw new InvalidOperationException("Player spawn is not aligned to terrain.");
            Physics.SyncTransforms();
            if(!Physics.Raycast(spawn.transform.position+Vector3.up,Vector3.down,3f,1<<GameLayers.Ground,QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Spawn has no ground collision.");
            for(int x=-20;x<=20;x+=2)
            {
                var origin=new Vector3(EldenbrookLandscape.RiverX(50f)+x,EldenbrookLandscape.WaterHeight(50f)+5f,50f);
                if(!Physics.Raycast(origin,Vector3.down,out var hit,4f,1<<GameLayers.Ground,QueryTriggerInteraction.Ignore) ||
                   Mathf.Abs(hit.point.y-(EldenbrookLandscape.WaterHeight(50f)+2.2f))>.3f)
                    throw new InvalidOperationException("Bridge deck collision gap at "+x);
            }
            Debug.Log("Landscape validation passed: colliders present, river channel submerged near village, spawn ground collision and bridge deck checked; trees="+trees);
        }

        private static void RenderView(string name, Vector3 position, Vector3 lookAt)
        {
            var camera = new GameObject("Open World Preview Camera").AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            camera.fieldOfView = 54f;
            camera.farClipPlane = 18000f;
            var target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
            Directory.CreateDirectory("ArtSource/WorldPreviews");
            File.WriteAllBytes("ArtSource/WorldPreviews/"+name+".png", image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(camera.gameObject);
            Debug.Log("Wrote ArtSource/WorldPreviews/"+name+".png");
        }

        private static Terrain BuildTile(Transform parent, int tileX, int tileZ)
        {
            string path = Output + "/Terrain/Frieren_" + tileX + "_" + tileZ + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, path);
            }
            bool villageTile = tileX == 1 && tileZ == 1;
            data.heightmapResolution = villageTile ? 2049 : 513;
            data.alphamapResolution = villageTile ? 2048 : 512;
            data.baseMapResolution = 1024;
            data.size = new Vector3(TileSize, TerrainHeight, TileSize);
            data.terrainLayers = layers;
            int resolution = data.heightmapResolution;
            var heights = new float[resolution, resolution];
            float startX = -WorldSize * 0.5f + tileX * TileSize;
            float startZ = -WorldSize * 0.5f + tileZ * TileSize;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float worldX = startX + x * TileSize / (resolution - 1f);
                float worldZ = startZ + z * TileSize / (resolution - 1f);
                heights[z, x] = Height(worldX, worldZ) / TerrainHeight;
            }
            data.SetHeights(0, 0, heights);
            Paint(data, startX, startZ);
            PopulateTrees(data, startX, startZ, tileX, tileZ);
            EldenbrookLandscape.PlantForest(data, startX, startZ);
            WorldFoliagePass.Populate(data, startX, startZ);
            var gameObject = Terrain.CreateTerrainGameObject(data);
            gameObject.name = "Terrain " + tileX + "," + tileZ;
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = new Vector3(startX, 0f, startZ);
            gameObject.layer = GameLayers.Ground;
            var terrain = gameObject.GetComponent<Terrain>();
            terrain.drawHeightmap = true;
            terrain.heightmapPixelError = 8;
            terrain.basemapDistance = 2000f;
            terrain.treeDistance = 1400f;
            terrain.treeBillboardDistance = 180f;
            terrain.treeCrossFadeLength = 30f;
            terrain.detailObjectDensity = 0.55f;
            terrain.detailObjectDistance = 160f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            return terrain;
        }

        private static float Height(float x, float z)
        {
            float edgeDistance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float coast = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5900f, 4300f, edgeDistance));
            float rolling = (Mathf.PerlinNoise((x + 20000f) / 1200f, (z - 13000f) / 1200f) - .5f) * 55f;
            float broad = (Mathf.PerlinNoise((x - 9000f) / 3200f, (z + 4000f) / 3200f) - .5f) * 100f;
            float northRange = Gaussian(x, z, -1200f, 3900f, 1800f, 1200f) * 520f
                + Gaussian(x, z, 2200f, 3650f, 1500f, 1050f) * 485f
                + Gaussian(x, z, -3900f, 3100f, 1100f, 900f) * 310f;
            float westernRange = Gaussian(x, z, -3700f, -1800f, 900f, 2600f) * 330f;
            float southernRange = Gaussian(x, z, 500f, -4700f, 1900f, 1000f) * 290f;
            float easternHills = Gaussian(x, z, 3600f, -900f, 1300f, 1800f) * 150f;
            float riverValley = Gaussian(x, z, -250f, 400f, 750f, 4800f) * 95f
                + Gaussian(x, z, 1800f, -900f, 500f, 2500f) * 70f;
            float mainland = Mathf.Lerp(12f, 105f, coast);
            float mountainMass = northRange + westernRange + southernRange;
            float mountainMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(90f, 310f, mountainMass));
            // Sharp connected crests within the existing ranges; the meadow's height stays unchanged.
            float ridge = 1f - Mathf.Abs(2f * Mathf.PerlinNoise((x + 18000f) / 510f, (z + 23000f) / 510f) - 1f);
            float crags = 1f - Mathf.Abs(2f * Mathf.PerlinNoise((x + 7300f) / 170f, (z + 16200f) / 170f) - 1f);
            float summit = Mathf.Max(Peak(x, z, -1250f, 4100f, 720f, 300f),
                Peak(x, z, 2200f, 3800f, 580f, 310f));
            float relief = mountainMask * (ridge * ridge * 160f + crags * 32f + summit);
            float hummocks=(Mathf.PerlinNoise(x/90f+120f,z/90f+180f)-.5f)*8f*coast*(1f-mountainMask);
            float height = Mathf.Clamp(mainland + rolling + broad + mountainMass + easternHills - riverValley + relief+hummocks, 3f, TerrainHeight - 10f);
            float townDistance = Vector2.Distance(new Vector2(x, z), new Vector2(1150f, 50f));
            height = Mathf.Lerp(78f, height, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(65f, 125f, townDistance)));
            return EldenbrookLandscape.Shape(x,z,height);
        }

        private static float Peak(float x, float z, float centerX, float centerZ, float radius, float rise)
        {
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(centerX, centerZ));
            return Mathf.Pow(Mathf.Clamp01(1f - distance / radius), 1.25f) * rise;
        }

        private static float Gaussian(float x, float z, float centerX, float centerZ, float radiusX, float radiusZ)
        {
            float dx = (x - centerX) / radiusX;
            float dz = (z - centerZ) / radiusZ;
            return Mathf.Exp(-(dx * dx + dz * dz) * 1.8f);
        }

        private static void Paint(TerrainData data, float startX, float startZ)
        {
            int resolution = data.alphamapResolution;
            var map = new float[resolution, resolution, layers.Length];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float worldX = startX + (x + .5f) * TileSize / resolution;
                float worldZ = startZ + (z + .5f) * TileSize / resolution;
                float height = Height(worldX, worldZ);
                float north = Mathf.InverseLerp(2200f, 5200f, worldZ);
                float south = Mathf.InverseLerp(-1800f, -5600f, worldZ);
                float mountain = Mathf.InverseLerp(160f, 460f, height);
                float slope = data.GetSteepness((x + .5f) / resolution, (z + .5f) / resolution);
                float cliff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 43f, slope));
                float sand = Mathf.Clamp01(Mathf.InverseLerp(55f, 15f, height) + south * .55f);
                float snowLine = 530f + 55f * Mathf.PerlinNoise(worldX / 190f + 50f, worldZ / 190f + 80f);
                float snow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(snowLine, snowLine + 160f, height)) * north * (1f - cliff * .9f);
                float rock = Mathf.Clamp01(Mathf.Max(mountain * .9f, cliff) * (1f - snow));
                float meadow = Mathf.Clamp01(1f - mountain * 1.4f) * (1f - south * .75f) * (1f - cliff)
                    * Mathf.Lerp(.25f,.85f,Mathf.PerlinNoise(worldX/75f+30f,worldZ/75f+60f));
                float grass = Mathf.Clamp01(1f - sand - snow - rock - meadow);
                float total = grass + meadow + rock + snow + sand;
                map[z, x, 0] = grass / total;
                map[z, x, 1] = meadow / total;
                map[z, x, 2] = rock / total;
                map[z, x, 3] = snow / total;
                map[z, x, 4] = sand / total;
                EldenbrookLandscape.Paint(map,x,z,worldX,worldZ);
            }
            data.SetAlphamaps(0, 0, map);
        }

        private static void PopulateTrees(TerrainData data, float startX, float startZ, int tileX, int tileZ)
        {
            var pine = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green.prefab");
            var fruit = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab");
            data.treePrototypes = new[] { new TreePrototype { prefab = forestPine }, new TreePrototype { prefab = forestBroadleaf } };
            var random = new System.Random(1807 + tileX * 47 + tileZ * 131);
            var trees = new System.Collections.Generic.List<TreeInstance>();
            for (int i = 0; i < 1100; i++)
            {
                float x = (float)random.NextDouble();
                float z = (float)random.NextDouble();
                float worldX = startX + x * TileSize;
                float worldZ = startZ + z * TileSize;
                float height = Height(worldX, worldZ);
                bool northernForest = worldZ > 300f && height < 190f;
                bool centralForest = worldZ > -2300f && worldZ < 1800f && Mathf.Abs(worldX + 900f) < 3000f;
                bool coastal = Mathf.Max(Mathf.Abs(worldX), Mathf.Abs(worldZ)) > 4700f;
                if (coastal || height > 260f || (!northernForest && !centralForest) || !EldenbrookLandscape.CanPlant(worldX,worldZ)) continue;
                trees.Add(new TreeInstance
                {
                    // Terrain tree positions are normalized in all three axes. A zero Y plants every
                    // trunk at the TerrainData base rather than at the sampled surface.
                    position = new Vector3(x, data.GetInterpolatedHeight(x, z) / data.size.y, z), prototypeIndex = worldZ > 2200f ? 0 : random.Next(4) == 0 ? 1 : 0,
                    widthScale = .75f + (float)random.NextDouble() * .55f,
                    heightScale = .75f + (float)random.NextDouble() * .65f,
                    color = Color.Lerp(new Color(.55f, .62f, .42f), Color.white, (float)random.NextDouble() * .14f),
                    lightmapColor = Color.white
                });
            }
            data.treeInstances = trees.ToArray();
        }

        private static void PopulateGrass(TerrainData data, float startX, float startZ)
        {
            var grass = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Plants/PT_Grass_02.prefab");
            data.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototype = grass, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = .65f, maxWidth = 1.35f, minHeight = .7f, maxHeight = 1.45f,
                    healthyColor = new Color(.42f, .68f, .26f), dryColor = new Color(.69f, .60f, .25f)
                }
            };
            data.SetDetailResolution(256, 32);
            var detail = new int[256, 256];
            for (int z = 0; z < 256; z++)
            for (int x = 0; x < 256; x++)
            {
                float worldX = startX + (x + .5f) * TileSize / 256f;
                float worldZ = startZ + (z + .5f) * TileSize / 256f;
                float height = Height(worldX, worldZ);
                float meadow = Mathf.PerlinNoise((worldX + 24100f) / 150f, (worldZ - 19300f) / 150f);
                bool fertile = height > 40f && height < 190f && worldZ < 2400f && worldZ > -3900f;
                detail[z, x] = fertile && meadow > .42f ? meadow > .68f ? 3 : 1 : 0;
                if (!EldenbrookLandscape.CanPlant(worldX, worldZ)) detail[z, x] = 0;
            }
            data.SetDetailLayer(0, 0, 0, detail);
            data.wavingGrassTint = new Color(.49f, .73f, .31f);
            data.wavingGrassStrength = .35f;
            data.wavingGrassAmount = .65f;
            data.wavingGrassSpeed = .55f;
        }

        private static void BuildOcean(Transform parent)
        {
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "Endless Coast Water";
            ocean.transform.SetParent(parent);
            ocean.transform.position = new Vector3(0f, 21f, 0f);
            ocean.transform.localScale = new Vector3(1450f, 1f, 1450f);
            Object.DestroyImmediate(ocean.GetComponent<Collider>());
            ocean.GetComponent<Renderer>().sharedMaterial = Material("Ocean", new Color(.035f, .18f, .31f), .65f);
        }

        private static void BuildRivers(Transform parent)
        {
            var rivers = new GameObject("Rivers").transform;
            rivers.SetParent(parent);
            Material water = Material("River Water", new Color(.04f, .28f, .39f), .45f);
            River(rivers, "Northern River", water, 25f, new[]
            {
                new Vector2(1250f, 5100f), new Vector2(800f, 4100f), new Vector2(350f, 3000f),
                new Vector2(700f, 1750f), new Vector2(1380f, 50f), new Vector2(2250f, -900f), new Vector2(5100f, -1100f)
            });
            River(rivers, "Western River", water, 19f, new[]
            {
                new Vector2(-4200f, 2850f), new Vector2(-3150f, 1900f), new Vector2(-2100f, 900f),
                new Vector2(-1200f, 200f), new Vector2(-250f, -900f), new Vector2(1200f, -1900f), new Vector2(4700f, -2600f)
            });
        }

        private static void BuildTravelRoads(Transform parent)
        {
            var roads = new GameObject("Travel Roads").transform;
            roads.SetParent(parent);
            Material earth = Material("Sunlit Earth Path", new Color(.52f, .38f, .20f), .04f);
            Ribbon(roads, "Central Meadow Road", earth, 7f, .8f, new[]
            {
                new Vector2(1150f, -2700f), new Vector2(920f, -1600f), new Vector2(1120f, -700f),
                new Vector2(1150f, 50f), new Vector2(780f, 900f), new Vector2(300f, 1800f), new Vector2(100f, 3100f)
            });
            Ribbon(roads, "Forest Road", earth, 6f, .8f, new[]
            {
                new Vector2(-2650f, -600f), new Vector2(-1750f, -100f), new Vector2(-850f, 250f),
                new Vector2(100f, 160f), new Vector2(1150f, 50f)
            });
        }

        private static void River(Transform parent, string name, Material material, float width, Vector2[] anchors)
        {
            Ribbon(parent, name, material, width, .75f, anchors);
        }

        private static void Ribbon(Transform parent, string name, Material material, float width, float heightOffset, Vector2[] anchors)
        {
            const int samplesPerSegment = 128;
            int count = (anchors.Length - 1) * samplesPerSegment + 1;
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float raw = i / (float)samplesPerSegment;
                int index = Mathf.Min(anchors.Length - 2, Mathf.FloorToInt(raw));
                float t = raw - index;
                Vector2 point = Vector2.Lerp(anchors[index], anchors[index + 1], t);
                points[i] = new Vector3(point.x, Height(point.x, point.y) + heightOffset, point.y);
            }
            var vertices = new Vector3[count * 2]; var uv = new Vector2[count * 2]; var triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = (points[Mathf.Min(count - 1, i + 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * width;
                vertices[i * 2] = points[i] - side; vertices[i * 2 + 1] = points[i] + side;
                if (name.Contains("Road"))
                {
                    for (int edge = 0; edge < 2; edge++)
                    {
                        int vertex = i * 2 + edge;
                        vertices[vertex].y = SurfaceHeight(vertices[vertex].x, vertices[vertex].z) + .035f;
                    }
                }
                uv[i * 2] = new Vector2(0f, i * .14f); uv[i * 2 + 1] = new Vector2(1f, i * .14f);
                if (i == count - 1) continue;
                int v = i * 2; int t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            string path = Output + "/Terrain/" + name.Replace(' ', '_') + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.name = Path.GetFileNameWithoutExtension(path); mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var river = new GameObject(name); river.transform.SetParent(parent);
            river.AddComponent<MeshFilter>().sharedMesh = mesh;
            river.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void BuildScenicProps(Transform parent)
        {
            var props = new GameObject("Scenic Foreground Dressing").transform;
            props.SetParent(parent);
            GameObject pine = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green.prefab");
            GameObject fruit = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab");
            GameObject rock = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Generic_Rock_01.prefab");
            GameObject riverRock = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_River_Rock_Pile_02.prefab");
            GameObject flower = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Flowers/PT_Poppy_02.prefab");
            GameObject shrub = Require<GameObject>("Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/Shrubs/PT_Generic_Shrub_01_green.prefab");
            var random = new System.Random(72019);
            for (int i = 0; i < 600; i++)
            {
                float x = -250f + (float)random.NextDouble()*1400f;
                float z = -140f + (float)random.NextDouble()*1290f;
                if (EldenbrookLandscape.ForestDensity(x,z)<.5f) continue;
                Place(props, i%5==0 ? rock : shrub, new Vector2(x,z), .8f+(float)random.NextDouble()*1.2f,random.Next(360));
            }
            for (int i = 0; i < 88; i++)
            {
                float z = -1300f + i * 26f;
                float side = (i % 2 == 0 ? -1f : 1f) * (18f + (float)random.NextDouble() * 42f);
                Place(props, i % 5 == 0 ? pine : fruit, new Vector2(1080f + side, z), .8f + (float)random.NextDouble() * .65f, random.Next(360));
            }
            for (int i = 0; i < 260; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 22f + (float)random.NextDouble() * 140f;
                Place(props, flower, new Vector2(1150f + Mathf.Cos(angle) * radius, 50f + Mathf.Sin(angle) * radius),
                    .35f + (float)random.NextDouble() * .4f, random.Next(360));
            }
            for (int i = 0; i < 180; i++)
            {
                float x = -1700f + (float)random.NextDouble() * 4100f;
                float z = -1900f + (float)random.NextDouble() * 3900f;
                if (Mathf.Abs(x - 1150f) < 110f && Mathf.Abs(z - 50f) < 150f) continue;
                Place(props, shrub, new Vector2(x, z), .45f + (float)random.NextDouble() * .8f, random.Next(360));
            }
            for (int i = 0; i < 48; i++)
            {
                float x = -3500f + (float)random.NextDouble() * 6700f;
                float z = 2600f + (float)random.NextDouble() * 2500f;
                Place(props, rock, new Vector2(x, z), 1.3f + (float)random.NextDouble() * 3f, random.Next(360));
            }
            // Small scree clusters establish scale around exposed foothills.
            for (int cluster = 0; cluster < 60; cluster++)
            {
                float x = -4200f + (float)random.NextDouble() * 7800f;
                float z = 2500f + (float)random.NextDouble() * 2400f;
                if (Height(x, z) < 200f || Height(x, z) > 580f) continue;
                for (int stone = 0; stone < 4; stone++)
                    Place(props, rock, new Vector2(x + random.Next(-18, 19), z + random.Next(-18, 19)),
                        .4f + (float)random.NextDouble() * 1.6f, random.Next(360));
            }
            for (int i = 0; i < 56; i++)
            {
                float z = -900f + i * 32f;
                float x = EldenbrookLandscape.RiverX(z) + ((i % 2 == 0 ? -1f : 1f) * (EldenbrookLandscape.RiverWidth(z)+4f+(float)random.NextDouble()*9f));
                if(EldenbrookLandscape.RoadDistance(x,z)<10f)continue;
                Place(props, riverRock, new Vector2(x, z), 1.5f + (float)random.NextDouble() * 2f, random.Next(360), true);
                Place(props, shrub, new Vector2(x+3f,z+3f), 1.3f,random.Next(360),true);
            }
        }

        private static void Place(Transform parent, GameObject prefab, Vector2 position, float scale, float yaw, bool riverBank = false)
        {
            if (!riverBank && !EldenbrookLandscape.CanPlant(position.x, position.y)) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.SetParent(parent);
            instance.transform.position = new Vector3(position.x, SurfaceHeight(position.x, position.y), position.y);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale *= scale;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static void BuildLandmarks(Transform parent)
        {
            var root = new GameObject("Regional Landmark Seeds").transform;
            root.SetParent(parent);
            Marker(root, "Northern Mountains - future snowy frontier", new Vector3(0f, 0f, 4100f));
            Marker(root, "Central Forest - future ancient wood", new Vector3(-1050f, 0f, 300f));
            Marker(root, "Eastern Coast - future port", new Vector3(3950f, 0f, -800f));
            Marker(root, "Southern Drylands - future desert", new Vector3(300f, 0f, -4300f));
            Marker(root, "Player Start - Central Meadow", new Vector3(1150f, 0f, 50f));
        }

        private static float SurfaceHeight(float x, float z)
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (x >= origin.x && x <= origin.x + size.x && z >= origin.z && z <= origin.z + size.z)
                    return origin.y + terrain.terrainData.GetInterpolatedHeight((x - origin.x) / size.x, (z - origin.z) / size.z);
            }
            throw new InvalidOperationException("No terrain at " + x + ", " + z);
        }

        private static void BuildPlayerStart(Transform parent)
        {
            Vector3 meadow = new Vector3(1150f, 0f, 50f);
            meadow.y = Height(meadow.x, meadow.z) + .1f;
            var spawn = new GameObject("PlayerSpawn - Central Meadow");
            spawn.transform.SetParent(parent);
            spawn.transform.SetPositionAndRotation(meadow, Quaternion.Euler(0f, 18f, 0f));
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = meadow + new Vector3(0f, 3f, -5f);
            cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .15f;
            camera.farClipPlane = 6500f;
            cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<OrbitCameraRig>();
            var spawner = spawn.AddComponent<PlayerSpawner>();
            SetReference(rig, "inputReader", Require<InputReader>(ProjectPaths.InputReader));
            SetReference(spawner, "playerPrefab", Require<GameObject>(ProjectPaths.PlayerPrefab));
            SetReference(spawner, "cameraRig", rig);
        }

        private static void Marker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.position = new Vector3(position.x, Height(position.x, position.z) + 1f, position.z);
        }

        private static void BuildLighting(Transform parent)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(parent);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            sun.color = new Color(1f, .87f, .68f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.skybox = Require<Material>("Assets/Art/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .59f, .78f);
            RenderSettings.ambientEquatorColor = new Color(.38f, .48f, .35f);
            RenderSettings.ambientGroundColor = new Color(.13f, .17f, .13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.54f, .67f, .72f);
            RenderSettings.fogStartDistance = 800f;
            RenderSettings.fogEndDistance = 5000f;
        }

        private static TerrainLayer[] CreateLayers()
        {
            return new[]
            {
                Layer("Lowland Grass", new Color(.18f, .34f, .13f),
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Grass_Diffuse.png",
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Grass_Normal.png"),
                Layer("Meadow", new Color(.42f, .50f, .19f),
                    "Assets/Art/Polytope Studio/Lowpoly_Environments/Sources/Textures/PT_Ground_Grass_Green_01.png"),
                Layer("Mountain Rock", new Color(.31f, .31f, .28f),
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Rock_Diffuse.png",
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texure_Rock_Normal.png"),
                Layer("Northern Snow", new Color(.76f, .79f, .75f)),
                Layer("Southern Sand", new Color(.61f, .48f, .26f),
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Dirt_Diffuse.png",
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Dirt_Normal.png"),
                Layer("Trail Earth", Color.white,
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Dirt_Diffuse.png",
                    "Assets/Art/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Dirt_Normal.png")
            };
        }

        private static TerrainLayer Layer(string name, Color color, string diffusePath = null, string normalPath = null)
        {
            string texturePath = Output + "/Materials/" + name + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(2, 2) { name = name };
                texture.SetPixels(new[] { color, color, color, color }); texture.Apply();
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            string layerPath = Output + "/Terrain/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
            layer.diffuseTexture = string.IsNullOrEmpty(diffusePath) ? texture : Require<Texture2D>(diffusePath);
            layer.normalMapTexture = string.IsNullOrEmpty(normalPath) ? null : Require<Texture2D>(normalPath);
            layer.tileSize = name == "Mountain Rock" ? new Vector2(90f, 90f) : new Vector2(24f, 24f);
            if (name == "Trail Earth") layer.tileSize = new Vector2(5f,5f);
            layer.normalScale = name == "Mountain Rock" ? .7f : .3f;
            layer.smoothness = .05f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Material Material(string name, Color color, float smoothness)
        {
            string path = Output + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_Color", color); material.SetFloat("_Glossiness", smoothness); material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);
        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
