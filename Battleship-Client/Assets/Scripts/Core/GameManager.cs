﻿using System;
using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Common;
using BattleshipGame.Network;
using BattleshipGame.UI;
using Colyseus.Schema;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace BattleshipGame.Core
{
    public class GameManager : MonoBehaviour
    {
        private const int ShotsPerTurn = 3;
        private static int _cellCount;
        private static GameClient _client;
        private static GameMode _mode;
        private static int _playerNumber;
        private static int[] _placementMap;
        private static readonly List<int> Shots = new List<int>();
        private static State _state;
        [SerializeField] private TMP_Text messageField;
        [SerializeField] private MapViewer userMap;
        [SerializeField] private MapViewer opponentMap;
        [SerializeField] private OpponentStatusMaskPlacer opponentStatusMaskPlacer;
        [SerializeField] private int areaSize = 9;

        [Tooltip("Add the types of ships only. Amounts and the sorting order are determined by the ship itself.")]
        [SerializeField]
        private List<Ship> ships;

        private Ship _currentShipToBePlaced;
        private bool _isShipPlacementComplete;
        private int _shipsPlaced;
        private ConnectionManager _connectionManager;
        private Queue<Ship> _shipsToBePlaced;
        public int MapAreaSize => areaSize;
        public IEnumerable<Ship> Ships => ships;

        private void Start()
        {
            if (ConnectionManager.TryGetInstance(out _connectionManager))
            {
                _client = _connectionManager.Client;
                _cellCount = areaSize * areaSize;
                ResetPlacementMap();
                _client.InitialStateReceived += OnInitialStateReceived;
                _client.GamePhaseChanged += OnGamePhaseChanged;
                if (_client.State != null) OnInitialStateReceived(_client.State);
                opponentMap.SetDisabled();
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }

        private void OnDestroy()
        {
            if (_client == null) return;
            _client.InitialStateReceived -= OnInitialStateReceived;
            _client.GamePhaseChanged -= OnGamePhaseChanged;
        }

        private void OnValidate()
        {
            var hashSet = new HashSet<Ship>();
            foreach (var ship in ships) hashSet.Add(ship);

            ships = hashSet.OrderBy(ship => ship.rankOrder).ToList();
        }

        public event Action FireReady;
        public event Action FireNotReady;
        public event Action RandomAvailable;
        public event Action RandomHidden;
        public event Action ContinueAvailable;
        public event Action ContinueHidden;

        private void ResetPlacementMap()
        {
            _shipsPlaced = 0;
            _placementMap = new int[_cellCount];
            for (var i = 0; i < _cellCount; i++) _placementMap[i] = -1;
            userMap.ClearAllShips();
            _isShipPlacementComplete = false;
        }

        public void PlaceShipsRandomly()
        {
            ResetPlacementMap();
            PopulateShipsToBePlaced();
            _currentShipToBePlaced = _shipsToBePlaced.Dequeue();
            while (!_isShipPlacementComplete)
                PlaceShip(new Vector3Int(Random.Range(0, areaSize), Random.Range(0, areaSize), 0));
        }

        private void BeginShipPlacement()
        {
            _mode = GameMode.Placement;
            RandomAvailable?.Invoke();
            messageField.text = "Place your Ships!";
            userMap.SetPlacementMode();
            PopulateShipsToBePlaced();
            _currentShipToBePlaced = _shipsToBePlaced.Dequeue();
            UpdateCursor();
        }

        private void PopulateShipsToBePlaced()
        {
            _shipsToBePlaced = new Queue<Ship>();
            foreach (var ship in ships)
                for (var i = 0; i < ship.amount; i++)
                    _shipsToBePlaced.Enqueue(ship);
        }

        public void PlaceShip(Vector3Int cellCoordinate)
        {
            if (_mode != GameMode.Placement) return;
            if (!DoesShipFitIn(_currentShipToBePlaced, cellCoordinate)) return;
            (int shipWidth, int shipHeight) = _currentShipToBePlaced.GetShipSize();
            int xMin = cellCoordinate.x - 1;
            int xMax = cellCoordinate.x + shipWidth;
            int yMin = cellCoordinate.y - shipHeight;
            int yMax = cellCoordinate.y + 1;
            for (int y = yMin; y <= yMax; y++)
            {
                if (y < 0 || y > areaSize - 1) continue;
                for (int x = xMin; x <= xMax; x++)
                {
                    if (x < 0 || x > areaSize - 1) continue;
                    if (!AddCellToPlacementMap(new Vector3Int(x, y, 0), true)) return;
                }
            }

            foreach (var p in _currentShipToBePlaced.PartCoordinates)
                AddCellToPlacementMap(new Vector3Int(cellCoordinate.x + p.x, cellCoordinate.y + p.y, 0));

            userMap.SetShip(_currentShipToBePlaced, cellCoordinate);
            if (_shipsToBePlaced.Count > 0)
            {
                _currentShipToBePlaced = _shipsToBePlaced.Dequeue();
                _shipsPlaced++;
                UpdateCursor();
                return;
            }

            _isShipPlacementComplete = true;
            userMap.SetDisabled();
            ContinueAvailable?.Invoke();
        }

        public bool DoesShipFitIn(Ship ship, Vector3Int cellCoordinate)
        {
            (int shipWidth, int shipHeight) = ship.GetShipSize();
            return cellCoordinate.x >= 0 && cellCoordinate.x + shipWidth <= areaSize &&
                   cellCoordinate.y - (shipHeight - 1) >= 0;
        }

        public void ContinueAfterPlacementComplete()
        {
            ContinueHidden?.Invoke();
            RandomHidden?.Invoke();
            _client.SendPlacement(_placementMap);
            WaitForOpponentPlaceShips();
        }

        private void WaitForOpponentPlaceShips()
        {
            _mode = GameMode.Placement;
            userMap.SetDisabled();
            opponentMap.SetDisabled();
            messageField.text = "Waiting for the Opponent to place the ships!";
        }

        private void WaitForOpponentTurn()
        {
            _mode = GameMode.Battle;
            userMap.SetDisabled();
            opponentMap.SetDisabled();
            messageField.text = "Waiting for the Opponent to Attack!";
        }

        private void StartTurn()
        {
            Shots.Clear();
            _mode = GameMode.Battle;
            userMap.SetDisabled();
            opponentMap.SetAttackMode();
            messageField.text = "It's Your Turn!";
        }

        private void ShowResult()
        {
            _mode = GameMode.Result;
            userMap.SetDisabled();
            opponentMap.SetDisabled();
            messageField.text = _state.winningPlayer == _playerNumber ? "You Win!" : "You Lost!!!";
        }

        public void MarkTarget(Vector3Int targetCoordinate)
        {
            int targetIndex = GridConverter.ToCellIndex(targetCoordinate, areaSize);
            if (Shots.Contains(targetIndex))
            {
                Shots.Remove(targetIndex);
                opponentMap.ClearMarkerTile(targetIndex);
            }
            else if (Shots.Count < ShotsPerTurn && opponentMap.SetMarker(targetIndex, Marker.MarkedTarget))
            {
                Shots.Add(targetIndex);
            }

            if (Shots.Count == ShotsPerTurn)
                FireReady?.Invoke();
            else
                FireNotReady?.Invoke();
        }

        public static void FireShots()
        {
            if (Shots.Count == ShotsPerTurn)
                _client.SendTurn(Shots.ToArray());
        }

        private void UpdateCursor()
        {
            userMap.SetCursorTile(_currentShipToBePlaced.tile);
        }

        private bool AddCellToPlacementMap(Vector3Int coordinate, bool testOnly = false)
        {
            int cellIndex = GridConverter.ToCellIndex(coordinate, areaSize);
            if (cellIndex < 0 || cellIndex >= _cellCount) return false;
            if (_placementMap[cellIndex] >= 0) return false;
            if (testOnly) return true;
            _placementMap[cellIndex] = _shipsPlaced;
            return true;
        }

        private void OnInitialStateReceived(State initialState)
        {
            _state = initialState;
            var player = _state.players[_client.SessionId];
            if (player != null)
                _playerNumber = player.seat;
            else
                _playerNumber = -1;
            _state.OnChange += OnStateChanged;
            _state.player1Shots.OnChange += OnFirstPlayerShotsChanged;
            _state.player2Shots.OnChange += OnSecondPlayerShotsChanged;
            _state.player1Ships.OnChange += OnFirstPlayerShipsChanged;
            _state.player2Ships.OnChange += OnSecondPlayerShipsChanged;
            OnGamePhaseChanged(_state.phase);
        }

        private void OnStateChanged(List<DataChange> changes)
        {
            Debug.Log("OnStateChanged");
            foreach (var _ in changes.Where(change => change.Field == "playerTurn"))
                CheckTurn();
        }

        private void OnFirstPlayerShotsChanged(int value, int key)
        {
            Debug.Log($"Player1ShotsChanged: {key},{value}");
            const int playerNumber = 1;
            SetMarker(key, value, playerNumber);
        }

        private void OnSecondPlayerShotsChanged(int value, int key)
        {
            Debug.Log($"Player2ShotsChanged: {key},{value}");
            const int playerNumber = 2;
            SetMarker(key, value, playerNumber);
        }

        private void SetMarker(int item, int index, int playerNumber)
        {
            Marker marker;
            if (_playerNumber == playerNumber)
            {
                marker = Marker.ShotTarget;
                Debug.Log($"SetMarker at opponentMap: {item}");
                opponentMap.SetMarker(item, marker);
            }
            else
            {
                marker = _placementMap[item] == -1 ? Marker.Missed : Marker.Hit;
                Debug.Log($"SetMarker at userMap: {item}");
                userMap.SetMarker(item, marker);
            }
        }

        private void OnFirstPlayerShipsChanged(int value, int key)
        {
            const int playerNumber = 1;
            SetHealth(key, value, playerNumber);
        }

        private void OnSecondPlayerShipsChanged(int value, int key)
        {
            const int playerNumber = 2;
            SetHealth(key, value, playerNumber);
        }

        private void SetHealth(int item, int index, int playerNumber)
        {
            if (_playerNumber != playerNumber) opponentStatusMaskPlacer.PlaceMask(item, index);
        }

        private void CheckTurn()
        {
            if (_state.playerTurn == _playerNumber)
                StartTurn();
            else
                WaitForOpponentTurn();
        }

        private static void Leave()
        {
            _client.Leave();
            SceneManager.LoadScene(0);
        }

        private void OnGamePhaseChanged(string phase)
        {
            switch (phase)
            {
                case "waiting":
                    Leave();
                    break;
                case "place":
                    BeginShipPlacement();
                    break;
                case "battle":
                    CheckTurn();
                    break;
                case "result":
                    ShowResult();
                    break;
            }
        }

        private enum GameMode
        {
            Placement,
            Battle,
            Result
        }
    }
}