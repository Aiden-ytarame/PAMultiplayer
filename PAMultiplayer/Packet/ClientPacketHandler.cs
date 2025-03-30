using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
using PAMultiplayer.Managers.MenuManagers;
using PAMultiplayer.Patch;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Packet;

public abstract class ClientPacketHandler
{
    public static readonly Dictionary<PacketType, ClientPacketHandler> PacketHandlers = new()
    {
        { PacketType.Position          , new PositionClientPacket()},
        { PacketType.Damage            , new DamageClientPacket()}, 
        { PacketType.Start             , new StartClientPacket()},
        { PacketType.PlayerId          , new PlayerIdClientPacket()},
        { PacketType.Checkpoint        , new CheckpointClientPacket()},
        { PacketType.Rewind            , new RewindClientPacket()},
        { PacketType.Boost             , new BoostClientPacket()},
        { PacketType.NextLevel         , new NextLevelClientPacket()},
        { PacketType.DamageAll         , new DamageAllClientPacket()},
        { PacketType.OpenChallenge     , new OpenChallengeClientPacket()},
        { PacketType.CheckLevelId      , new CheckLevelIdClientPacket()},
        { PacketType.ChallengeAudioData, new AudioDataClientPacket()},
        { PacketType.ChallengeVote     , new ChallengeVoteClientPacket()},
        { PacketType.LobbyState        , new LobbyStateClientPacket()},
        { PacketType.LatePlayerLoaded  , new LatePlayerClientPacket()}
    };

   /// <summary>
   /// Checks if there's enough bytes for the type of packet received
   /// </summary>
    public void TryProcessPacket(BinaryReader reader, int size)
    {
        
        if (size < DataSize)
        {
            PAM.Logger.LogWarning($"Packet size is too small [{size}], type [{GetType().Name}]");
            return;
        }
        
        ProcessPacket(reader);
    }

    /// <summary>
    /// Overriden method to handle the data received using a BinaryReader.
    /// </summary>
    /// <param name="reader"></param>
    protected abstract void ProcessPacket(BinaryReader reader);
    
    /// <summary>
    /// The amount of Bytes required by this packet for it to be handled.
    /// </summary>
    protected abstract int DataSize { get; }
}

public class PositionClientPacket : ClientPacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        if (steamID.IsLocalPlayer()) return;
        
        if (GlobalsManager.Players.TryGetValue(steamID, out var playerData))
        {
            if (playerData.VGPlayerData.PlayerObject)
            {
                VGPlayer player = GlobalsManager.Players[steamID].VGPlayerData.PlayerObject;
                
                if(!player) return;
                
                Transform rb = player.Player_Wrapper;
                
                Vector2 pos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            
                var rot = pos - (Vector2)rb.position;
                rb.position = pos;
                if (rot.sqrMagnitude > 0.0001f)
                {
                    rot.Normalize();
                    player.p_lastMoveX = rot.x;
                    player.p_lastMoveY = rot.y;
                }
            }
        }
    }

    protected override int DataSize => 16;
}

public class DamageClientPacket : ClientPacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        PAM.Inst.Log.LogDebug($"Damaging player {steamID}");

        if (steamID.IsLocalPlayer()) return;

        int health = reader.ReadInt32();
        if(GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (!player.VGPlayerData.PlayerObject.IsValidPlayer()) return;
            player.VGPlayerData.PlayerObject.Health = health;
            player.VGPlayerData.PlayerObject.PlayerHit();
        }
      
    }

    protected override int DataSize => 12;
}

public class DamageAllClientPacket : ClientPacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
   {
       SteamId steamID = reader.ReadUInt64();
       int healthPreHit = reader.ReadInt32();
        PAM.Inst.Log.LogInfo($"Damaging all players {healthPreHit}");

        if (DataManager.inst.GetSettingBool("MpLinkedHealthPopup", true))
        {
            if (GlobalsManager.Players.TryGetValue(steamID, out var playerData))
            {
                string hex = VGPlayerManager.Inst.GetPlayerColorHex(playerData.VGPlayerData.PlayerID);
                VGPlayerManager.Inst.DisplayNotification($"Nano [<color=#{hex}>{playerData.Name}</color>] got hit!", 1f);
            }
        }
       
        
        foreach (var vgPlayerData in GlobalsManager.Players)
        {
            VGPlayer player = vgPlayerData.Value.VGPlayerData.PlayerObject;
            if (player.IsValidPlayer())
            {
                if (player.Health < healthPreHit)
                {
                    PAM.Inst.Log.LogWarning($"Old message");
                    continue;
                }

                if (player.IsLocalPlayer())
                {
                    PAM.Inst.Log.LogWarning($"LOCAL");
                    Player_Patch.IsDamageAll = true;
                }
                
                player.Health = healthPreHit;
                player.PlayerHit();
            }
        }
   }

    protected override int DataSize => 12;
}

