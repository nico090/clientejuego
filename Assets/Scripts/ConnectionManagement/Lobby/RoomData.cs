using System;

namespace Unity.BossRoom.ConnectionManagement.Lobby
{
    [Serializable]
    public class RoomInfo
    {
        public string room_id;
        public string name;
        public bool has_password;
        public int current_players;
        public int max_players;
        public string host_address;
        public int port;
        public string status;
        public string created_at;
    }

    [Serializable]
    public class RoomListResponse
    {
        public RoomInfo[] items;
    }

    [Serializable]
    public class CreateRoomRequest
    {
        public string name;
        public string password;
        public int max_players;
        public string creator_name;
    }

    [Serializable]
    public class CreateRoomResponse
    {
        public string room_id;
        public string name;
        public int port;
        public int max_players;
        public string host_address;
        public string room_key;
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
        public string error;
    }

    [Serializable]
    public class RoomStatusResponse
    {
        public string room_id;
        public string status;
        public int current_players;
    }
}
