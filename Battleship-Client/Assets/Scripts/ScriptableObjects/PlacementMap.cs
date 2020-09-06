﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattleshipGame.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Placement Map", menuName = "Battleship/Placement Map", order = 3)]
    public class PlacementMap : ScriptableObject
    {
        [SerializeField] private List<Placement> placements = new List<Placement>();

        public void PlaceShip(int shipIndex, Ship ship, Vector3Int coordinate)
        {
            int index = -1;
            for (var i = 0; i < placements.Count; i++)
            {
                if (!placements[i].shipIndex.Equals(shipIndex)) continue;
                index = shipIndex;
                break;
            }

            if (index > -1)
            {
                var placement = placements[index];
                placement.Coordinate = coordinate;
                placements[index] = placement;
            }
            else
            {
                placements.Add(new Placement(shipIndex, ship, coordinate));
            }
        }
        
        public List<Placement> GetPlacements()
        {
            return placements.ToList();
        }

        public void Clear()
        {
            placements.Clear();
        }

        [Serializable]
        public struct Placement
        {
            public int shipIndex;
            public Ship ship;
            public Vector3Int Coordinate;

            public Placement(int shipIndex, Ship ship, Vector3Int coordinate)
            {
                this.shipIndex = shipIndex;
                this.ship = ship;
                Coordinate = coordinate;
            }
        }
    }
}