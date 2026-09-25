using Steamworks.Ugc;

namespace PAMultiplayer.Data;

public struct SteamWorkshopLevel(Item levelItem, SteamWorkshopLevel.VisibilityType visibility)
{
    public enum VisibilityType
    {
        Private,
        Friends,
        Unlisted,
        Public
    }
    public Item LevelItem = levelItem;
    public VisibilityType Visibility = visibility;
}