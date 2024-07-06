using System;
using PAMultiplayer.Patch;
using UnityEngine;
using UnityEngine.Playables;

namespace PAMultiplayer.Managers
{
    //this class has to be remade or removed
    public class NetworkManager : MonoBehaviour
    {
        void OnEnable()
        {
            if (StaticManager.IsHosting && !GameManager.Inst.IsEditor)
            {
                Plugin.Inst.Log.LogError("Init Server");
                SteamLobbyManager.Inst.CreateLobby();
            }
        }

        //everything here will go into its own steam manager later on
        //this is all testing
      
        void Update() //Not sure if FixedUpdate is better.
        {
            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();
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
        }

    }
}
