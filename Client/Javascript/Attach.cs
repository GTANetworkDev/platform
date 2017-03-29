using GTA;
using GTA.Native;
using GTANetwork.Util;
using GTANetworkShared;
using System;

namespace GTANetwork.Streamer
{
    internal partial class Streamer
    {
        public void AttachEntityToEntity(IStreamedItem ent, IStreamedItem entTarget, Attachment info)
        {
            if (!ent.StreamedIn || !entTarget.StreamedIn || info == null) return;
            LogManager.DebugLog("AE2E_1");
            if (entTarget.EntityType == (byte)EntityType.Blip ||
                entTarget.EntityType == (byte)EntityType.TextLabel || // Can't attach to a blip, textlabel or marker
                entTarget.EntityType == (byte)EntityType.Marker ||
                ent.EntityType == (byte)EntityType.Marker ||
                ent.EntityType == (byte)EntityType.TextLabel || // If we're attaching blip/label/marker, UpdateAttachments will take care of it for us.
                ent.EntityType == (byte)EntityType.Blip ||
                ent.EntityType == (byte)EntityType.Pickup) // TODO: Make pickups attachable.
            {
                return;
            }
            LogManager.DebugLog("AE2E_2");
            var handleSource = NetToEntity(ent.RemoteHandle);
            var handleTarget = NetToEntity(entTarget.RemoteHandle);
            LogManager.DebugLog("AE2E_3");
            if (handleSource == null || handleTarget == null) return;
            LogManager.DebugLog("AE2E_4");
            int bone = 0;

            if (!string.IsNullOrWhiteSpace(info.Bone))
            {
                if (entTarget is RemotePlayer || entTarget is RemotePed)
                {
                    bone = Function.Call<int>(Hash.GET_PED_BONE_INDEX, handleTarget.Handle, (int)Enum.Parse(typeof(Bone), info.Bone, true));
                }
                else
                {
                    bone = new Prop(handleTarget.Handle).GetBoneIndex(info.Bone);
                }
            }

            if (bone == -1) bone = 0;

            LogManager.DebugLog("ATTACHING " + handleSource.Handle + " TO " + handleTarget.Handle +
                                " WITH BONE " + bone);

            Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, handleSource.Handle, handleTarget.Handle,
                bone,
                info.PositionOffset.X, info.PositionOffset.Y, info.PositionOffset.Z,
                info.RotationOffset.X, info.RotationOffset.Y, info.RotationOffset.Z,
                false, // p9
                false, // useSoftPinning
                false, // collision
                false, // p12
                2, // vertexIndex
                true // fixedRot
                );
        }

        public void DetachEntity(IStreamedItem ent, bool collision)
        {
            if (ent?.AttachedTo == null) return;

            var target = NetToStreamedItem(ent.AttachedTo.NetHandle);

            target?.Attachables?.Remove(ent.RemoteHandle);

            var entHandle = NetToEntity(ent.RemoteHandle);

            if (entHandle != null && entHandle.Handle != 0 && !(ent is RemoteBlip)) Function.Call(Hash.DETACH_ENTITY, entHandle.Handle, true, collision);

            ent.AttachedTo = null;
        }

        public void ReattachAllEntities(IStreamedItem ent, bool recursive)
        {
            var prop = ent as EntityProperties;
            if (prop?.Attachables == null) return;

            foreach (var i in prop.Attachables)
            {
                var target = NetToStreamedItem(i);

                if (target == null) continue;
                AttachEntityToEntity(target, ent, ent.AttachedTo);

                if (recursive) ReattachAllEntities(target, true);
            }
        }
    }
}