public class StartClientPacket : ClientPacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        if (LobbyScreenManager.Instance)
        {
            LobbyScreenManager.Instance.StartLevel();
        }

        if (ChallengeManager.Inst)
        {
            ChallengeManager.Inst.StartVoting_Client();
        }
    }

    protected override int DataSize => 0;
}

public class PlayerIdClientPacket : ClientPacketHandler
{
    private static int _amountOfInfo;

   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        int id = reader.ReadInt32();
        
        //will likely remove this, this is useless
        int amount = reader.ReadInt32();
        
        GlobalsManager.HasLoadedBasePlayerIds = false;
        
        _amountOfInfo++;
        PAM.Logger.LogInfo($"Player Id from [{id}] Received");

        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (steamID.IsLocalPlayer())
                GlobalsManager.LocalPlayerObjectId = id;
            
            player.VGPlayerData.PlayerID = id;
        }
        else
        {
            VGPlayerManager.VGPlayerData newData = new()
            {
                PlayerID = id,
                ControllerID = id
            };
            GlobalsManager.Players.Add(steamID, new PlayerData(newData, "placeHolder"));
        }
        if (_amountOfInfo >= amount)
        {
            _amountOfInfo = 0;
            GlobalsManager.HasLoadedBasePlayerIds = true;
        }
    }
   
    protected override int DataSize => 16;
}

public class CheckpointClientPacket : ClientPacketHandler
{
    protected override void ProcessPacket(BinaryReader reader)
    {
        if (!GlobalsManager.HasLoadedLobbyInfo)
        {
            return;
        }

        int index = reader.ReadInt32();
        PAM.Logger.LogInfo($"Checkpoint [{index}] Received");
        GameManager.Inst.playingCheckpointAnimation = true;
        VGPlayerManager.Inst.RespawnPlayers();
        VGPlayerManager.Inst.HealPlayers();

        GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation(index));
    }

    protected override int DataSize => 4;
}

public class RewindClientPacket : ClientPacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        if (!GlobalsManager.HasLoadedLobbyInfo)
        {
            return;
        }
        
        int index = reader.ReadInt32();
        PAM.Logger.LogInfo($"Rewind to Checkpoint [{index}] Received");
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if (vgPlayerData.PlayerObject.IsValidPlayer())
            {
                vgPlayerData.PlayerObject.Health = 0;
                vgPlayerData.PlayerObject.ClearEvents();
                vgPlayerData.PlayerObject.PlayerDeath();
            }
        }
        GameManager.Inst.RewindToCheckpoint(index);
        
        //GlobalsManager.HasLoadedLobbyInfo = true;
    }

    protected override int DataSize => 4;
}

public class BoostClientPacket : ClientPacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        if(steamID.IsLocalPlayer()) return;
        
        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (player.VGPlayerData.PlayerObject && !player.VGPlayerData.PlayerObject.isDead)
            {
                player.VGPlayerData.PlayerObject.PlayParticles(VGPlayer.ParticleTypes.Boost);
            }
        }
    }

    protected override int DataSize => 8;
}

public class NextLevelClientPacket : ClientPacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        ulong levelID = reader.ReadUInt64();
        int seed = reader.ReadInt32();
        
        PAM.Logger.LogInfo($"New random seed : {seed}");

        GlobalsManager.LevelId = levelID.ToString();
        SteamLobbyManager.Inst.RandSeed = seed;
        GlobalsManager.IsReloadingLobby = true;
        GlobalsManager.LobbyState = SteamLobbyManager.LobbyState.Lobby;
        GlobalsManager.HasLoadedLobbyInfo = true;
        SceneLoader.Inst.manager.ClearLoadingTasks();
   
        VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(GlobalsManager.LevelId);
        if (level)
        {
            ArcadeManager.Inst.CurrentArcadeLevel = level;
            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            return;
        }

        GlobalsManager.IsDownloading = true;
        PAM.Logger.LogError($"You did not have the lobby's level downloaded!, Downloading Level...");
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }

    protected override int DataSize => 12;
}

public class OpenChallengeClientPacket : ClientPacketHandler
{
    protected override void ProcessPacket(BinaryReader reader)
    {
        SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "0");
        GlobalsManager.IsReloadingLobby = true;
        GlobalsManager.HasLoadedLobbyInfo = true;
        SceneLoader.Inst.manager.ClearLoadingTasks();
        SceneLoader.Inst.LoadSceneGroup("Challenge");
    }

    protected override int DataSize => 0;
}

public class CheckLevelIdClientPacket : ClientPacketHandler
{
    protected override void ProcessPacket(BinaryReader reader)
    {
        if (!ChallengeManager.Inst)
        {
            PAM.Logger.LogFatal("Challenge scene wasnt loaded when getting level list");
            return;
        }
        
        List<ulong> levelIds = new();
        for (int i = 0; i < 6; i++)
        {
            levelIds.Add(reader.ReadUInt64());
        }
        
        ChallengeManager.Inst.StartCoroutine(ProcessLevelIdCoroutine(levelIds).WrapToIl2Cpp());
    }

