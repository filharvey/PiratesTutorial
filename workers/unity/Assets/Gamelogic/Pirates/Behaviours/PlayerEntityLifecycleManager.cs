﻿using Improbable;using Improbable.Entity.Component;using Improbable.Player;using Improbable.Unity;using Improbable.Unity.Core;using Improbable.Unity.Visualizer;using Improbable.Worker;using UnityEngine;

namespace Assets.Gamelogic.Player
{
    // Enable this MonoBehaviour on FSim (server-side) workers only
    [EngineType(EnginePlatform.FSim)]
    public class PlayerEntityLifecycleManager : MonoBehaviour
    {
        // Enable this MonoBehaviour only on the worker which has write-access for the entity's PlayerLifecycle component
        [Require] private PlayerLifecycle.Writer PlayerLifecycleWriter;

        EntityId thisEntityId;

        void OnEnable()
        {
            // Cache entity id for faster retrieval
            thisEntityId = gameObject.EntityId();
            // Initialize heartbeat countdown
            PlayerLifecycleWriter.Send(new PlayerLifecycle.Update().SetCurrentMissedHeartbeats(0));

            // Register command callbacks
            PlayerLifecycleWriter.CommandReceiver.OnDeletePlayer += OnDeletePlayer;
            PlayerLifecycleWriter.CommandReceiver.OnHeartbeat += OnHeartbeat;

            // Periodic counter increase
            InvokeRepeating("IncrementMissedHeartbeats", 0f, PlayerLifecycleWriter.Data.playerHeartbeatInterval);
        }

        void OnDisable()        {
            // Deregister command callbacks
            PlayerLifecycleWriter.CommandReceiver.OnDeletePlayer -= OnDeletePlayer;            PlayerLifecycleWriter.CommandReceiver.OnHeartbeat -= OnHeartbeat;            CancelInvoke("IncrementMissedHeartbeats");        }

        // Command callback for requests for this entity to be deleted from the simulation
        private void OnDeletePlayer(ResponseHandle<PlayerLifecycle.Commands.DeletePlayer, DeletePlayerRequest, DeletePlayerResponse> responseHandle)        {            DeletePlayer();            responseHandle.Respond(new DeletePlayerResponse());        }

        private void DeletePlayer()        {
            // Use World Commands API to delete this entity
            SpatialOS.Commands.DeleteEntity(PlayerLifecycleWriter, thisEntityId, result =>
                {
                    if (result.StatusCode != StatusCode.Success)
                    {
                        Debug.LogErrorFormat("PlayerEntityLifecycleManager failed to delete inactive player entity {0} with error message: {1}", thisEntityId, result.ErrorMessage);
                        return;
                    }
                    Debug.Log("PlayerEntityLifecycleManager deleted inactive player entity " + thisEntityId);
                });        }

        // Command callback for handling heartbeats from clients indicating client is connect and missed heartbeat counter should be reset
        private void OnHeartbeat(ResponseHandle<PlayerLifecycle.Commands.Heartbeat, HeartbeatRequest, HeartbeatResponse> responseHandle)        {
            // Heartbeats are issued by clients authoritative over the player entity and should only be sending heartbeats for themselves
            if (responseHandle.Request.senderEntityId == thisEntityId)            {
                // Reset missed heartbeat counter to avoid entity being deleted
                PlayerLifecycleWriter.Send(new PlayerLifecycle.Update().SetCurrentMissedHeartbeats(0));            }
            // Acknowledge command receipt
            responseHandle.Respond(new HeartbeatResponse());        }

        private void IncrementMissedHeartbeats()        {            if (PlayerLifecycleWriter.Data.currentMissedHeartbeats >= PlayerLifecycleWriter.Data.maxMissedHeartbeats)            {
                // Delete entity if it has missed too many heartbeats
                DeletePlayer();            }            else            {                // Increment counter if time-out period has not elapsed                PlayerLifecycleWriter.Send(new PlayerLifecycle.Update().SetCurrentMissedHeartbeats(PlayerLifecycleWriter.Data.currentMissedHeartbeats + 1));
            }
        }
    }
}