using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Birdsong
{
    public sealed class GameUI : MonoBehaviour
    {
        static readonly Color Ink = new Color32(35, 58, 67, 255);
        static readonly Color Muted = new Color32(103, 124, 126, 255);
        static readonly Color Paper = new Color32(247, 243, 230, 255);
        static readonly Color Teal = new Color32(44, 119, 113, 255);
        static readonly Color Coral = new Color32(222, 112, 86, 255);
        static readonly Color Soft = new Color32(224, 234, 222, 255);
        GameController game;
        Canvas canvas;
        RectTransform safe, page, modal, boardArea;
        BoardManager board;
        Camera gameCamera;
        Transform tiles;
        Font font;
        Sprite round;
        Texture2D roundTexture;
        Text score, moves, feedback, soundLabel;
        readonly List<Text> goalTexts = new List<Text>();
        readonly List<Image> goalFills = new List<Image>();
        Coroutine feedbackRoutine;
        Rect previousSafe;
        Vector2 previousScreen;

        public void Initialize(GameController controller)
        {
            game = controller;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            round = RoundedSprite();
            var root = new GameObject("Birdsong UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            safe = Rect("Safe area", root.transform, 0, 0, 1, 1);
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            ApplySafeArea();
        }

        public void ConfigureBoard(BoardManager manager, Camera camera)
        {
            board = manager;
            gameCamera = camera;
            camera.backgroundColor = Paper;
            camera.orthographic = true;
            tiles = new GameObject("Board tiles").transform;
            tiles.SetParent(transform, false);
            for (int x = 0; x < board.width; x++) for (int y = 0; y < board.height; y++)
            {
                var tile = new GameObject("Cell " + x + "," + y, typeof(SpriteRenderer));
                tile.transform.SetParent(tiles, false);
                tile.transform.position = board.Position(x, y);
                var renderer = tile.GetComponent<SpriteRenderer>();
                renderer.sprite = round;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = new Vector2(board.offsetX * 0.94f, board.offsetY * 0.94f);
                renderer.color = (x + y) % 2 == 0 ? Soft : new Color32(231, 237, 226, 255);
                renderer.sortingOrder = -2;
            }
            tiles.gameObject.SetActive(false);
        }

        RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        Image Panel(string name, Transform parent, Color color, float x0, float y0, float x1, float y1, bool blocks = false)
        {
            var image = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<Image>();
            image.sprite = round;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = blocks;
            return image;
        }
        Text Label(string name, Transform parent, string value, int size, Color color,
            float x0, float y0, float x1, float y1, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }
        Button Button(string name, Transform parent, string value, Action action, Color color,
            float x0, float y0, float x1, float y1, bool enabled = true)
        {
            Image background = Panel(name, parent, color, x0, y0, x1, y1, true);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.interactable = enabled;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.97f, 0.94f);
            colors.pressedColor = new Color(0.8f, 0.89f, 0.85f);
            colors.disabledColor = new Color(1, 1, 1, 0.42f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label("Label", background.transform, value, 29, Color.white, 0.04f, 0.08f, 0.96f, 0.92f);
            button.onClick.AddListener(() => { game.Play(SoundCue.Button); action(); });
            return button;
        }
        void BirdIcon(Transform parent, int prefab, float x0, float y0, float x1, float y1)
        {
            if (game.board == null || game.board.birdPrefabs == null || prefab >= game.board.birdPrefabs.Length) return;
            var source = game.board.birdPrefabs[prefab];
            if (source == null || !source.TryGetComponent<SpriteRenderer>(out var renderer)) return;
            var image = Rect("Bird illustration", parent, x0, y0, x1, y1).gameObject.AddComponent<Image>();
            image.sprite = renderer.sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        void NewPage(string name, bool gameplay)
        {
            HideModal();
            if (feedbackRoutine != null) { StopCoroutine(feedbackRoutine); feedbackRoutine = null; }
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            page = Rect(name, safe, 0, 0, 1, 1);
            if (tiles != null) tiles.gameObject.SetActive(gameplay);
            goalTexts.Clear(); goalFills.Clear();
            soundLabel = null;
            feedback = null;
            boardArea = null;
        }
        void SaveWarning(Transform parent)
        {
            if (!string.IsNullOrEmpty(game.Progress?.Warning))
                Label("Save notice", parent, game.Progress.Warning, 18, Coral, 0.08f, 0.005f, 0.92f, 0.06f);
        }
        public void ShowMainMenu()
        {
            NewPage("Main menu", false);
            Label("Eyebrow", page, "A POCKET-SIZED PUZZLE", 20, Teal, 0.1f, 0.87f, 0.9f, 0.91f);
            Label("Title", page, "Birdsong", 82, Ink, 0.07f, 0.76f, 0.93f, 0.87f);
            Label("Subtitle", page, "A little flock. A lovely adventure.", 27, Muted, 0.08f, 0.71f, 0.92f, 0.76f);
            Panel("Illustration backdrop", page, Soft, 0.08f, 0.43f, 0.92f, 0.68f);
            BirdIcon(page, 0, 0.10f, 0.48f, 0.40f, 0.65f);
            BirdIcon(page, 2, 0.35f, 0.45f, 0.65f, 0.62f);
            BirdIcon(page, 3, 0.60f, 0.48f, 0.90f, 0.65f);
            Button("Play", page, "LET'S FLY", game.LevelSelect, Teal, 0.15f, 0.30f, 0.85f, 0.38f);
            Label("How to play", page, "Swap neighbors. Match 3 or more.\nHelp your flock reach its goal in six little chapters.", 24, Muted, 0.1f, 0.17f, 0.9f, 0.26f);
            var sound = Button("Sound", page, "", game.ToggleSound, Ink, 0.3f, 0.08f, 0.7f, 0.14f);
            soundLabel = sound.GetComponentInChildren<Text>(); RefreshSound();
            SaveWarning(page);
        }
        public void ShowLevelSelect()
        {
            NewPage("Level select", false);
            Label("Heading", page, "Your little journey", 48, Ink, 0.06f, 0.88f, 0.94f, 0.96f);
            Label("Subheading", page, "Six chapters, one happy flock", 24, Muted, 0.1f, 0.83f, 0.9f, 0.88f);
            for (int i = 0; i < game.Campaign.levels.Length; i++)
            {
                int index = i;
                bool open = i < game.Progress.Data.highestUnlocked;
                bool done = game.Progress.Data.completed[i];
                float top = 0.80f - i * 0.105f;
                var card = Panel("Level " + (i + 1), page, open ? Color.white : Soft, 0.06f, top - 0.092f, 0.94f, top, true);
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                button.interactable = open;
                button.onClick.AddListener(() => { game.Play(SoundCue.Button); game.StartLevel(index); });
                Label("Number", card.transform, (i + 1).ToString("00"), 40, open ? Teal : Muted, 0.03f, 0.08f, 0.19f, 0.94f);
                Label("Title", card.transform, game.Campaign.levels[i].title, 28, Ink, 0.23f, 0.44f, 0.94f, 0.9f, TextAnchor.MiddleLeft);
                string status = !open ? "LOCKED" : done ? "COMPLETE  /  Best " + game.Progress.Data.bestScores[i] : "READY TO PLAY";
                Label("Status", card.transform, status, 19, done ? Teal : Muted, 0.23f, 0.08f, 0.94f, 0.44f, TextAnchor.MiddleLeft);
            }
            Button("Home", page, "Back to home", game.MainMenu, Ink, 0.22f, 0.075f, 0.78f, 0.14f);
            SaveWarning(page);
        }
        public void ShowGameplay()
        {
            NewPage("Gameplay HUD", true);
            Label("Chapter", page, "CHAPTER " + (game.LevelIndex + 1) + " / 6", 22, Teal, 0.06f, 0.94f, 0.70f, 0.98f, TextAnchor.MiddleLeft);
            Label("Title", page, game.Session.Level.title, 38, Ink, 0.06f, 0.885f, 0.76f, 0.94f, TextAnchor.MiddleLeft);
            Button("Pause", page, "II", game.Pause, Ink, 0.81f, 0.9f, 0.94f, 0.97f);
            var scoreCard = Panel("Score card", page, Color.white, 0.06f, 0.802f, 0.63f, 0.876f);
            Label("Caption", scoreCard.transform, "SCORE", 18, Muted, 0.06f, 0.54f, 0.92f, 0.92f, TextAnchor.MiddleLeft);
            score = Label("Score", scoreCard.transform, "0", 37, Ink, 0.06f, 0.02f, 0.92f, 0.58f, TextAnchor.MiddleLeft);
            var movesCard = Panel("Moves card", page, Teal, 0.65f, 0.802f, 0.94f, 0.876f);
            moves = Label("Moves", movesCard.transform, "", 30, Color.white, 0.03f, 0.05f, 0.97f, 0.95f);
            var goals = Panel("Objectives", page, Soft, 0.06f, 0.682f, 0.94f, 0.79f);
            int count = game.Session.Level.objectives.Length;
            for (int i = 0; i < count; i++)
            {
                float bottom = 1f - (i + 1f) / count;
                goalTexts.Add(Label("Objective " + i, goals.transform, "", 23, Ink, 0.04f, bottom + 0.11f / count, 0.65f, bottom + 0.94f / count, TextAnchor.MiddleLeft));
                var track = Panel("Progress track", goals.transform, Color.white, 0.68f, bottom + 0.35f / count, 0.96f, bottom + 0.62f / count);
                var fill = Panel("Progress", track.transform, Teal, 0, 0, 0, 1);
                goalFills.Add(fill);
            }
            boardArea = Rect("Board viewport", page, 0.06f, 0.18f, 0.94f, 0.66f);
            feedback = Label("Match feedback", page, "", 28, Coral, 0.05f, 0.133f, 0.95f, 0.178f);
            Label("Instructions", page, "Swipe a bird, or tap two neighbors.\nOnly a successful match uses a move.", 22, Muted, 0.06f, 0.055f, 0.94f, 0.13f);
            RefreshHud();
            Canvas.ForceUpdateCanvases();
            FrameBoard();
        }
        public void RefreshHud()
        {
            if (game.Session == null || score == null) return;
            score.text = game.Session.Score.ToString("N0");
            moves.text = "MOVES\n" + game.Session.Moves;
            for (int i = 0; i < goalTexts.Count; i++)
            {
                var goal = game.Session.Level.objectives[i];
                int progress = game.Session.Progress(goal);
                goalTexts[i].text = goal.label + "  " + Mathf.Min(progress, goal.target) + " / " + goal.target;
                goalFills[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)progress / goal.target), 1);
            }
        }
        RectTransform NewModal(string title, string subtitle)
        {
            HideModal();
            modal = Rect("Modal", canvas.transform, 0, 0, 1, 1);
            Panel("Dimmer", modal, new Color(0.08f, 0.16f, 0.19f, 0.82f), 0, 0, 1, 1, true);
            var modalSafe = Rect("Modal safe area", modal, safe.anchorMin.x, safe.anchorMin.y, safe.anchorMax.x, safe.anchorMax.y);
            var card = Panel("Card", modalSafe, Paper, 0.07f, 0.19f, 0.93f, 0.82f, true).rectTransform;
            Label("Title", card, title, 46, Ink, 0.06f, 0.77f, 0.94f, 0.92f);
            Label("Details", card, subtitle, 28, Muted, 0.08f, 0.56f, 0.92f, 0.76f);
            return card;
        }
        public void ShowPause()
        {
            var card = NewModal("Take a breather", "Your flock will wait for you.");
            Button("Resume", card, "Resume", game.Resume, Teal, 0.1f, 0.43f, 0.9f, 0.54f);
            Button("Restart", card, "Restart level", game.Restart, Ink, 0.1f, 0.30f, 0.9f, 0.41f);
            var sound = Button("Sound", card, "", game.ToggleSound, Ink, 0.1f, 0.17f, 0.9f, 0.28f);
            soundLabel = sound.GetComponentInChildren<Text>(); RefreshSound();
            Button("Levels", card, "Level select", game.LevelSelect, Coral, 0.1f, 0.04f, 0.9f, 0.15f);
        }
        public void ShowResult(bool won)
        {
            bool finale = won && game.LevelIndex == game.Campaign.levels.Length - 1;
            string title = won ? finale ? "The flock is home!" : "Level complete!" : "Out of moves";
            string details = "Score  " + game.Session.Score.ToString("N0") + "\nBest  " + game.Progress.Data.bestScores[game.LevelIndex].ToString("N0");
            if (finale) details += "\nAll six chapters complete.";
            var card = NewModal(title, details);
            if (won && !finale) Button("Next", card, "Next level", game.NextLevel, Teal, 0.1f, 0.41f, 0.9f, 0.53f);
            Button("Replay", card, won ? "Replay" : "Retry", game.Restart, won ? Ink : Teal, 0.1f, 0.25f, 0.9f, 0.37f);
            Button("Levels", card, "Level select", game.LevelSelect, Coral, 0.1f, 0.09f, 0.9f, 0.21f);
            SaveWarning(card);
        }
        public void ShowError(string message)
        {
            if (game.board != null) game.board.SetState(GameState.Error);
            var card = NewModal("A little snag", message);
            Button("Levels", card, "Back to levels", game.LevelSelect, Ink, 0.1f, 0.12f, 0.9f, 0.26f);
        }
        public void HideModal()
        {
            if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
            modal = null;
            soundLabel = null;
        }
        public void RefreshSound() { if (soundLabel != null) soundLabel.text = game.Progress.Data.sound ? "Sound: ON" : "Sound: OFF"; }
        public void ShowFeedback(string text)
        {
            if (feedback == null) return;
            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(AnimateFeedback(text));
        }
        IEnumerator AnimateFeedback(string text)
        {
            feedback.text = text;
            for (float t = 0; t < 1.7f; t += Time.deltaTime)
            {
                feedback.transform.localScale = Vector3.one * (1 + 0.08f * Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI));
                yield return null;
            }
            feedback.text = "";
            feedback.transform.localScale = Vector3.one;
            feedbackRoutine = null;
        }
        void Update()
        {
            if (safe == null) return;
            if (Screen.safeArea != previousSafe || previousScreen != new Vector2(Screen.width, Screen.height))
            {
                ApplySafeArea();
                Canvas.ForceUpdateCanvases();
                FrameBoard();
            }
        }
        void ApplySafeArea()
        {
            previousSafe = Screen.safeArea;
            previousScreen = new Vector2(Screen.width, Screen.height);
            if (Screen.width == 0 || Screen.height == 0) return;
            safe.anchorMin = new Vector2(previousSafe.xMin / Screen.width, previousSafe.yMin / Screen.height);
            safe.anchorMax = new Vector2(previousSafe.xMax / Screen.width, previousSafe.yMax / Screen.height);
        }
        public void FrameBoard()
        {
            if (boardArea == null || gameCamera == null || board == null) return;
            var corners = new Vector3[4];
            boardArea.GetWorldCorners(corners);
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                for (int i = 0; i < corners.Length; i++) corners[i] = RectTransformUtility.WorldToScreenPoint(gameCamera, corners[i]);
            float pixelsW = corners[2].x - corners[0].x, pixelsH = corners[2].y - corners[0].y;
            if (pixelsW <= 0 || pixelsH <= 0) return;
            float unitsPerPixel = Mathf.Max(board.width * board.offsetX / pixelsW, board.height * board.offsetY / pixelsH);
            float pixelWidth = gameCamera.pixelWidth, pixelHeight = gameCamera.pixelHeight;
            gameCamera.orthographicSize = unitsPerPixel * pixelHeight * 0.5f;
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            gameCamera.transform.position = new Vector3((board.width - 1) * board.offsetX * 0.5f - (center.x - pixelWidth * 0.5f) * unitsPerPixel,
                (board.height - 1) * board.offsetY * 0.5f - (center.y - pixelHeight * 0.5f) * unitsPerPixel, -10);
        }
        Sprite RoundedSprite()
        {
            const int size = 64, radius = 12;
            roundTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            roundTexture.filterMode = FilterMode.Bilinear;
            roundTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            roundTexture.SetPixels(pixels); roundTexture.Apply();
            return Sprite.Create(roundTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
        void OnDestroy()
        {
            if (round != null) Destroy(round);
            if (roundTexture != null) Destroy(roundTexture);
        }
    }
}
