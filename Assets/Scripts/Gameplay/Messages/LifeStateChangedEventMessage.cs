using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Utils;

namespace Unity.BossRoom.Gameplay.Messages
{
    public struct LifeStateChangedEventMessage
    {
        public LifeState NewLifeState;
        public CharacterTypeEnum CharacterType;
        public FixedPlayerName CharacterName;

        /// <summary>
        /// Reference to the ServerCharacter whose life state changed. Used for respawn.
        /// </summary>
        public ServerCharacter ServerCharacter;

        /// <summary>
        /// netId of the character that dealt the killing blow. 0 if unknown.
        /// </summary>
        public uint KillerNetId;

        /// <summary>
        /// True if the killer was an NPC, false if it was a player (or unknown).
        /// </summary>
        public bool KilledByNpc;
    }
}
