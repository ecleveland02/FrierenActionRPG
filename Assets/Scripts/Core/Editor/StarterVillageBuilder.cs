using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    public static class StarterVillageBuilder
    {
        private const string Folder = "Assets/Art/World/FrierenMap/Materials/";
        public static void Build(Transform world, float height)
        {
            var town = new GameObject("Eldenbrook - Starter Village").transform;
            town.SetParent(world); town.position = new Vector3(1150f, height, 50f);
            var plaster = Mat("Village Cream Plaster", new Color(.78f,.70f,.51f));
            var timber = Mat("Village Dark Timber", new Color(.20f,.12f,.07f));
            var roof = Mat("Village Slate Roof", new Color(.20f,.28f,.34f));
            var terracotta = Mat("Village Clay Roof", new Color(.43f,.19f,.12f));
            var stone = Mat("Village Stone", new Color(.43f,.44f,.39f));
            var path = Mat("Village Square Earth", new Color(.50f,.39f,.24f));
            var glass = Mat("Village Windows", new Color(.22f,.39f,.43f));
            for(int x=3;x<=9;x++) for(int z=-3;z<=3;z++)
                if(Vector2.Distance(new Vector2(x,z),new Vector2(6,0))>1.6f)
                    Box(town,"Well Paving",new Vector3(x,.035f,z),new Vector3(.94f,.07f,.94f),stone,false);
            for(int i=0;i<6;i++)
            {
                bool left=i<3; float z=(i%3-1)*23f;
                var house=new GameObject(i==0 ? "Wayside Inn" : "Cottage "+i).transform;
                house.SetParent(town,false); house.localPosition=new Vector3(left ? -25:25,0,z);
                house.localRotation=Quaternion.Euler(0,left ? 90:-90,0);
                Box(house,"Stone Foundation",new Vector3(0,.25f,0),new Vector3(11,.5f,9),stone);
                Box(house,"Plaster Walls",new Vector3(0,2.7f,0),new Vector3(10,5,8),plaster);
                for(int p=-1;p<=1;p++) Box(house,"Timber Post",new Vector3(p*4.9f,2.8f,4.06f),new Vector3(.25f,5.3f,.25f),timber,false);
                Box(house,"Cross Beam",new Vector3(0,4.9f,4.08f),new Vector3(10,.25f,.25f),timber,false);
                Box(house,"Closed Door",new Vector3(0,1.5f,4.08f),new Vector3(1.6f,2.5f,.18f),timber,false);
                foreach(float x in new[]{-3f,3f})
                {
                    Box(house,"Window Frame",new Vector3(x,2.7f,4.12f),new Vector3(1.6f,1.6f,.18f),timber,false);
                    Box(house,"Window",new Vector3(x,2.7f,4.23f),new Vector3(1.3f,1.3f,.05f),glass,false);
                }
                for(int side=-1;side<=1;side+=2)
                {
                    var panel=Box(house,"Pitched Roof",new Vector3(side*2.7f,6.5f,0),new Vector3(6.5f,.3f,10),i%2==0 ? roof:terracotta);
                    panel.transform.localRotation=Quaternion.Euler(0,0,-side*28f);
                }
                Box(house,"Chimney",new Vector3(3,6.3f,-2),new Vector3(.9f,4,.9f),stone);
            }
            // Open well: short stone sides leave a visible hollow center.
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6f;
                var block=Box(town,"Well Stone",new Vector3(6+Mathf.Sin(a)*1.4f,.6f,Mathf.Cos(a)*1.4f),new Vector3(.75f,1.2f,.45f),stone);
                block.transform.localRotation=Quaternion.Euler(0,i*30,0);
            }
            foreach(float x in new[]{4f,8f}) Box(town,"Well Post",new Vector3(x,1.8f,0),new Vector3(.22f,3.6f,.22f),timber);
            Box(town,"Well Canopy",new Vector3(6,3.6f,0),new Vector3(5,.25f,3.5f),roof);
            Prop(town,"SM_Bench",new Vector3(-7,0,8),180);
            Prop(town,"SM_Bench",new Vector3(-7,0,-8),0);
            Prop(town,"SM_Barrel",new Vector3(-17,0,-28),0);
            Prop(town,"SM_Barrel",new Vector3(17,0,28),0);
            var spawn=world.Find("PlayerSpawn - Central Meadow");
            if(spawn!=null) spawn.position=town.position+new Vector3(0,.15f,-9);
        }

        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collision=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position; obj.transform.localScale=size; obj.layer=GameLayers.Ground;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision) Object.DestroyImmediate(obj.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(obj,StaticEditorFlags.BatchingStatic); return obj;
        }
        private static Material Mat(string name,Color color)
        {
            string path=Folder+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat,path);}
            mat.color=color; mat.SetFloat("_Glossiness",.08f); mat.enableInstancing=true; EditorUtility.SetDirty(mat); return mat;
        }
        private static void Prop(Transform parent,string name,Vector3 position,float yaw)
        {
            string path="Assets/Prefabs/Stylized_Labs/Stylized_Fantasy/Props_Sample/Prefabs/"+name+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null) throw new FileNotFoundException(path);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(obj,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
        }
    }
}
