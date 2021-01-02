﻿using System;
using BattleshipGame.Core;
using BattleshipGame.Localization;
using UnityEngine;
using static BattleshipGame.Core.GameStateContainer.GameState;

namespace BattleshipGame.UI
{
    [RequireComponent(typeof(LocalizedText))]
    public class StatusTextController : MonoBehaviour
    {
        [SerializeField] private GameStateContainer gameStateContainer;
        [SerializeField] private Key statusSelectMode;
        [SerializeField] private Key statusConnecting;
        [SerializeField] private Key statusNetworkError;
        [SerializeField] private Key statusWaitingJoin;
        [SerializeField] private Key statusPlacementImpossible;
        [SerializeField] private Key statusPlacementReady;
        [SerializeField] private Key statusWaitingPlace;
        [SerializeField] private Key statusPlaceShips;
        [SerializeField] private Key statusWaitingDecision;
        [SerializeField] private Key statusWaitingAttack;
        [SerializeField] private Key statusPlayerTurn;
        private LocalizedText _statusText;

        private void Awake()
        {
            _statusText = GetComponent<LocalizedText>();
            gameStateContainer.StateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            gameStateContainer.StateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameStateContainer.GameState state)
        {
            switch (state)
            {
                case GameStart:
                    _statusText.SetText(statusSelectMode);
                    break;
                case MainMenu:
                    _statusText.ClearText();
                    break;
                case NetworkError:
                    _statusText.SetText(statusNetworkError);
                    break;
                case BeginLobby:
                    _statusText.ClearText();
                    break;
                case Connecting:
                    _statusText.SetText(statusConnecting);
                    break;
                case WaitingOpponentJoin:
                    _statusText.SetText(statusWaitingJoin);
                    break;
                case BeginPlacement:
                    _statusText.SetText(statusPlaceShips);
                    break;
                case PlacementImpossible:
                    _statusText.SetText(statusPlacementImpossible);
                    break;
                case PlacementReady:
                    _statusText.SetText(statusPlacementReady);
                    break;
                case WaitingOpponentPlacement:
                    _statusText.SetText(statusWaitingPlace);
                    break;
                case BeginBattle:
                    _statusText.ClearText();
                    break;
                case PlayerTurn:
                    _statusText.SetText(statusPlayerTurn);
                    break;
                case OpponentTurn:
                    _statusText.SetText(statusWaitingAttack);
                    break;
                case BattleResult:
                    _statusText.ClearText();
                    break;
                case WaitingOpponentRematchDecision:
                    _statusText.SetText(statusWaitingDecision);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}