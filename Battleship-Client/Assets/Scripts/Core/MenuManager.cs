﻿using System.Collections;
using BattleshipGame.AI;
using BattleshipGame.Common;
using BattleshipGame.Localization;
using BattleshipGame.UI;
using UnityEditor;
using UnityEngine;

namespace BattleshipGame.Core
{
    public class MenuManager : MonoBehaviour
    {
        [SerializeField] private Key popupSingleHeader;
        [SerializeField] private Key popupSingleMessage;
        [SerializeField] private Key popupSingleConfirm;
        [SerializeField] private Key popupSingleDecline;
        [SerializeField] private Key statusNetworkError;
        [SerializeField] private Key cancel;
        [SerializeField] private ColorVariable cancelColor;
        [SerializeField] private ButtonController quitButton;
        [SerializeField] private ButtonController singlePlayerButton;
        [SerializeField] private ButtonController multiplayerButton;
        [SerializeField] private ButtonController optionsButton;
        [SerializeField] private Canvas optionsCanvas;
        [SerializeField] private Options options;
        [SerializeField] private GameObject popUpPrefab;
        [SerializeField] private GameObject progressBarCanvasPrefab;
        private GameObject _progressBar;
        private bool _isConnecting;
        private bool _isConnectionCanceled;

        private void Start()
        {
            quitButton.AddListener(Quit);
            singlePlayerButton.AddListener(PlayAgainstAI);
            multiplayerButton.AddListener(PlayWithFriends);
            optionsButton.AddListener(() => { optionsCanvas.enabled = true; });

            void Quit()
            {
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
                Application.OpenURL(webQuitPage);
#else
                Application.Quit();
#endif
            }

            void PlayAgainstAI()
            {
                BuildPopUp().Show(popupSingleHeader, popupSingleMessage, popupSingleConfirm, popupSingleDecline,
                    OnEasyMode, OnHardMode);

                void OnEasyMode()
                {
                    options.aiDifficulty = Difficulty.Easy;
                    StartLocalRoom();
                }

                void OnHardMode()
                {
                    options.aiDifficulty = Difficulty.Hard;
                    StartLocalRoom();
                }

                void StartLocalRoom()
                {
                    if (!GameManager.TryGetInstance(out var gameManager)) return;
                    gameManager.StartLocalClient();
                }
            }

            void PlayWithFriends()
            {
                if (!GameManager.TryGetInstance(out var gameManager)) return;

                if (_isConnecting)
                {
                    _isConnectionCanceled = true;
                    multiplayerButton.SetInteractable(false);
                    ResetMenu();
                }
                else
                {
                    _isConnecting = true;
                    singlePlayerButton.SetInteractable(false);
                    optionsButton.SetInteractable(false);
                    quitButton.SetInteractable(false);
                    multiplayerButton.ChangeText(cancel);
                    multiplayerButton.ChangeColor(cancelColor);
                    if (progressBarCanvasPrefab) _progressBar = Instantiate(progressBarCanvasPrefab);
                    gameManager.ConnectToServer(() =>
                    {
                        _isConnecting = false;
                        if (_isConnectionCanceled)
                        {
                            StartCoroutine(FinishNetworkClient());
                        }
                        else
                        {
                            GameSceneManager.Instance.GoToLobby();
                        }
                    }, errorMessage =>
                    {
                        _isConnecting = false;
                        _isConnectionCanceled = false;
                        gameManager.SetStatusText(statusNetworkError);
                        Debug.LogError(errorMessage);
                        ResetMenu();
                    });
                }

                void ResetMenu()
                {
                    singlePlayerButton.SetInteractable(true);
                    optionsButton.SetInteractable(true);
                    quitButton.SetInteractable(true);
                    multiplayerButton.ResetText();
                    multiplayerButton.ResetColor();
                    Destroy(_progressBar);
                    gameManager.ClearStatusText();
                }

                IEnumerator FinishNetworkClient()
                {
                    yield return new WaitForSecondsRealtime(1);
                    gameManager.FinishNetworkClient();
                    _isConnectionCanceled = false;
                    multiplayerButton.SetInteractable(true);
                }
            }
        }

        private PopUpWindow BuildPopUp()
        {
            return Instantiate(popUpPrefab).GetComponent<PopUpWindow>();
        }
    }
}