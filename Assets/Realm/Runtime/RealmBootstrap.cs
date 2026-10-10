using UnityEngine;
using UnityEngine.InputSystem;

namespace TheRealm
{
    public sealed class RealmBootstrap : MonoBehaviour
    {
        [SerializeField] RealmCampaign campaign;
        [SerializeField] InputActionAsset inputActions;
        public void Configure(RealmCampaign content,InputActionAsset input){campaign=content;inputActions=input;}
        void Awake()
        {
            Application.targetFrameRate=60;
            Application.runInBackground=true;
            var session=gameObject.AddComponent<RealmSession>();session.Initialize(campaign);
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--realm-smoke")>=0)
            {RealmNetworkSmoke.Attach(gameObject,session);return;}
            var presentation=gameObject.AddComponent<RealmPresentation>();presentation.Initialize(session,inputActions);
            gameObject.AddComponent<RealmInterface>().Initialize(session,presentation);
            gameObject.AddComponent<RealmAudio>().Initialize(session);
            RealmNetworkSmoke.Attach(gameObject,session);
        }
    }
}
