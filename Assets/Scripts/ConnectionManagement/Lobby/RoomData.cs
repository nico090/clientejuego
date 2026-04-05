using System;

namespace Unity.BossRoom.ConnectionManagement.Lobby
{
    [Serializable]
    public class RoomInfo
    {
        public string room_id;
        public string name;
        public bool has_password;
        public bool is_locked;
        public int current_players;
        public int max_players;
        public string host_address;
        public int port;
        public string status;
        public string admin_player;
        public string created_at;
    }

    [Serializable]
    public class RoomListResponse
    {
        public RoomInfo[] items;
    }

    [Serializable]
    public class JoinRoomRequest
    {
        public string room_id;
        public string password;
        public string player_name;
    }

    [Serializable]
    public class JoinResponse
    {
        public bool success;
        public string host_address;
        public int port;
        public string room_key;
        public bool is_admin;
        public string error;
    }

    [Serializable]
    public class SetPrivateRequest
    {
        public string room_id;
        public string player_name;
        public string password;
    }

    [Serializable]
    public class StartGameRequest
    {
        public string room_id;
        public string player_name;
    }

    [Serializable]
    public class RoomStatusResponse
    {
        public string room_id;
        public string status;
        public int current_players;
        public string admin_player;
        public bool is_locked;
    }
}
