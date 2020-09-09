﻿using System.Collections.Generic;
using BattleshipGame.Common;
using BattleshipGame.Core;
using BattleshipGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.Tilemaps;
using static BattleshipGame.Common.MapInteractionMode;

namespace BattleshipGame.TilePaint
{
    public class TurnHighlighter : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Rules rules;
        [SerializeField] private BattleManager manager;
        [SerializeField] private BattleMap battleMap;
        [SerializeField] private Tilemap layer;
        [SerializeField] private Tile tile;

        private Grid _grid;
        private OpponentStatus _status;

        private void Start()
        {
            _grid = GetComponent<Grid>();
            _status = GetComponent<OpponentStatus>();
        }

        private void OnMouseDown()
        {
            if (battleMap.InteractionMode != TurnHighlighting && battleMap.InteractionMode != TargetMarking) return;
            var coordinate = GridUtils.ScreenToCoordinate(Input.mousePosition, _grid, sceneCamera, rules.AreaSize);
            if (_status)
            {
                int shotTurn = _status.GetShotTurn(coordinate);
                manager.HighlightTurn(shotTurn);
            }
            else
            {
                manager.HighlightShotsInTheSameTurn(coordinate);
            }
        }

        public void HighlightTurns(IEnumerable<int> cells)
        {
            layer.ClearAllTiles();
            foreach (int cell in cells)
                layer.SetTile(GridUtils.CellIndexToCoordinate(cell, rules.AreaSize.x), tile);
        }
    }
}