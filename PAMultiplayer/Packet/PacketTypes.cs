
namespace PAMultiplayer.Packet;

public enum PacketType : ushort
{
    Damage,
    Position,
    Start,
    PlayerId,
    Checkpoint,
    Rewind,
    Boost,
    NextLevel,
    DamageAll,
    OpenChallenge,
    CheckLevelId,
    ChallengeAudioData,
    ChallengeVote,
    LobbyState
}
