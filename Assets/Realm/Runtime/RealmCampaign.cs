using System;
using UnityEngine;

namespace TheRealm
{
    [Serializable]
    public sealed class QuestDefinition
    {
        public string id, title, narrator, prompt, optionA, optionB, success, failure;
        public int region, difficulty;
        // Faction movement for choosing optionA. Choosing optionB applies the opposite.
        public int settlementShift, wildernessShift;
        public Discipline skill;
        public Vector3 location;
    }
    [CreateAssetMenu(menuName = "The Realm/Campaign")]
    public sealed class RealmCampaign : ScriptableObject
    {
        public QuestDefinition[] quests;
        public static QuestDefinition[] CreateDefaultQuests()
        {
            return new[]
            {
                Q("embers", "A debt of embers",0, new Vector3(5,0,9), Discipline.Spirit,10,"Warden Elowen","The forest bells rang without wind. Our missing scouts carried the last wardstone. Bring it home—or learn why they took it.","Promise to recover the stone","Demand payment first"),
                Q("tracks", "Footsteps beneath the fern",0,new Vector3(-10,0,37),Discipline.Wilderness,11,"A broken trail","Bootprints turn toward the old grove. A second trail is bloodied, but a warning has been carved into the bark.","Follow the warning carefully","Take the direct trail",0,1),
                Q("oath", "The deserter's oath",0,new Vector3(13,0,65),Discipline.Spirit,12,"Scout Mara","We took the stone because the roots were dying. Elowen knew. I will show you the grove if you leave my people out of this.","Protect the deserters","Expose them to the settlement",-1,1),
                Q("grove", "The listening grove",0,new Vector3(-9,0,91),Discipline.Elements,13,"The grove remembers","A luminous root curls around the stone. It beats with a heartbeat that is not your own. Taking it may save the village and silence the grove.","Share the stone's strength","Claim it for the settlement",-1,1),
                Q("warden", "Beneath the hollow crown",0,new Vector3(0,0,122),Discipline.Steel,14,"The Rootbound Warden","The guardian falls silent. Its crown contains a map to the borderlands, and a warning: three seals keep the Sleeper dreaming.","Restore the first seal","Take its power",1,1),
                Q("return", "What the fire remembers",0,new Vector3(5,0,9),Discipline.Artifice,11,"Warden Elowen","You brought back more than a stone. Tell us what happened in the grove. The people will remember what you say tonight.","Tell the whole truth","Keep the grove's secret",1,-1),
                Q("bridge", "The broken crossing",1,new Vector3(-7,0,156),Discipline.Artifice,13,"A ruined crossing","A caravan is trapped beyond the bridge. Repairing the span exposes you to the watchtower; slipping through abandons them.","Repair the crossing","Take the concealed path"),
                Q("tithe", "The iron tithe",1,new Vector3(12,0,186),Discipline.Shadow,14,"Captain Vey","The border belongs to whoever can hold it. Give me the seal-map and my soldiers will let your village stand.","Negotiate a false bargain","Refuse the captain"),
                Q("prison", "Names in the quarry",1,new Vector3(-12,0,212),Discipline.Shadow,14,"The imprisoned mason","My family built the second seal. The captain made us break it. Open these locks and I can mend what remains.","Free the prisoners quietly","Challenge their captors",1,1),
                Q("forge", "The silent forge",1,new Vector3(11,0,241),Discipline.Artifice,15,"The last furnace","The forge can strengthen your settlement's wards or restore the border's starving fields. There is fuel for only one.","Restore the fields","Strengthen the village wards",-1,1),
                Q("marshal", "The Ash Marshal",1,new Vector3(0,0,271),Discipline.Steel,16,"A crown of cinders","The marshal's banner burns. Beneath it, the second seal is cracked open. Beyond lies a stair into a forgotten kingdom.","Mend the second seal","Break it and press onward",1,1),
                Q("council", "The cost of shelter",1,new Vector3(5,0,9),Discipline.Spirit,13,"The settlement council","Refugees crowd the gates. Welcoming them will strain our stores; turning them away leaves them to the waking realm.","Open the gates","Keep the gates guarded"),
                Q("stair", "The sunless stair",2,new Vector3(-8,0,304),Discipline.Wilderness,15,"An ancient inscription","We buried our king with every memory of his cruelty. The stair remembers each trespass. Speak a truth to pass.","Offer an honest confession","Trick the listening stone",0,1),
                Q("archive", "A kingdom unwritten",2,new Vector3(12,0,333),Discipline.Elements,16,"The glass archivist","The Sleeper was a guardian before he was a king. Our fear made him a prison. Will you learn what we erased?","Preserve the forbidden history","Destroy the dangerous records",-1,1),
                Q("choir", "The voiceless choir",2,new Vector3(-12,0,360),Discipline.Spirit,16,"The bound choir","The voices sustain the last seal. Freeing them weakens the prison. Leaving them binds another age of suffering.","Release the bound voices","Maintain the final seal",-1,1),
                Q("mirror", "The mercy of mirrors",2,new Vector3(9,0,386),Discipline.Shadow,17,"Your reflection","The mirror offers a road home. It promises safety if you leave the next traveler to bear your burden.","Reject the bargain","Accept the costly shortcut",1,1),
                Q("sleeper", "The king beneath the roots",2,new Vector3(0,0,420),Discipline.Elements,18,"The Sleeper","The ancient king kneels. His power could shelter your people, but every shelter casts a shadow. Decide what world will follow.","Release the realm from its king","Bind the king to guard the frontier",-1,1),
                Q("dawn", "A dawn you have earned",2,new Vector3(5,0,9),Discipline.Spirit,12,"The people of the frontier","Names of the dead surround the hearth. The world has changed. Those who survived must choose what to build upon it.","Build a refuge for all","Found a vigilant stronghold",1,1)
            };
        }
        static QuestDefinition Q(string id,string title,int region,Vector3 location,Discipline skill,int dc,string narrator,string prompt,string a,string b,int settlement=1,int wilderness=0)
        {
            return new QuestDefinition { id=id,title=title,region=region,location=location,skill=skill,difficulty=dc,narrator=narrator,prompt=prompt,optionA=a,optionB=b,settlementShift=settlement,wildernessShift=wilderness,success="Your insight earns trust and a stronger reward.",failure="Your words fail to convince. The choice remains, but its cost rises." };
        }
    }
}
