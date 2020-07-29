﻿using System.Linq;
using BattleshipGame.Network;
using BattleshipGame.UI;
using Colyseus.Schema;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleshipGame.Core
{
    internal enum GameMode
    {
        Placement,
        Battle,
        Result
    }

    public class GameManager : MonoBehaviour
    {
        private int _cellCount;
        private GameClient _client;
        private int _currentShot;
        private GameMode _mode;
        private int _myPlayerNumber;
        private int[] _placement;
        private int _shipsPlaced;
        private int[] _shots;
        private State _state;
        [SerializeField] private int mapSize = 10;
        [SerializeField] private MapViewer mapViewer;
        [SerializeField] private TMP_Text messageField;

        private void Start()
        {
            _client = GameClient.Instance;

            if (!_client.Connected) SceneManager.LoadScene("ConnectingScene");

            _cellCount = (mapSize - 1) * (mapSize - 1);
            _placement = new int[_cellCount];
            _shots = new int[3];

            for (var i = 0; i < _cellCount; i++) _placement[i] = -1;

            _client.InitialStateReceived += OnInitialStateReceived;
            _client.GamePhaseChanged += OnGamePhaseChanged;

            if (_client.State != null) OnInitialStateReceived(this, _client.State);
        }

        private void OnDestroy()
        {
            _client.InitialStateReceived -= OnInitialStateReceived;
            _client.GamePhaseChanged -= OnGamePhaseChanged;
        }

        private void BeginShipPlacement()
        {
            _mode = GameMode.Placement;
            messageField.text = "Place your Ships!";
            _shipsPlaced = 0;
            mapViewer.SetPlacementMode();
            UpdateCursor();
        }

        private void WaitForOpponentPlaceShips()
        {
            _mode = GameMode.Placement;
            mapViewer.SetDisabled();
            messageField.text = "Waiting for the Opponent to place the ships!";
        }

        private void WaitForOpponentTurn()
        {
            _mode = GameMode.Battle;
            mapViewer.SetDisabled();
            messageField.text = "Waiting for the Opponent to Attack!";
        }

        private void StartTurn()
        {
            _currentShot = 0;
            _mode = GameMode.Battle;
            mapViewer.SetAttackMode();
            messageField.text = "It's Your Turn!";
        }

        private void ShowResult()
        {
            _mode = GameMode.Result;
            mapViewer.SetDisabled();
            messageField.text = _state.winningPlayer == _myPlayerNumber ? "You Win!" : "You Lost!!!";
        }

        public void PlaceShip(Vector3Int coordinate)
        {
            if (_mode != GameMode.Placement) return;

            var shipWidth = 1;
            var shipHeight = 1;
            var shipType = (ShipType) _shipsPlaced;

            switch (shipType)
            {
                case ShipType.Admiral:
                    shipWidth = 3;
                    shipHeight = 3;
                    break;
                case ShipType.VCruiser:
                    shipHeight = 3;
                    break;
                case ShipType.HCruiser:
                    shipWidth = 3;
                    break;
                case ShipType.VGunBoat:
                    shipHeight = 2;
                    break;
                case ShipType.HGunBoat:
                    shipWidth = 2;
                    break;
            }

            if (coordinate.x < 1 || coordinate.x + (shipWidth - 1) >= mapSize ||
                coordinate.y - (shipHeight - 1) < 0) return;

            // x - 1, y - height > x + width, y + 1;
            var xMin = coordinate.x - 1;
            var xMax = coordinate.x + shipWidth;
            var yMin = coordinate.y - shipHeight;
            var yMax = coordinate.y + 1;

            for (var y = yMin; y <= yMax; y++)
            {
                if (y < 0 || y > mapSize - 2) continue;
                for (var x = xMin; x <= xMax; x++)
                {
                    if (x < 1 || x > mapSize - 1) continue;
                    if (!SetPlacementCell(new Vector3Int(x, y, 0), shipType, true)) return;
                }
            }

            switch (shipType)
            {
                case ShipType.Admiral:
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x + 2, coordinate.y, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x + 1, coordinate.y - 1, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y - 2, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x + 2, coordinate.y - 2, 0), shipType);
                    break;
                case ShipType.VCruiser:
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y - 1, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y - 2, 0), shipType);
                    break;
                case ShipType.HCruiser:
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x + 1, coordinate.y, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x + 2, coordinate.y, 0), shipType);
                    break;
                case ShipType.VGunBoat:
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y - 1, 0), shipType);
                    break;
                case ShipType.HGunBoat:
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y, 0), shipType);
                    SetPlacementCell(new Vector3Int(coordinate.x + 1, coordinate.y, 0), shipType);
                    break;
                default:
                    SetPlacementCell(new Vector3Int(coordinate.x, coordinate.y, 0), shipType);
                    break;
            }

            mapViewer.SetShip(shipType, coordinate);
            _shipsPlaced++;
            UpdateCursor();
            if (_shipsPlaced != 9) return;
            _client.SendPlacement(_placement);
            WaitForOpponentPlaceShips();
        }

        public void TakeTurn(Vector3Int coordinate)
        {
            var targetIndex = coordinate.y * (mapSize - 1) + (coordinate.x - 1);
            _shots[_currentShot] = targetIndex;
            if (_currentShot == 2) _client.SendTurn(_shots);

            _currentShot++;
        }

        private void UpdateCursor()
        {
            mapViewer.SetShipCursor((ShipType) _shipsPlaced);
        }

        private bool SetPlacementCell(Vector3Int coordinate, ShipType shipType, bool testOnly = false)
        {
            var cellIndex = coordinate.y * (mapSize - 1) + (coordinate.x - 1);

            if (cellIndex < 0 || cellIndex >= _cellCount) return false;
            if (_placement[cellIndex] >= 0) return false;
            if (testOnly) return true;

            _placement[cellIndex] = _shipsPlaced;
            return true;
        }

        private void OnInitialStateReceived(object sender, State initialState)
        {
            _state = initialState;
            var me = _state.players[_client.SessionId];

            _myPlayerNumber = me?.seat ?? -1;

            _state.OnChange += OnStateChanged;
            _state.player1Shots.OnChange += OnFirstPlayerShotsChanged;
            _state.player2Shots.OnChange += OnSecondPlayerShotsChanged;

            OnGamePhaseChanged(this, _state.phase);
        }

        private void OnStateChanged(object sender, OnChangeEventArgs args)
        {
            foreach (var unused in args.Changes.Where(change => change.Field == "playerTurn")) CheckTurn();
        }

        private void OnFirstPlayerShotsChanged(object sender, KeyValueEventArgs<short, int> change)
        {
            var marker = change.Value == 1 ? Marker.Hit : Marker.Miss;
            mapViewer.SetMarker(change.Key, marker, _myPlayerNumber == 1);
        }

        private void OnSecondPlayerShotsChanged(object sender, KeyValueEventArgs<short, int> change)
        {
            var marker = change.Value == 1 ? Marker.Hit : Marker.Miss;
            mapViewer.SetMarker(change.Key, marker, _myPlayerNumber == 2);
        }

        private void CheckTurn()
        {
            if (_state.playerTurn == _myPlayerNumber)
                StartTurn();
            else
                WaitForOpponentTurn();
        }

        private void Leave()
        {
            _client.Leave();
            SceneManager.LoadScene("ConnectingScene");
        }

        private void OnGamePhaseChanged(object sender, string phase)
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
    }
}