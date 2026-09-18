using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    // Shared geometry rules keep the heightfield, planting and surface paint aligned.
    public static class EldenbrookLandscape
    {
        private static float Smooth(float lower, float upper, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lower, upper, value));
        public static float RiverX(float z) => 1430f + 65f * Mathf.Sin((z - 50f) / 330f) + 32f * Mathf.Sin((z - 50f) / 770f);
        public static float WaterHeight(float z) => 21f + (z + 6000f) * (57f / 6050f);
        public static float RiverWidth(float z) => 13f + 3f * Mathf.Sin(z / 240f) + 2f * Mathf.Sin(z / 470f);
        public static float RiverDistance(float x, float z) => Mathf.Abs(x - RiverX(z));
        public static float NorthRoadX(float z) => 1150f + 48f * Mathf.Sin((z - 50f) / 330f) * Mathf.SmoothStep(0f, 1f, Mathf.Abs(z - 50f) / 110f);
        public static float ForestRoadZ(float x) => 50f + 110f * Mathf.Sin((1150f-x)/310f) * Mathf.SmoothStep(0f,1f,(1150f-x)/100f);
        public static float RoadDistance(float x,float z)
        {
            float distance = Mathf.Abs(x-NorthRoadX(z));
            if(x < 1150f && x > -400f) distance=Mathf.Min(distance,Mathf.Abs(z-ForestRoadZ(x)));
            if(x>=1150f && x<1660f) distance=Mathf.Min(distance,Mathf.Abs(z-50f));
            for(int row=-1;row<=1;row++)
                if(Mathf.Abs(x-1150f)<22f) distance=Mathf.Min(distance,Mathf.Abs(z-(50f+row*23f)));
            return distance;
        }
        public static float ForestDensity(float x,float z)
        {
            float radius=new Vector2((x-440f)/720f,(z-490f)/650f).magnitude;
            float density=1f-Smooth(.8f,1f,radius);
            float clearing=Vector2.Distance(new Vector2(x,z),new Vector2(350f,570f));
            return density*Smooth(30f,65f,clearing);
        }
        public static bool CanPlant(float x,float z) => RoadDistance(x,z)>9f && RiverDistance(x,z)>RiverWidth(z)+18f &&
            Vector2.Distance(new Vector2(x,z),new Vector2(1150f,50f))>80f;
        public static float Shape(float x,float z,float original)
        {
            float distance=RiverDistance(x,z), width=RiverWidth(z), water=WaterHeight(z);
            float bed=water-2.5f+2.5f*Smooth(0f,width,distance)+Smooth(width,width+12f,distance);
            float terrain=Mathf.Lerp(bed,original,Smooth(width+12f,width+100f,distance));
            // Bridge approaches meet its deck gradually; the channel beneath remains open.
            float approach=Smooth(16f,20f,distance)*(1f-Smooth(32f,115f,distance));
            approach*=1f-Smooth(5f,24f,Mathf.Abs(z-50f));
            return Mathf.Lerp(terrain,WaterHeight(50f)+2.2f,approach);
        }
        public static void Paint(float[,,] map,int x,int z,float wx,float wz)
        {
            float edge=Mathf.PerlinNoise(wx*.07f+200f,wz*.07f+200f)*.9f;
            float road=1f-Smooth(2.4f,5f+edge,RoadDistance(wx,wz));
            float square=1f-Smooth(10f,15f,Mathf.Max(Mathf.Abs(wx-1150f),Mathf.Abs(wz-50f)));
            road=Mathf.Max(road,square);
            float bank=1f-Smooth(RiverWidth(wz)+8f,RiverWidth(wz)+20f,RiverDistance(wx,wz));
            float forest=ForestDensity(wx,wz)*.16f;
            float dirt=Mathf.Max(road,Mathf.Max(bank*.7f,forest));
            for(int layer=0;layer<5;layer++) map[z,x,layer]*=1f-dirt;
            map[z,x,5]=dirt;
        }
        public static GameObject TreePrototype(GameObject source,float height)
        {
            string path="Assets/Art/World/FrierenMap/"+source.name+"_Forest.prefab";
            var tree=Object.Instantiate(source);
            var renderers=tree.GetComponentsInChildren<Renderer>(true);
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            tree.transform.localScale*=height/Mathf.Max(.01f,bounds.size.y);
            var prefab=PrefabUtility.SaveAsPrefabAsset(tree,path);
            Object.DestroyImmediate(tree); return prefab;
        }
        public static void PlantForest(TerrainData data,float ox,float oz)
        {
            var trees=new List<TreeInstance>(data.treeInstances);
            var random=new System.Random(412+(int)ox+(int)oz);
            for(float z=System.Math.Max(oz,-160f);z<System.Math.Min(oz+4000,1150f);z+=15f)
            for(float x=System.Math.Max(ox,-280f);x<System.Math.Min(ox+4000,1160f);x+=15f)
            {
                float wx=x+(float)random.NextDouble()*15f, wz=z+(float)random.NextDouble()*15f;
                float cluster=Mathf.Lerp(.5f,1f,Mathf.PerlinNoise(wx/75f+50f,wz/75f+50f));
                if(random.NextDouble()>ForestDensity(wx,wz)*cluster || !CanPlant(wx,wz)) continue;
                float nx=(wx-ox)/4000f,nz=(wz-oz)/4000f;
                if(data.GetSteepness(nx,nz)>32f)continue;
                trees.Add(new TreeInstance{position=new Vector3(nx,data.GetInterpolatedHeight(nx,nz)/data.size.y,nz),
                    prototypeIndex=random.Next(5)==0?0:1,widthScale=.85f+(float)random.NextDouble()*.4f,
                    heightScale=.8f+(float)random.NextDouble()*.5f,rotation=(float)random.NextDouble()*Mathf.PI*2f,
                    color=Color.Lerp(new Color(.8f,.92f,.74f),Color.white,(float)random.NextDouble()),lightmapColor=Color.white});
            }
            data.treeInstances=trees.ToArray();
        }
        public static void BuildWaterAndBridge(Transform root)
        {
            const int count=1101;
            var vertices=new Vector3[count*2]; var uv=new Vector2[count*2]; var triangles=new int[(count-1)*6];
            for(int i=0;i<count;i++)
            {
                float z=5000f-i*10f; float x=RiverX(z),y=WaterHeight(z),width=RiverWidth(z);
                vertices[i*2]=new Vector3(x-width,y,z);vertices[i*2+1]=new Vector3(x+width,y,z);
                uv[i*2]=new Vector2(0,i*1.5f);uv[i*2+1]=new Vector2(1,i*1.5f);
                if(i==count-1)continue;
                int v=i*2,t=i*6;
                triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;
                triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;
            }
            string path="Assets/Art/World/FrierenMap/Terrain/CarvedRiver.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.name="CarvedRiver";mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var water=new GameObject("Eldenbrook River - Downhill Channel");water.transform.SetParent(root,false);
            water.AddComponent<MeshFilter>().sharedMesh=mesh;
            water.AddComponent<MeshRenderer>().sharedMaterial=Material("Flowing River",Shader.Find("Frieren/World/River"),new Color(.08f,.38f,.43f));
            var bridge=new GameObject("East Footbridge").transform;bridge.SetParent(root,false);
            bridge.position=new Vector3(RiverX(50f),WaterHeight(50f)+2.2f,50f);
            var wood=Material("Bridge Timber",Shader.Find("Standard"),new Color(.32f,.22f,.12f));
            for(int i=-20;i<=20;i++) Box(bridge,"Deck Plank",new Vector3(i, -.18f,0),new Vector3(.98f,.35f,5.5f),wood);
            for(int side=-1;side<=1;side+=2)
            {
                Box(bridge,"Handrail",new Vector3(0,1.1f,side*2.6f),new Vector3(42,.16f,.16f),wood);
                for(int i=-20;i<=20;i+=5) Box(bridge,"Rail Post",new Vector3(i,.5f,side*2.6f),new Vector3(.22f,1.5f,.22f),wood);
            }
        }
        public static Material Material(string name,Shader shader,Color color)
        {
            if(shader==null)throw new System.InvalidOperationException("Missing shader for "+name);
            string path="Assets/Art/World/FrierenMap/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
            mat.shader=shader;mat.color=color;mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        private static void Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=pos;obj.transform.localScale=size;obj.layer=GameLayers.Ground;obj.GetComponent<Renderer>().sharedMaterial=mat;
        }
    }
}
