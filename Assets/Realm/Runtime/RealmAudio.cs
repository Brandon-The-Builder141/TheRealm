using UnityEngine;

namespace TheRealm
{
    public sealed class RealmAudio : MonoBehaviour
    {
        RealmSession session;
        AudioSource ambience, effects;
        AudioClip wind, strike, hurt, dice;
        string lastRoll;
        float lastHealth=-1;
        public void Initialize(RealmSession value)
        {
            session=value;
            ambience=gameObject.AddComponent<AudioSource>();effects=gameObject.AddComponent<AudioSource>();
            ambience.spatialBlend=effects.spatialBlend=0;ambience.volume=.11f;effects.volume=.22f;
            wind=Make("Original frontier wind",12,0);
            strike=Make("Original blade impact",.16f,1);hurt=Make("Original injury cue",.3f,2);dice=Make("Original fate dice",.5f,3);
            ambience.clip=wind;ambience.loop=true;ambience.Play();
        }
        static AudioClip Make(string name,float seconds,int kind)
        {
            const int rate=22050;var data=new float[Mathf.RoundToInt(seconds*rate)];var rng=new System.Random(891+kind);float filtered=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate;float noise=(float)rng.NextDouble()*2-1;filtered=Mathf.Lerp(filtered,noise,.025f);
                if(kind==0)
                {
                    float loopFade=Mathf.Min(1,Mathf.Min(t,seconds-t)*2);
                    data[i]=(filtered*.8f+Mathf.Sin(t*2*Mathf.PI*110)*.05f+Mathf.Sin(t*2*Mathf.PI*165)*.025f)*loopFade;
                }
                else if(kind==1)data[i]=(noise*.5f+Mathf.Sin(t*1100)*.3f)*Mathf.Exp(-t*35);
                else if(kind==2)data[i]=Mathf.Sin(t*(520-t*500))*.7f*Mathf.Exp(-t*13);
                else data[i]=noise*.55f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*95)),14)*Mathf.Exp(-t*6);
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void Update()
        {
            if(session?.World==null)return;
            if(session.World.lastRoll!=lastRoll)
            {
                if(lastRoll!=null)effects.PlayOneShot(session.World.lastRoll.Contains(" vs ")?dice:strike);
                lastRoll=session.World.lastRoll;
            }
            var c=session.LocalCharacter;
            if(c!=null){if(lastHealth>=0 && c.health<lastHealth)effects.PlayOneShot(hurt);lastHealth=c.health;}
        }
        void OnDestroy(){if(wind!=null)Destroy(wind);if(strike!=null)Destroy(strike);if(hurt!=null)Destroy(hurt);if(dice!=null)Destroy(dice);}
    }
}