    IEnumerator ProcessLevelIdCoroutine(List<ulong> levelIds)
    {
        List<ulong> unknownLevelIds = new();
        for (var i = 0; i < levelIds.Count; i++)
        {
            ulong levelId = levelIds[i];
            bool hasLevel = false;
            do
            {
                if (ArcadeLevelDataManager.Inst.GetLocalCustomLevel(levelId.ToString()))
                {
                    hasLevel = true;
                    break;
                }

                yield return new WaitForSeconds(0.5f);
            } while (SteamWorkshopFacepunch.inst.isLoadingLevels);

            if (!hasLevel)
            {
                unknownLevelIds.Add(levelId);
            }
            ChallengeManager.Inst.CreateLevelEntry(levelId, i);
        }
            
        PAM.Logger.LogInfo($"requesting audio of [{unknownLevelIds.Count}] levels");
        SteamManager.Inst.Client.SendCheckLevelID(unknownLevelIds);
    }

    protected override int DataSize => 48;
}


public class AudioDataClientPacket : ClientPacketHandler
{
    
    private readonly List<float> _audioDataBuffer = new(400000);

    private ulong _lastId;

    protected override void ProcessPacket(BinaryReader reader)
    {
        PAM.Logger.LogInfo("Received audio data");
        var audioID = reader.ReadUInt64();
        var done = reader.ReadUInt16();
        var frequency = reader.ReadInt32();
        var channels = reader.ReadInt32();
       
        
        if (audioID != _lastId)
        {
            _lastId = audioID;
            _audioDataBuffer.Clear();
        }

        ReadOnlySpan<short> data =
            MemoryMarshal.Cast<byte, short>(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
        foreach (var f in data)
        {
            _audioDataBuffer.Add((float)f / short.MaxValue); //add range doesnt work, and ToArray may allocate a lot of memory
        }

        if (done != 1)
        {
            return;
        }
      
        PAM.Logger.LogInfo($"Got all audio data for level [{audioID}]");
        
        var newClip = AudioClip.Create(audioID.ToString(), _audioDataBuffer.Count / channels, channels, frequency, false);
        newClip.SetData(_audioDataBuffer.ToArray(), 0); //this to array is specially bad cuz its making 2 copies, may fix later
        newClip.LoadAudioData();
        
        _audioDataBuffer.Clear();

        if (ChallengeManager.Inst)
        {
            ChallengeManager.Inst.SetLevelSong(audioID, newClip);
        }
    }

    protected override int DataSize => 18;
}

public class ChallengeVoteClientPacket : ClientPacketHandler
{
    protected override void ProcessPacket(BinaryReader reader)
    {
        var levelId = reader.ReadUInt64();

        if (ChallengeManager.Inst)
        {
            ChallengeManager.Inst.SetVoteWinner(levelId);
        }
    }

    protected override int DataSize => 8;
}

public class LobbyStateClientPacket : ClientPacketHandler
{
    protected override void ProcessPacket(BinaryReader reader)
    {
        var hitCount = reader.ReadInt32();
        var currentTime = reader.ReadSingle();
        
        LevelEndScreen.ActionMoment actionMoment = new();  
        actionMoment.position = Vector3.zero;
        actionMoment.time = currentTime;
        
        var endScreen = Object.FindFirstObjectByType<LevelEndScreen>();//.Hits
        if (endScreen == null)
        {
            return;
        }
        for (int i = 0; i < hitCount; i++)
        {
            endScreen.Hits.Add(actionMoment);
        }
        
        if (GlobalsManager.HasLoadedLobbyInfo)
        {
            return;
        }
        GlobalsManager.HasLoadedLobbyInfo = true;
        
        GameManager.Inst.UnPause();
        
        AudioManager.Inst.CurrentAudioSource.time = currentTime;
        VGPlayerManager.inst.RespawnPlayers();
        
        while (reader.BaseStream.Position + 10 <= reader.BaseStream.Length)
        {
            var id = reader.ReadUInt64();
            var health = reader.ReadUInt16();

            if (GlobalsManager.Players.TryGetValue(id, out var player))
            {
               player.VGPlayerData.PlayerObject.Health = health;
               if (health <= 0)
               {
                   Object.Destroy(player.VGPlayerData.PlayerObject);
               }
            }
        }
    }

    protected override int DataSize => 8;
}

public class LatePlayerClientPacket : ClientPacketHandler
{
    protected override void ProcessPacket(BinaryReader reader)
    {
        var playerId = reader.ReadUInt64();

        if (!VGPlayerManager.Inst.players.Contains(GlobalsManager.Players[playerId].VGPlayerData))
        {
            VGPlayerManager.Inst.players.Add(GlobalsManager.Players[playerId].VGPlayerData);
        }
    }

    protected override int DataSize => 8;
}
