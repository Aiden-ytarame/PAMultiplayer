using PAMultiplayer.Managers;

namespace PAMultiplayer.Data;

public class LobbyInfo
{
    public SteamLobbyManager.LobbyState LobbyState;
        
    public bool HasLoadedAllInfo => HasLoadedExternalInfo && HasLoadedBasePlayerIds;

    public bool HasLoadedExternalInfo;
    public bool HasLoadedBasePlayerIds;

    public bool HasLoadedAllLobbyInfo => HasLoadedMainLobbyInfo && HasLoadedMidLobbyInfo;
        
    public bool HasLoadedMidLobbyInfo = true;
    public bool HasLoadedMainLobbyInfo = true;
    
    public bool HasStarted = false;
        
    public bool JoinedMidLevel = false;

    public uint RewindCounter = 0; //used to prevent laggy players to take damage from old rewinds

    public bool AllowClientLevels = false;
}