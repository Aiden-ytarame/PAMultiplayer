using System;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    public class NetworkManager : MonoBehaviour
    {
        void OnEnable()
        {
            try
            {
                if (!SteamClient.IsValid || !SteamClient.IsLoggedOn)
                    SteamClient.Init(440310);
            }
            catch (Exception e)
            {
                Plugin.Inst.Log.LogError(e);
            }
            
            if (StaticManager.IsHosting)
            {
                Plugin.Inst.Log.LogError("Init Server");
                SteamLobbyManager.Inst.CreateLobby();

            }
        }

        //everything here will go into its own steam manager later on
        //this is all testing
      
        void Update() //Not sure if FixedUpdate is better.
        {
            var PosEnu = StaticManager.PlayerPositions.GetEnumerator();

            while (PosEnu.MoveNext())
            {
                if (PosEnu.Current.Key == StaticManager.LocalPlayer)
                    continue;

                if (!StaticManager.Players[PosEnu.Current.Key].PlayerObject)
                    continue;

                Rigidbody2D rb = StaticManager.Players[PosEnu.Current.Key].PlayerObject.Player_Rigidbody;

                Vector2 DeltaPos = rb.position - PosEnu.Current.Value;
                StaticManager.Players[PosEnu.Current.Key].PlayerObject.Player_Wrapper.transform.Rotate(new Vector3(0, 0, Mathf.Atan2(DeltaPos.x, DeltaPos.y)), Space.World);

                rb.position = PosEnu.Current.Value;

            }

            PosEnu.Dispose();
        }

        void OnDisable()
        {
            try
            {
                SteamManager.Inst.EndServer();
                SteamManager.Inst.EndClient();
            }
            catch(Exception e)
            {
                Plugin.Inst.Log.LogError(e);
            }
            
            StaticManager.LobbyInfo = new LobbyInfo();
        }

    }
}
