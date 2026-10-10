using System.Collections.Generic;
using UnityEngine;

namespace TheRealm
{
    // Original modular geometry. Materials are shared and become saved assets in the Editor builder.
    public static class RealmArt
    {
        public static readonly Color Gold = new Color(.79f,.59f,.28f);
        public static readonly Color[] DisciplineColors = { new Color(.43f,.56f,.64f), new Color(.42f,.32f,.58f), new Color(.35f,.55f,.33f), new Color(.24f,.59f,.76f), new Color(.85f,.71f,.40f), new Color(.72f,.38f,.23f) };
        static readonly Dictionary<string, Material> materials = new Dictionary<string,Material>();
        public static IEnumerable<KeyValuePair<string,Material>> Materials => materials;
        public static void ResetCache() => materials.Clear();
        public static void RegisterMaterial(string name,Material value) => materials[name]=value;
        public static Material Material(string name,Color color,float metal=0,bool glow=false)
        {
            if(materials.TryGetValue(name,out var existing) && existing!=null) return existing;
            var shader=Shader.Find("HDRP/Lit");
            var m=new Material(shader) {name=name,enableInstancing=true};
            m.SetColor("_BaseColor",color); m.SetFloat("_Metallic",metal); m.SetFloat("_Smoothness",metal>0?.6f:.22f);
            if(glow) { m.SetColor("_EmissiveColor",color*5); m.SetColor("_EmissiveColorLDR",color); }
            materials[name]=m; return m;
        }
        // Editor-supplied world kit (static meshes and tileable materials authored in ArtSource/World). Both are null at
        // runtime and in tests, in which case the original primitive construction is used.
        public static System.Func<string,GameObject> KitProvider;
        public static System.Func<string,Material> MaterialProvider;
        public static Material Textured(string name,string set,Vector2 tiling)
        {
            if(materials.TryGetValue(name,out var existing) && existing!=null) return existing;
            var source=MaterialProvider?.Invoke(set); if(source==null) return null;
            var m=new Material(source) {name=name}; m.SetTextureScale("_BaseColorMap",tiling); materials[name]=m; return m;
        }
        // Kit meshes are authored with a plain "W_Glow" placeholder material for anything meant to shine; swapping it to
        // the scene's actual light colour (fire, ward light, ...) keeps one mesh usable for every kind of glow.
        static void SwapGlow(GameObject obj,Material glow)
        {
            foreach(var r in obj.GetComponentsInChildren<Renderer>())
            {
                var shared=r.sharedMaterials; bool changed=false;
                for(int m=0;m<shared.Length;m++) if(shared[m]!=null && shared[m].name=="W_Glow") {shared[m]=glow;changed=true;}
                if(changed) r.sharedMaterials=shared;
            }
        }
        static GameObject Kit(Transform parent,string name,Vector3 position,float yaw,bool solid,float footprint=1f)
        {
            var obj=KitProvider?.Invoke(name); if(obj==null) return null;
            obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
            if(!solid) return obj;
            // Collider from the mesh bounds in the kit's own space; footprint trims roof overhangs so walls, not eaves, block.
            var bounds=new Bounds(); bool any=false;
            foreach(var filter in obj.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null) continue;
                var m=obj.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix; var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var corner=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(!any){bounds=new Bounds(corner,Vector3.zero);any=true;} else bounds.Encapsulate(corner);
                }
            }
            var box=obj.AddComponent<BoxCollider>(); box.center=bounds.center; box.size=new Vector3(bounds.size.x*footprint,bounds.size.y,bounds.size.z*footprint);
            obj.layer=8; return obj;
        }
        public static GameObject Part(Transform parent,string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material,bool collision=false)
        {
            var obj=GameObject.CreatePrimitive(shape); obj.name=name; obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            if(collision) obj.layer=8;
            else { var collider=obj.GetComponent<Collider>(); if(Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider); }
            return obj;
        }
        public static GameObject Adventurer(string name,Color color,bool enemy=false,bool boss=false)
        {
            var root=new GameObject(name);
            var cloth=Material("Cloth_"+ColorUtility.ToHtmlStringRGB(color),color);
            var iron=Material("Weathered iron",new Color(.25f,.29f,.31f),.7f);
            var skin=Material("Warm stone skin",new Color(.53f,.37f,.25f));
            var dark=Material("Dark leather",new Color(.10f,.085f,.075f));
            Part(root.transform,"Torso",PrimitiveType.Capsule,new Vector3(0,1.15f,0),new Vector3(.66f,.46f,.40f),cloth);
            Part(root.transform,"Belt",PrimitiveType.Cube,new Vector3(0,.92f,0),new Vector3(.67f,.12f,.42f),dark);
            Part(root.transform,"Head",PrimitiveType.Sphere,new Vector3(0,1.82f,0),Vector3.one*.36f,skin);
            Part(root.transform,"Hood",PrimitiveType.Sphere,new Vector3(0,1.9f,-.06f),new Vector3(.43f,.36f,.4f),cloth);
            Part(root.transform,"Face",PrimitiveType.Cube,new Vector3(0,1.82f,.17f),new Vector3(.24f,.17f,.07f),skin);
            Part(root.transform,"LeftArm",PrimitiveType.Capsule,new Vector3(-.43f,1.13f,0),new Vector3(.2f,.36f,.22f),cloth);
            Part(root.transform,"RightArm",PrimitiveType.Capsule,new Vector3(.43f,1.13f,0),new Vector3(.2f,.36f,.22f),cloth);
            Part(root.transform,"LeftLeg",PrimitiveType.Capsule,new Vector3(-.19f,.45f,0),new Vector3(.23f,.44f,.25f),dark);
            Part(root.transform,"RightLeg",PrimitiveType.Capsule,new Vector3(.19f,.45f,0),new Vector3(.23f,.44f,.25f),dark);
            Part(root.transform,"ShoulderL",PrimitiveType.Sphere,new Vector3(-.39f,1.45f,0),new Vector3(.32f,.2f,.4f),iron);
            Part(root.transform,"ShoulderR",PrimitiveType.Sphere,new Vector3(.39f,1.45f,0),new Vector3(.32f,.2f,.4f),iron);
            Part(root.transform,"Blade",PrimitiveType.Cube,new Vector3(.54f,.85f,.53f),new Vector3(.08f,.07f,1.1f),iron);
            Part(root.transform,"Guard",PrimitiveType.Cube,new Vector3(.54f,.85f,.12f),new Vector3(.32f,.10f,.08f),Material("Antique brass",Gold,.65f));
            if(enemy) Part(root.transform,"Eyes",PrimitiveType.Cube,new Vector3(0,1.86f,.22f),new Vector3(.25f,.04f,.03f),Material("Ember eyes",new Color(1,.25f,.06f),0,true));
            if(boss) { root.transform.localScale=Vector3.one*1.8f; for(int i=0;i<5;i++) Part(root.transform,"Crown tine",PrimitiveType.Cube,new Vector3((i-2)*.1f,2.12f,0),new Vector3(.05f,.3f,.06f),iron); }
            return root;
        }
        public static void BuildEnvironment(Transform parent)
        {
            var grass=Material("Moss and earth",new Color(.12f,.19f,.115f));
            var path=Material("Weathered trail",new Color(.28f,.25f,.18f));
            var stone=Material("Ancient limestone",new Color(.29f,.32f,.30f));
            var bark=Material("Old bark",new Color(.17f,.12f,.085f));
            var leaves=Material("Forest canopy",new Color(.12f,.27f,.17f));
            var plaster=Material("Warm plaster",new Color(.55f,.45f,.31f));
            var roof=Material("Slate roof",new Color(.17f,.23f,.26f));
            var glow=Material("Wardlight",new Color(.22f,.83f,.72f),0,true);
            Part(parent,"The frontier",PrimitiveType.Cube,new Vector3(0,-.6f,213),new Vector3(100,1,490),Textured("Frontier grass","W_Grass",new Vector2(25,122.5f))??grass,true);
            Part(parent,"The old road",PrimitiveType.Cube,new Vector3(0,-.065f,208),new Vector3(7,.08f,451),Textured("Old road earth","W_Earth",new Vector2(1.75f,112.75f))??path);
            bool village=BuildVillage(parent);
            var rng=new System.Random(7321);
            for(int i=0;i<230;i++)
            {
                float z=(float)rng.NextDouble()*450-15;
                float x=(i%2==0?-1:1)*(13+(float)rng.NextDouble()*29);
                if(z<20 && Mathf.Abs(x)<25) continue;
                if(z<150)
                {
                    // Original forest kit: oak, fir and birch, with a thin trunk-only collider so the canopy doesn't block movement.
                    var kind=new[]{"Oak","Fir","Birch"}[rng.Next(3)];
                    var tree=Kit(parent,kind,new Vector3(x,0,z),(float)rng.NextDouble()*360,true,.14f);
                    if(tree==null)
                    {
                        float h=5+(float)rng.NextDouble()*7;
                        Part(parent,"Ancient trunk",PrimitiveType.Cylinder,new Vector3(x,h*.5f,z),new Vector3(.6f,h*.5f,.6f),bark,true);
                        Part(parent,"Canopy",PrimitiveType.Sphere,new Vector3(x,h,z),new Vector3(5.5f,3.5f,5.5f),leaves);
                        Part(parent,"Canopy",PrimitiveType.Sphere,new Vector3(x+1,h-1.5f,z),new Vector3(4.5f,3,4.5f),leaves);
                    }
                    else if(rng.NextDouble()<.4)
                    {
                        var undergrowth=new[]{"Bush_0","Bush_1","Bush_2","FernClump_0","FernClump_1","FernClump_2","GrassClump_0","GrassClump_1","GrassClump_2","Stump_0","Stump_1","Log_0"};
                        float ox=x+((float)rng.NextDouble()*2-1)*2.6f, oz=z+((float)rng.NextDouble()*2-1)*2.6f;
                        Kit(parent,undergrowth[rng.Next(undergrowth.Length)],new Vector3(ox,0,oz),(float)rng.NextDouble()*360,false);
                    }
                }
                else
                {
                    // Keep the original random sequence stable so later forest placements do not move between art passes.
                    float width=3+(float)rng.NextDouble()*5, height=4+(float)rng.NextDouble()*9;
                    int yaw=rng.Next(180), lean=rng.Next(-14,15);
                    float side=x<0?-1:1;
                    var cliff=Kit(parent,new[]{"Cliff_A","Cliff_B","Cliff_C"}[i%3],new Vector3(side*Mathf.Max(28,Mathf.Abs(x)),0,z),yaw,true,.82f);
                    if(cliff==null)
                    {
                        var rock=Part(parent,"Broken escarpment",PrimitiveType.Cube,new Vector3(x,2,z),new Vector3(width,height,5),stone,true);
                        rock.transform.localRotation=Quaternion.Euler(0,yaw,lean);
                    }
                    else if(i%4==0) Kit(parent,"ScreeCluster",new Vector3(side*21,0,z+3),yaw,false);
                }
            }
            BuildBorderlandRuins(parent);
            for(int i=0;i<7 && !village;i++)
            {
                float x=i%2==0?-13:13, z=-8+(i/2)*8;
                Part(parent,"Emberhearth cottage",PrimitiveType.Cube,new Vector3(x,2,z),new Vector3(7,4,6),plaster,true);
                var r=Part(parent,"Sloping slate",PrimitiveType.Cube,new Vector3(x-1.8f,4.4f,z),new Vector3(4.3f,.3f,7),roof); r.transform.localRotation=Quaternion.Euler(0,0,30);
                r=Part(parent,"Sloping slate",PrimitiveType.Cube,new Vector3(x+1.8f,4.4f,z),new Vector3(4.3f,.3f,7),roof); r.transform.localRotation=Quaternion.Euler(0,0,-30);
                Part(parent,"Timber door",PrimitiveType.Cube,new Vector3(x,1.2f,z+3.03f),new Vector3(1.3f,2.4f,.12f),bark);
                Part(parent,"Window glow",PrimitiveType.Cube,new Vector3(x+2,2.2f,z+3.06f),new Vector3(1,1.1f,.08f),Material("Hearth windows",new Color(1,.55f,.18f),0,true));
            }
            for(int region=0;region<3;region++)
            {
                float z=115+region*149;
                var daisPos=new Vector3(0,0,z+6);
                var dais=Kit(parent,"SealDais",daisPos,0,false);
                if(dais!=null)
                {
                    var detail=Kit(parent,"SealDais_Detail",daisPos,0,false);
                    if(detail!=null) SwapGlow(detail,glow);
                }
                else Part(parent,"Seal dais",PrimitiveType.Cylinder,daisPos+Vector3.up*.12f,new Vector3(16,.12f,16),stone);
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4; Vector3 p=new Vector3(Mathf.Sin(angle)*10,0,z+6+Mathf.Cos(angle)*10);
                    // The rune channel is carved into the pillar's local +Z face; turn it to face the dais at the ring's centre.
                    var pillar=Kit(parent,"SealPillar",p,angle*Mathf.Rad2Deg+180f,true);
                    if(pillar!=null) SwapGlow(pillar,glow);
                    else
                    {
                        Part(parent,"Seal pillar",PrimitiveType.Cylinder,p+Vector3.up*3,new Vector3(1,3,1),stone,true);
                        Part(parent,"Luminous inscription",PrimitiveType.Cube,p+new Vector3(0,3,-.52f),new Vector3(.16f,2,.08f),glow);
                    }
                }
                var gatePos=new Vector3(0,0,28+region*149);
                var gate=Kit(parent,"StoneGate",gatePos,0,false);
                if(gate!=null)
                {
                    // One mesh for the whole gate, but the two side pillars must stay solid while the passage between them
                    // stays open: a single bounds-based collider would seal the gap, so place two matching the authored posts.
                    foreach(float sx in new[]{-1f,1f})
                    {
                        var box=gate.AddComponent<BoxCollider>(); box.center=new Vector3(sx*5f,3f,0); box.size=new Vector3(2.2f,6.2f,2f);
                    }
                    gate.layer=8; SwapGlow(gate,glow);
                }
                else Arch(parent,gatePos,stone);
            }
            for(int i=0;i<32;i++)
            {
                float z=i*14;
                if(village && z<19) continue;
                var post=Kit(parent,"LanternPost",new Vector3(4.7f,0,z),0,false);
                if(post!=null) { SwapGlow(post,glow); continue; }
                Part(parent,"Trail lantern post",PrimitiveType.Cylinder,new Vector3(4.7f,1.2f,z),new Vector3(.12f,1.2f,.12f),bark);
                Part(parent,"Trail lantern",PrimitiveType.Cube,new Vector3(4.7f,2.5f,z),new Vector3(.32f,.5f,.32f),glow);
            }
            if(!village)
            {
                Part(parent,"Hearth ring",PrimitiveType.Cylinder,new Vector3(0,.1f,4),new Vector3(3,.1f,3),stone);
                Part(parent,"Hearth flame",PrimitiveType.Sphere,new Vector3(0,.6f,4),new Vector3(.8f,1.2f,.8f),Material("Living ember",new Color(1,.32f,.07f),0,true));
            }
            var npc=Adventurer("Warden Elowen",new Color(.40f,.53f,.49f)); npc.transform.SetParent(parent,false); npc.transform.localPosition=new Vector3(5,0,9); npc.transform.localRotation=Quaternion.Euler(0,220,0);
            RealmWorldPolish.Build(parent);
        }
        // Emberhearth built from the world kit. Returns false when no kit is available so the primitive village is used.
        // Keeps the haven gameplay layout: open street |x|<3.5, Warden Elowen at (5,9), spawn at (0,3); the hearth sits just north of spawn.
        static bool BuildVillage(Transform parent)
        {
            var probe=KitProvider?.Invoke("Cottage_A");
            if(probe==null) return false;
            Object.DestroyImmediate(probe);
            var street=Textured("Emberhearth cobbles","W_Cobblestone",new Vector2(1.8f,8));
            if(street!=null) Part(parent,"Emberhearth street",PrimitiveType.Cube,new Vector3(0,-.05f,3),new Vector3(7.2f,.08f,32),street);
            // Cottages face the street: kit doors face local +Z.
            var cottages=new (string kit,float x,float z)[] {("Cottage_A",-14,-9),("Cottage_B",-14,2.5f),("Cottage_C",-14,14),("Cottage_C",14,-9),("Cottage_A",14,2.5f),("Cottage_B",14,14)};
            foreach(var c in cottages) Kit(parent,c.kit,new Vector3(c.x,0,c.z),c.x<0?90:-90,true,.86f);
            Kit(parent,"Hearth",new Vector3(0,0,8),0,false);   // clear of the spawn point at (0,3)
            var fire=new GameObject("Hearth fire light").AddComponent<Light>(); fire.transform.SetParent(parent,false); fire.transform.localPosition=new Vector3(0,1.1f,8);
            fire.type=LightType.Point; fire.color=new Color(1,.55f,.25f); fire.range=14; fire.intensity=2500;
            Kit(parent,"Well",new Vector3(-6.5f,0,-4),15,true);
            Kit(parent,"Cart",new Vector3(7,0,-3),20,true);
            Kit(parent,"Stores",new Vector3(8.5f,0,-10),0,true);
            Kit(parent,"Stores",new Vector3(-8.5f,0,8),160,true);
            Kit(parent,"Woodpile",new Vector3(-9,0,-11),90,false);
            Kit(parent,"Woodpile",new Vector3(9.5f,0,11),-90,false);
            Kit(parent,"NoticeBoard",new Vector3(-5,0,16.5f),90,true);
            Kit(parent,"Bench",new Vector3(-5.5f,0,1),90,false);
            Kit(parent,"Bench",new Vector3(5.5f,0,14),-90,false);
            // Street lanterns alternate sides; kit lantern arms point along local -X.
            foreach(var (lx,lz) in new[]{(3.9f,-10f),(-3.9f,-2f),(3.9f,6f),(-3.9f,14f)}) Kit(parent,"LanternPost",new Vector3(lx,0,lz),lx>0?0:180,false);
            // Fence line around the sides and back of the haven; the street and road stay open.
            for(float z=-11.5f;z<=17;z+=3) { Kit(parent,"Fence",new Vector3(-19,0,z),90,true); Kit(parent,"Fence",new Vector3(19,0,z),90,true); }
            for(float x=-17.5f;x<=18;x+=3) if(Mathf.Abs(x)>5) Kit(parent,"Fence",new Vector3(x,0,-12.8f),0,true);
            return true;
        }
        static void BuildBorderlandRuins(Transform parent)
        {
            // Off-road clusters leave the seven-metre road, authored quest sites and seal rings clear.
            for(int i=0;i<8;i++)
            {
                float z=new[]{164f,201f,230f,290f,320f,350f,380f,437f}[i];
                float side=i%2==0?-1:1;
                var group=new GameObject(i<4?"Borderland ruins":"Sunless ruins");
                group.transform.SetParent(parent,false);
                var wall=Kit(group.transform,i%2==0?"RuinWall_A":"RuinWall_B",new Vector3(side*20,0,z),side*18,false);
                AddStoneColliders(wall);
                var arch=Kit(group.transform,"RuinArch",new Vector3(side*20,0,z+7),0,false);
                AddStoneColliders(arch);
                AddStoneColliders(Kit(group.transform,"RuinPillar",new Vector3(side*15.5f,0,z+4),i*37,false));
                Kit(group.transform,"RuinRubble",new Vector3(side*18,0,z-3),i*53,false);
                // Decorative stairs terminate outside the traversable field; player movement is currently ground-plane based.
                AddStoneColliders(Kit(group.transform,"RuinStairs",new Vector3(side*32,0,z+2),side*90,false));
                Kit(group.transform,"RockShelf",new Vector3(side*33,0,z+6),i*41,true,.85f);
            }
        }
        static void AddStoneColliders(GameObject obj)
        {
            if(obj==null)return;
            // Mesh-shaped static collision preserves arch openings and broken wall silhouettes.
            foreach(var filter in obj.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null)continue;
                filter.gameObject.layer=8;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            }
        }
        static void Arch(Transform parent,Vector3 position,Material stone)
        {
            Part(parent,"Gate left",PrimitiveType.Cube,position+new Vector3(-5,3,0),new Vector3(2,6,2),stone,true);
            Part(parent,"Gate right",PrimitiveType.Cube,position+new Vector3(5,3,0),new Vector3(2,6,2),stone,true);
            Part(parent,"Gate lintel",PrimitiveType.Cube,position+new Vector3(0,6,0),new Vector3(12,1.2f,2),stone);
        }
    }
}
