using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Frieren.Core.EditorTools
{
    public static class WorldFoliagePass
    {
        private const string Demo = "Assets/Art/Polytope Studio/Lowpoly_Demos/Environment_Free/";
        public static void BuildAndPreview()
        {
            FrierenMapWorldBuilder.Build();
            FrierenMapWorldBuilder.RenderPreview();
            PreviewGround();
        }
        public static void Populate(TerrainData data,float startX,float startZ)
        {
            // The map below stores instance counts, not 0..255 coverage values.
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            var shortGrass=Prototype("PT_Grass_02_v1",.45f,.9f);
            var tallGrass=Prototype("PT_High_Grass_02_v1",1f,1.8f);
            var flowers=Prototype("PT_Poppy_02_v1",.65f,1.1f);
            data.detailPrototypes=new[]{shortGrass,tallGrass,flowers};
            int resolution=startX==-2000f && startZ==-2000f ? 2048 : 1024;
            data.SetDetailResolution(resolution,16);
            var low=new int[resolution,resolution];
            var tall=new int[resolution,resolution];
            var bloom=new int[resolution,resolution];
            int occupied=0;
            float cell=data.size.x/resolution;
            for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++)
            {
                float nx=(x+.5f)/resolution,nz=(z+.5f)/resolution;
                float wx=startX+nx*data.size.x,wz=startZ+nz*data.size.z;
                float height=data.GetInterpolatedHeight(nx,nz);
                if(height<28f || height>340f || wz< -3700f || data.GetSteepness(nx,nz)>29f)continue;
                if(EldenbrookLandscape.RoadDistance(wx,wz)<6f+cell ||
                   EldenbrookLandscape.RiverDistance(wx,wz)<EldenbrookLandscape.RiverWidth(wz)+2f+cell ||
                   Vector2.Distance(new Vector2(wx,wz),new Vector2(1150f,50f))<65f+cell)continue;
                float patch=Mathf.PerlinNoise(wx/37f+280f,wz/37f+140f);
                float habitat=Mathf.PerlinNoise(wx/210f+40f,wz/210f+90f);
                float forest=EldenbrookLandscape.ForestDensity(wx,wz);
                if(habitat<.32f)continue;
                low[z,x]=patch>.34f ? Mathf.Clamp(Mathf.RoundToInt(cell*cell*1.1f),1,16) : 0;
                if(patch>.55f && habitat>.43f && forest<.8f)
                {
                    tall[z,x]=Mathf.Clamp(Mathf.RoundToInt(cell*cell*1.5f),1,16);
                    low[z,x]/=3;occupied++;
                }
                if(patch>.49f && patch<.55f && habitat>.55f && forest<.5f)bloom[z,x]=1;
            }
            data.SetDetailLayer(0,0,0,low);data.SetDetailLayer(0,0,1,tall);data.SetDetailLayer(0,0,2,bloom);
            EditorUtility.SetDirty(data);
            Debug.Log("Foliage tile "+startX+","+startZ+": tall-grass cells="+occupied);
        }
        public static void RefreshFoliage()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/FrierenOpenWorld.unity");
            foreach(var terrain in Terrain.activeTerrains)
            {
                Populate(terrain.terrainData,terrain.transform.position.x,terrain.transform.position.z);
                terrain.Flush();
            }
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            PreviewGround();
        }
        private static DetailPrototype Prototype(string name,float min,float max)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Demo+"Helpers/"+name+".prefab");
            if(prefab==null)throw new System.InvalidOperationException("Missing sample foliage "+name);
            return new DetailPrototype{prototype=prefab,usePrototypeMesh=true,useInstancing=true,
                renderMode=DetailRenderMode.VertexLit,minWidth=.8f,maxWidth=1.3f,minHeight=min,maxHeight=max,
                healthyColor=Color.white,dryColor=Color.white,noiseSpread=.2f};
        }
        public static void PreviewGround()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FrierenOpenWorld.unity");
            var camera=new GameObject("Foliage Preview").AddComponent<Camera>();
            var terrain=Terrain.activeTerrains[0];
            foreach(var candidate in Terrain.activeTerrains)
                if(candidate.transform.position.x==-2000f && candidate.transform.position.z==-2000f)terrain=candidate;
            float x=1240f,z=130f;
            var map=terrain.terrainData.GetDetailLayer(0,0,terrain.terrainData.detailResolution,terrain.terrainData.detailResolution,1);
            float nearest=float.MaxValue;
            for(int iz=0;iz<map.GetLength(0);iz++)for(int ix=0;ix<map.GetLength(1);ix++)
            {
                if(map[iz,ix]<3)continue;
                float wx=terrain.transform.position.x+(ix+.5f)*4000f/map.GetLength(1);
                float wz=terrain.transform.position.z+(iz+.5f)*4000f/map.GetLength(0);
                float distance=Vector2.Distance(new Vector2(wx,wz),new Vector2(1240f,130f));
                if(distance<nearest){nearest=distance;x=wx;z=wz;}
            }
            Debug.Log("Grass preview location="+x+","+z+" nearest patch="+nearest);
            int px=Mathf.FloorToInt((x+2000f)/4000f*terrain.terrainData.detailPatchCount);
            int pz=Mathf.FloorToInt((z+2000f)/4000f*terrain.terrainData.detailPatchCount);
            var instances=terrain.terrainData.ComputeDetailInstanceTransforms(px,pz,1,.55f,out var bounds);
            Debug.Log("Detail transforms="+instances.Length+" bounds="+bounds+" scatter="+terrain.terrainData.detailScatterMode);
            if(instances.Length>0)Debug.Log("First detail="+JsonUtility.ToJson(instances[0]));
            foreach(var detail in terrain.terrainData.detailPrototypes)
                Debug.Log("Grass prototype bounds="+detail.prototype.GetComponent<MeshFilter>().sharedMesh.bounds+" density="+detail.density);
            camera.transform.position=new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+2f,z);
            camera.transform.rotation=Quaternion.Euler(10f,315f,0f);camera.farClipPlane=6000f;
            camera.Render();
            int frames=0;
            EditorApplication.CallbackFunction finish=null;
            finish=()=>
            {
                camera.Render();
                if(++frames<30)return;
                EditorApplication.update-=finish;
                Capture(camera,"MeadowGround");
                Object.DestroyImmediate(camera.gameObject);
                if(Application.isBatchMode)EditorApplication.Exit(0);
            };
            EditorApplication.update+=finish;
        }
        public static void InspectSample()
        {
            EditorSceneManager.OpenScene(Demo+"Environment_Free.unity");
            foreach(var terrain in Terrain.activeTerrains)
            {
                Debug.Log("SAMPLE terrain size="+terrain.terrainData.size+" detail="+terrain.terrainData.detailResolution+" distance="+terrain.detailObjectDistance);
                foreach(var detail in terrain.terrainData.detailPrototypes)
                    Debug.Log("SAMPLE detail="+detail.prototype+" size="+detail.minHeight+".."+detail.maxHeight+" instanced="+detail.useInstancing);
            }
            var cam=Camera.main;
            if(cam==null)throw new System.InvalidOperationException("Sample camera missing");
            Capture(cam,"PolytopeSample");
        }
        private static void Capture(Camera cam,string name)
        {
            var rt=new RenderTexture(1400,900,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var image=new Texture2D(1400,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();
            Directory.CreateDirectory("ArtSource/WorldPreviews");
            File.WriteAllBytes("ArtSource/WorldPreviews/"+name+".png",image.EncodeToPNG());
            cam.targetTexture=null;RenderTexture.active=null;
            Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);
        }
    }
}
