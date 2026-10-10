using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TheRealm
{
    public sealed class RealmSession : MonoBehaviour
    {
        public WorldState World { get; private set; }
        public RealmSimulation Simulation { get; private set; }
        public QuestDefinition[] Quests { get; private set; }
        public string Owner { get; private set; }
        public string Status { get; private set; } = "Choose a world to begin.";
        public bool Active => network != null && network.IsConnectedClient;
        public bool IsHost => network != null && network.IsHost;
        public CharacterState LocalCharacter => World?.characters.FindLast(c => c.owner == Owner && c.life != LifeState.Dead);
        public NetworkManager Network => network;
        NetworkManager network;
        UnityTransport transport;
        RealmSaveStore saves;
        readonly Dictionary<ulong,string> owners = new Dictionary<ulong,string>();
        readonly Dictionary<ulong, double> commandWindows = new Dictionary<ulong,double>();
        readonly Dictionary<ulong,int> commandCounts = new Dictionary<ulong,int>();
        bool registered;
        string saveDirectory;
        float snapshotAt, saveAt, step;
        public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;

        public void Initialize(RealmCampaign campaign)
        {
            Quests = campaign != null ? campaign.quests : RealmCampaign.CreateDefaultQuests();
            Owner = PlayerPrefs.GetString("Realm.Owner", "");
            string[] args = Environment.GetCommandLineArgs();
            for (int i=0;i<args.Length-1;i++) if(args[i]=="--realm-profile") Owner = args[i+1];
            saveDirectory=Path.Combine(Application.persistentDataPath,"TheRealm");
            for (int i=0;i<args.Length-1;i++) if(args[i]=="--realm-save-dir") saveDirectory=Path.GetFullPath(args[i+1]);
            if (string.IsNullOrWhiteSpace(Owner)) { Owner = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("Realm.Owner",Owner); PlayerPrefs.Save(); }
            network = gameObject.AddComponent<NetworkManager>();
            transport = gameObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=true,TickRate=30 };
            network.ConnectionApprovalCallback += Approve;
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += TransportFailed;
        }
        public void Host()
        {
            if (network.IsListening) return;
            try
            {
                saves = new RealmSaveStore(saveDirectory);
                World = saves.Load(); Simulation = new RealmSimulation(World,Quests);
                Simulation.Changed += Persist;
                Simulation.ResolveMovement = ResolveMovement;
                Simulation.PopulateRegion(World.chapter);
                owners[NetworkManager.ServerClientId] = Owner;
                transport.SetConnectionData("127.0.0.1",7777,"0.0.0.0");
                network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(Owner);
                if (!network.StartHost()) { Status="Could not host. Port 7777 may already be in use."; return; }
                Simulation.Connect(Owner,Now); Register(); Status="Hosting • UDP 7777 • up to four players";
                Persist();
            }
            catch(Exception e) { Status="Cannot open world: " + e.Message; Debug.LogException(e); }
        }
        public void UseDevelopmentSaveDirectory(string directory)
        {
            if(network.IsListening)throw new InvalidOperationException("Close the session before changing its save location.");
            saveDirectory=Path.GetFullPath(directory);
        }
        public void Join(string address)
        {
            if (network.IsListening) return;
            try
            {
                transport.SetConnectionData(address.Trim(),7777);
                network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(Owner);
                Status="Connecting to " + address + "…";
                if (!network.StartClient()) Status="Connection could not start.";
            }
            catch(Exception e) { Status=e.Message; }
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            string owner=Encoding.UTF8.GetString(request.Payload);
            bool valid=owner.Length>=8 && owner.Length<=64 && network.ConnectedClientsIds.Count<4;
            if(owners.ContainsValue(owner) && (!owners.TryGetValue(request.ClientNetworkId,out var existing) || existing!=owner)) valid=false;
            response.Approved=valid; response.CreatePlayerObject=false; response.Pending=false;
            response.Reason=valid ? "" : "World full or this player identity is already connected.";
            if(valid) owners[request.ClientNetworkId]=owner;
        }
        void Connected(ulong client)
        {
            Register();
            if(IsHost && owners.TryGetValue(client,out var owner)) Simulation.Connect(owner,Now);
            Status=IsHost ? "Hosting • " + network.ConnectedClientsIds.Count + "/4 players" : "Connected to the frontier";
        }
        void Disconnected(ulong client)
        {
            if(IsHost && owners.TryGetValue(client,out var owner)) { Simulation.Disconnect(owner,Now); owners.Remove(client); }
            if(client==network.LocalClientId) { registered=false; Status="Disconnected. " + network.DisconnectReason; }
        }
        void TransportFailed() { Status="Network connection failed. Check the host address and UDP port 7777."; }
        void Register()
        {
            if(registered || network.CustomMessagingManager==null) return;
            network.CustomMessagingManager.RegisterNamedMessageHandler("realm.command",ReceiveCommand);
            network.CustomMessagingManager.RegisterNamedMessageHandler("realm.world",ReceiveWorld);
            registered=true;
        }
        public void Send(PlayerCommand command)
        {
            if(!Active) return;
            if(IsHost) Simulation.Command(Owner,command,Now);
            else SendJson("realm.command",NetworkManager.ServerClientId,JsonUtility.ToJson(command),command.kind=="move" ? NetworkDelivery.UnreliableSequenced : NetworkDelivery.ReliableSequenced);
        }
        void ReceiveCommand(ulong sender,FastBufferReader reader)
        {
            if(!IsHost || !owners.TryGetValue(sender,out var owner)) return;
            double now=Now;
            if(!commandWindows.ContainsKey(sender) || now-commandWindows[sender]>1) { commandWindows[sender]=now; commandCounts[sender]=0; }
            if(++commandCounts[sender]>60 || reader.Length>2048) return;
            try { reader.ReadValueSafe(out string json); Simulation.Command(owner,JsonUtility.FromJson<PlayerCommand>(json),now); }
            catch(Exception e) { Debug.LogWarning("Rejected malformed player command: " + e.Message); }
        }
        void ReceiveWorld(ulong sender,FastBufferReader reader)
        {
            if(IsHost || sender!=NetworkManager.ServerClientId) return;
            try { reader.ReadValueSafe(out string json); World=JsonUtility.FromJson<WorldState>(json); }
            catch(Exception e) { Status="World update failed: " + e.Message; }
        }
        void SendJson(string name,ulong target,string json,NetworkDelivery delivery)
        {
            using(var writer=new FastBufferWriter(json.Length*4+32,Allocator.Temp))
            { writer.WriteValueSafe(json); network.CustomMessagingManager.SendNamedMessage(name,target,writer,delivery); }
        }
        void Update()
        {
            if(network==null || !network.IsListening) return;
            Register();
            if(!IsHost || Simulation==null) return;
            step+=Time.unscaledDeltaTime;
            int catchup=0;
            while(step>=.033333f && catchup++<5) { Simulation.Tick(.033333f,Now); step-=.033333f; }
            if(catchup>=5) step=0;
            if(Time.unscaledTime>=snapshotAt)
            {
                snapshotAt=Time.unscaledTime+.15f;
                string json=JsonUtility.ToJson(World);
                foreach(ulong id in network.ConnectedClientsIds) if(id!=NetworkManager.ServerClientId) SendJson("realm.world",id,json,NetworkDelivery.ReliableFragmentedSequenced);
            }
            if(Time.unscaledTime>=saveAt) { saveAt=Time.unscaledTime+5; Persist(); }
        }
        static Vector3 ResolveMovement(Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;
            if(delta.sqrMagnitude<.00001f) return from;
            if(Physics.SphereCast(from+Vector3.up*.85f,.3f,delta.normalized,out var hit,delta.magnitude+.05f,1<<8,QueryTriggerInteraction.Ignore))
                to=from+Vector3.ProjectOnPlane(delta,hit.normal);
            to.y=0; return to;
        }
        public void Persist()
        {
            if(saves==null || World==null || !string.IsNullOrEmpty(World.fatalError)) return;
            try { saves.Commit(World); }
            catch(Exception e) { World.fatalError="World saving failed. Simulation paused to protect your characters. " + e.Message; Status=World.fatalError; Debug.LogException(e); }
        }
        public void Leave()
        {
            if(IsHost) Persist();
            network.Shutdown(); registered=false; World=null; Simulation=null; owners.Clear();
            Status="World closed. Your story is saved.";
        }
        void OnApplicationQuit() { if(IsHost) Persist(); CloseTransport(); }
        // Release UDP 7777 synchronously. NetworkManager.Shutdown only flags a shutdown for its next update, which never
        // comes when Play Mode ends or scripts reload mid-session, and the editor process then keeps the port bound.
        void CloseTransport()
        {
            if(network==null || !network.IsListening) return;
            if(IsHost) Persist();
            try { network.Shutdown(); transport.Shutdown(); } catch(Exception e) { Debug.LogException(e); }
        }
        void OnDisable() => CloseTransport();
        void OnDestroy()
        {
            CloseTransport();
            if(network==null) return;
            network.ConnectionApprovalCallback-=Approve; network.OnClientConnectedCallback-=Connected;
            network.OnClientDisconnectCallback-=Disconnected; network.OnTransportFailure-=TransportFailed;
        }
    }
}
