using Unity.BossRoom.Utils;

namespace Unity.BossRoom.Gameplay.Messages
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    public struct CheatUsedMessage
    {
        string m_CheatUsed;
        FixedPlayerName m_CheaterName;

        public string CheatUsed => m_CheatUsed;
        public string CheaterName => m_CheaterName.ToString();

        public CheatUsedMessage(string cheatUsed, string cheaterName)
        {
            m_CheatUsed = cheatUsed;
            m_CheaterName = cheaterName;
        }
    }

#endif
}
