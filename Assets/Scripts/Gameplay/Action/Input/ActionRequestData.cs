using System;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Comprehensive class that contains information needed to play back any action on the server. This is what gets sent client->server when
    /// the Action gets played, and also what gets sent server->client to broadcast the action event. Note that the OUTCOMES of the action effect
    /// don't ride along with this object when it is broadcast to clients; that information is sync'd separately, usually by SyncVars.
    /// </summary>
    public struct ActionRequestData
    {
        public ActionID ActionID; //index of the action in the list of all actions in the game - a way to recover the reference to the instance at runtime
        public Vector3 Position;           //center position of skill, e.g. "ground zero" of a fireball skill.
        public Vector3 Direction;          //direction of skill, if not inferrable from the character's current facing.
        public uint[] TargetIds;           //netIds of targets, or null if untargeted.
        public float Amount;               //can mean different things depending on the Action. For a ChaseAction, it will be target range the ChaseAction is trying to achieve.
        public bool ShouldQueue;           //if true, this action should queue. If false, it should clear all current actions and play immediately.
        public bool ShouldClose;           //if true, the server should synthesize a ChaseAction to close to within range of the target before playing the Action. Ignored for untargeted actions.
        public bool CancelMovement;        // if true, movement is cancelled before playing this action

        //O__O Hey, are you adding something? Be sure to update ActionLogicInfo, as well as the methods below.

        [Flags]
        private enum PackFlags
        {
            None = 0,
            HasPosition = 1,
            HasDirection = 1 << 1,
            HasTargetIds = 1 << 2,
            HasAmount = 1 << 3,
            ShouldQueue = 1 << 4,
            ShouldClose = 1 << 5,
            CancelMovement = 1 << 6,
            //currently serialized with a byte. Change Read/Write if you add more than 8 fields.
        }

        public static ActionRequestData Create(Action action) =>
            new()
            {
                ActionID = action.ActionID
            };

        /// <summary>
        /// Returns true if the ActionRequestDatas are "functionally equivalent" (not including their Queueing or Closing properties).
        /// </summary>
        public bool Compare(ref ActionRequestData rhs)
        {
            bool scalarParamsEqual = (ActionID, Position, Direction, Amount) == (rhs.ActionID, rhs.Position, rhs.Direction, rhs.Amount);
            if (!scalarParamsEqual) { return false; }

            if (TargetIds == rhs.TargetIds) { return true; } //covers case of both being null.
            if (TargetIds == null || rhs.TargetIds == null || TargetIds.Length != rhs.TargetIds.Length) { return false; }
            for (int i = 0; i < TargetIds.Length; i++)
            {
                if (TargetIds[i] != rhs.TargetIds[i]) { return false; }
            }

            return true;
        }
    }

    /// <summary>
    /// Mirror NetworkWriter/NetworkReader extensions so ActionRequestData can be used in
    /// Commands, ClientRpcs, and NetworkMessages.
    /// </summary>
    public static class ActionRequestDataReaderWriterExtensions
    {
        [Flags]
        private enum PackFlags : byte
        {
            None = 0,
            HasPosition = 1,
            HasDirection = 1 << 1,
            HasTargetIds = 1 << 2,
            HasAmount = 1 << 3,
            ShouldQueue = 1 << 4,
            ShouldClose = 1 << 5,
            CancelMovement = 1 << 6,
        }

        public static void WriteActionRequestData(this NetworkWriter writer, ActionRequestData value)
        {
            PackFlags flags = PackFlags.None;
            if (value.Position != Vector3.zero) flags |= PackFlags.HasPosition;
            if (value.Direction != Vector3.zero) flags |= PackFlags.HasDirection;
            if (value.TargetIds != null) flags |= PackFlags.HasTargetIds;
            if (value.Amount != 0) flags |= PackFlags.HasAmount;
            if (value.ShouldQueue) flags |= PackFlags.ShouldQueue;
            if (value.ShouldClose) flags |= PackFlags.ShouldClose;
            if (value.CancelMovement) flags |= PackFlags.CancelMovement;

            writer.WriteInt(value.ActionID.ID);
            writer.WriteByte((byte)flags);

            if ((flags & PackFlags.HasPosition) != 0) writer.Write(value.Position);
            if ((flags & PackFlags.HasDirection) != 0) writer.Write(value.Direction);
            if ((flags & PackFlags.HasTargetIds) != 0)
            {
                writer.WriteInt(value.TargetIds.Length);
                foreach (var id in value.TargetIds)
                    writer.WriteUInt(id);
            }
            if ((flags & PackFlags.HasAmount) != 0) writer.WriteFloat(value.Amount);
        }

        public static ActionRequestData ReadActionRequestData(this NetworkReader reader)
        {
            var data = new ActionRequestData();
            data.ActionID = new ActionID { ID = reader.ReadInt() };
            var flags = (PackFlags)reader.ReadByte();

            data.ShouldQueue = (flags & PackFlags.ShouldQueue) != 0;
            data.CancelMovement = (flags & PackFlags.CancelMovement) != 0;
            data.ShouldClose = (flags & PackFlags.ShouldClose) != 0;

            if ((flags & PackFlags.HasPosition) != 0) data.Position = reader.Read<Vector3>();
            if ((flags & PackFlags.HasDirection) != 0) data.Direction = reader.Read<Vector3>();
            if ((flags & PackFlags.HasTargetIds) != 0)
            {
                int count = reader.ReadInt();
                data.TargetIds = new uint[count];
                for (int i = 0; i < count; i++)
                    data.TargetIds[i] = reader.ReadUInt();
            }
            if ((flags & PackFlags.HasAmount) != 0) data.Amount = reader.ReadFloat();

            return data;
        }
    }
}
