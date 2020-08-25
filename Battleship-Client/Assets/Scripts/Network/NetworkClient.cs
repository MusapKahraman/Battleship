﻿using System;
using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Schemas;
using Colyseus;
using UnityEngine;
using DataChange = Colyseus.Schema.DataChange;

namespace BattleshipGame.Network
{
    public class NetworkClient
    {
        private const string LocalEndpoint = "ws://localhost:2567";
        private const string OnlineEndpoint = "ws://bronzehero.herokuapp.com";
        private const string RoomName = "game";
        private bool _initialStateReceived;
        private Room<State> _room;
        public string SessionId => _room?.SessionId;
        public State State => _room?.State;
        public event Action Connected;
        public event Action<string> GamePhaseChanged;
        public event Action<State> InitialStateReceived;

        public async void Connect(NetworkManager.ServerType serverType)
        {
            if (_room != null && _room.Connection.IsOpen) return;
            string endPoint = serverType == NetworkManager.ServerType.Online ? OnlineEndpoint : LocalEndpoint;
            var client = new Client(endPoint);
            try
            {
                Debug.Log($"Joining in the room: \'{RoomName}\'");
                _room = await client.JoinOrCreate<State>(RoomName);
                Debug.Log($"Joined successfully in the room: \'{RoomName}\'!");
                Connected?.Invoke();
                _room.OnStateChange += OnStateChange;
                _room.State.OnChange += OnRoomStateChange;
                _room.State.players.OnAdd += OnPlayerAdd;
                _room.State.players.OnRemove += OnPlayerRemove;
                _room.State.players.OnChange += OnPlayerChange;
            }
            catch (Exception exception)
            {
                Debug.Log($"Error joining: {exception.Message}");
            }

            void OnStateChange(State state, bool isFirstState)
            {
                if (isFirstState)
                {
                    Debug.Log("Room state is changed for the first time.");
                    _initialStateReceived = true;
                    InitialStateReceived?.Invoke(state);
                }

                LogState(state);
            }

            void OnRoomStateChange(List<DataChange> changes)
            {
                foreach (var dataChange in changes)
                    Debug.Log($"{dataChange.Field}: {dataChange.PreviousValue} -> {dataChange.Value}");

                if (!_initialStateReceived) return;
                foreach (var change in changes.Where(change => change.Field == "phase"))
                    GamePhaseChanged?.Invoke((string) change.Value);
            }

            void OnPlayerAdd(Player player, string key)
            {
                Debug.Log($"player added: {key}");
            }

            void OnPlayerRemove(Player player, string key)
            {
                Debug.Log($"player removed: {key}");
            }

            void OnPlayerChange(Player player, string key)
            {
                Debug.Log($"player moved: {key}");
            }
        }

        private static void LogState(State state)
        {
            Debug.Log($"<color=#33F4FF>Room state is changed.</color> Phase: {state.phase}" +
                      $" | Player count: {state.players.Items.Count} | Current turn: {state.currentTurn}" +
                      $" | Player turn: {state.playerTurn} | Winning player: {state.winningPlayer}");

            // foreach (var item in state.player1Shots.Items) Debug.Log($"Player1 shot: [{item.Key}, {item.Value}]");
            //
            // foreach (var item in state.player2Shots.Items) Debug.Log($"Player2 shot: [{item.Key}, {item.Value}]");
            //
            // foreach (var item in state.player1Ships.Items) Debug.Log($"Player1 ship: [{item.Key}, {item.Value}]");
            //
            // foreach (var item in state.player2Ships.Items) Debug.Log($"Player2 ship: [{item.Key}, {item.Value}]");
        }

        public void Leave()
        {
            _room?.Leave();
            _room = null;
        }

        public void SendPlacement(int[] placement)
        {
            _room.Send("place", placement);
        }

        public void SendTurn(int[] targetIndexes)
        {
            _room.Send("turn", targetIndexes);
        }
    }
}