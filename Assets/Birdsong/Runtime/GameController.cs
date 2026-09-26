using System;
using System.Collections.Generic;
using UnityEngine;

namespace Birdsong
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameController : MonoBehaviour
    {
        public BoardManager board;
        public InputManager input;
        public Camera gameCamera;
        public TextAsset campaignData;
        public GameAudio audioPlayer;
        public GameUI ui;
        public Campaign Campaign { get; private set; }
        public ProgressStore Progress { get; private set; }
        public LevelSession Session { get; private set; }
        public int LevelIndex { get; private set; }
        public bool InGameplay { get; private set; }
        string configurationError;

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Time.timeScale = 1;
            Progress = new ProgressStore(new PlayerPreferences());
            if (ui == null) ui = gameObject.AddComponent<GameUI>();
            if (audioPlayer == null) audioPlayer = gameObject.AddComponent<GameAudio>();
            audioPlayer.SoundEnabled = Progress.Data.sound;
            ui.Initialize(this);
            if (board == null || input == null || gameCamera == null)
            { configurationError = "Missing Board, Input or Camera reference on GameController."; ui.ShowError(configurationError); return; }
            if (campaignData == null) campaignData = Resources.Load<TextAsset>("Campaign");
            try { Campaign = campaignData != null ? JsonUtility.FromJson<Campaign>(campaignData.text) : null; }
            catch (ArgumentException e) { configurationError = e.Message; }
            configurationError = configurationError ?? board.ValidateConfiguration() ??
                (Campaign == null ? "Campaign configuration is missing." : Campaign.Validate(board.AvailableColors()));
            if (configurationError != null) { Debug.LogError(configurationError); ui.ShowError(configurationError); return; }
            input.Configure(board, gameCamera);
            input.Selected += OnSelected;
            board.SwapAccepted += OnSwapAccepted;
            board.MatchesResolved += OnMatches;
            board.Settled += OnSettled;
            board.SoundRequested += Play;
            board.Feedback += ui.ShowFeedback;
            board.Failed += ui.ShowError;
            ui.ConfigureBoard(board, gameCamera);
            MainMenu();
        }
        void OnSelected() => Play(SoundCue.Select);
        public void Play(SoundCue cue) { if (audioPlayer != null) audioPlayer.Play(cue); }
        void OnSwapAccepted()
        {
            Session.ConsumeMove();
            ui.RefreshHud();
        }
        void OnMatches(List<int> sizes, Dictionary<int, int> colors, int cascade)
        {
            int points = Session.Award(sizes, colors, cascade);
            ui.RefreshHud();
            ui.ShowFeedback(cascade > 1 ? "Lovely cascade!  x" + cascade + "   +" + points : "+" + points + "  Happy birds!");
        }
        void OnSettled()
        {
            if (Session == null || !InGameplay || (!Session.Won && !Session.Lost)) return;
            bool won = Session.Won;
            board.SetState(won ? GameState.Won : GameState.Lost);
            input.CancelSelection();
            Progress.Record(LevelIndex, Session.Score, won);
            Play(won ? SoundCue.Win : SoundCue.Lose);
            ui.ShowResult(won);
        }
        public void StartLevel(int index)
        {
            if (configurationError != null || Campaign == null || index < 0 || index >= Campaign.levels.Length ||
                index >= Progress.Data.highestUnlocked) return;
            Time.timeScale = 1;
            input.CancelSelection();
            LevelIndex = index;
            Session = new LevelSession(Campaign.levels[index], Campaign.scoring);
            InGameplay = true;
            ui.ShowGameplay();
            board.BeginLevel();
        }
        public void Restart() => StartLevel(LevelIndex);
        public void NextLevel()
        {
            if (board.State == GameState.Won && LevelIndex + 1 < Campaign.levels.Length) StartLevel(LevelIndex + 1);
        }
        void LeaveGameplay()
        {
            Time.timeScale = 1;
            InGameplay = false;
            if (input != null) input.CancelSelection();
            if (board != null) board.ClearBoard();
            Session = null;
            Progress?.Save();
        }
        public void MainMenu()
        {
            LeaveGameplay();
            ui.ShowMainMenu();
        }
        public void LevelSelect()
        {
            if (configurationError != null) { ui.ShowError(configurationError); return; }
            LeaveGameplay();
            ui.ShowLevelSelect();
        }
        public void Pause()
        {
            if (!InGameplay || board.State == GameState.Paused || board.State == GameState.Won || board.State == GameState.Lost || board.State == GameState.Error) return;
            input.CancelSelection();
            board.Pause();
            Time.timeScale = 0;
            Progress.Save();
            ui.ShowPause();
        }
        public void Resume()
        {
            if (board.State != GameState.Paused) return;
            Time.timeScale = 1;
            board.Resume();
            ui.HideModal();
        }
        public void ToggleSound()
        {
            Progress.SetSound(!Progress.Data.sound);
            audioPlayer.SoundEnabled = Progress.Data.sound;
            ui.RefreshSound();
            if (audioPlayer.SoundEnabled) Play(SoundCue.Button);
        }
        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape) || board == null) return;
            if (board.State == GameState.Paused) Resume();
            else if (InGameplay) Pause();
            else MainMenu();
        }
        void OnApplicationPause(bool paused) { if (paused) { Pause(); Progress?.Save(); } }
        void OnApplicationFocus(bool focused) { if (!focused) Pause(); }
        void OnApplicationQuit() => Progress?.Save();
        void OnDestroy()
        {
            Time.timeScale = 1;
            if (input != null) input.Selected -= OnSelected;
            if (board == null) return;
            board.SwapAccepted -= OnSwapAccepted;
            board.MatchesResolved -= OnMatches;
            board.Settled -= OnSettled;
            board.SoundRequested -= Play;
            if (ui != null) { board.Feedback -= ui.ShowFeedback; board.Failed -= ui.ShowError; }
        }
        [ContextMenu("Development/Reset Birdsong Progress")]
        void ResetProgress()
        {
            if (!Application.isPlaying || (!Application.isEditor && !Debug.isDebugBuild)) return;
            Progress.ResetProgress();
            MainMenu();
        }
    }
}
