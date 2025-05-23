using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Silk.NET.Maths;
using TheAdventure.Models;
using TheAdventure.Models.Data;
using TheAdventure.Scripting;

namespace TheAdventure
{
    public class Engine
    {
        private readonly GameRenderer _renderer;
        private readonly Input _input;
        private readonly ScriptEngine _scriptEngine = new();
        private readonly Dictionary<int, GameObject> _gameObjects = new();
        private readonly Dictionary<string, TileSet> _loadedTileSets = new();
        private readonly Dictionary<int, Tile> _tileIdMap = new();

        private Level _currentLevel = new();
        private PlayerObject? _player;

        private DateTimeOffset _lastUpdate = DateTimeOffset.Now;
        private DateTimeOffset _startTime;

        private bool _dead;
        private int _currentScore;

        public bool HardMode { get; set; }
        public int PlayerX => _player?.Position.X ?? 0;
        public int PlayerY => _player?.Position.Y ?? 0;

        public Engine(GameRenderer renderer, Input input)
        {
            _renderer = renderer;
            _input = input;
            _input.OnMouseClick += OnMouseClick;
        }

        private void OnMouseClick(object? sender, (int x, int y) coords)
        {
            if (_renderer.ShowStartScreen)
            {
                if (_renderer.IsNormalClicked(coords.x, coords.y))
                {
                    HardMode = false;
                    _renderer.ShowStartScreen = false;
                    SetupWorld();
                }
                else if (_renderer.IsHardClicked(coords.x, coords.y))
                {
                    HardMode = true;
                    _renderer.ShowStartScreen = false;
                    SetupWorld();
                }
                return;
            }

            if (!_dead)
            {
                AddBomb(coords.x, coords.y);
                return;
            }

            if (!_renderer.ShowHighScoreList && _renderer.IsHighScoreClicked(coords.x, coords.y))
            {
                _renderer.ShowHighScoreList = true;
            }
            else if (_renderer.ShowHighScoreList && _renderer.IsBackClicked(coords.x, coords.y))
            {
                _renderer.ShowHighScoreList = false;
            }
            else if (_renderer.IsPlayButtonClicked(coords.x, coords.y))
            {
                Restart();
            }
        }

        public void SetupWorld()
        {
            _player = new PlayerObject(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);

            _gameObjects.Clear();
            _tileIdMap.Clear();
            _loadedTileSets.Clear();

            string json = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));
            _currentLevel = JsonSerializer.Deserialize<Level>(json) ?? throw new Exception("Failed to load level");

            foreach (var tsRef in _currentLevel.TileSets)
            {
                string tsJson = File.ReadAllText(Path.Combine("Assets", tsRef.Source));
                var ts = JsonSerializer.Deserialize<TileSet>(tsJson) ?? throw new Exception("Failed to load tile set");
                foreach (var tile in ts.Tiles)
                {
                    tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                    _tileIdMap[tile.Id!.Value] = tile;
                }
                _loadedTileSets[ts.Name] = ts;
            }

            _renderer.SetWorldBounds(new Rectangle<int>(
                0,
                0,
                _currentLevel.Width!.Value * _currentLevel.TileWidth!.Value,
                _currentLevel.Height!.Value * _currentLevel.TileHeight!.Value));

            _scriptEngine.LoadAll(Path.Combine("Assets", "Scripts"));

            _dead = false;
            _lastUpdate = _startTime = DateTimeOffset.Now;
            _renderer.ResetTimer();
        }

        public void ProcessFrame()
        {
            if (_renderer.ShowStartScreen || _dead || _player == null) return;

            var now = DateTimeOffset.Now;
            double dt = (now - _lastUpdate).TotalMilliseconds;
            _lastUpdate = now;

            double up = _input.IsUpPressed() ? 1 : 0;
            double down = _input.IsDownPressed() ? 1 : 0;
            double left = _input.IsLeftPressed() ? 1 : 0;
            double right = _input.IsRightPressed() ? 1 : 0;

            bool isAttacking = _input.IsKeyAPressed() && (up + down + left + right <= 1);
            bool addBomb = _input.IsKeyBPressed();

            _player.UpdatePosition(up, down, left, right, 48, 48, dt);
            ClampPlayerToWorld();

            if (isAttacking) _player.Attack();

            _scriptEngine.ExecuteAll(this);

            if (addBomb) AddBomb(_player.Position.X, _player.Position.Y, false);
        }

        private void ClampPlayerToWorld()
        {
            if (_player == null) return;

            int maxX = _currentLevel.Width!.Value * _currentLevel.TileWidth!.Value - 32;
            int maxY = _currentLevel.Height!.Value * _currentLevel.TileHeight!.Value - 32;

            int clampedX = Math.Clamp(_player.Position.X, 0, maxX);
            int clampedY = Math.Clamp(_player.Position.Y, 0, maxY);

            _player.Position = (clampedX, clampedY);
        }

        public void RenderFrame()
        {
            _renderer.SetDrawColor(0, 0, 0, 255);
            _renderer.ClearScreen();

            if (_renderer.ShowStartScreen)
            {
                _renderer.PresentFrame();
                return;
            }

            if (_dead)
            {
                _renderer.PresentFrame();
                return;
            }

            if (_player != null)
            {
                var p = _player.Position;
                _renderer.CameraLookAt(p.X, p.Y);
                RenderTerrain();
                RenderAllObjects();
            }

            _renderer.PresentFrame();
        }

        private void RenderAllObjects()
        {
            var toRemove = new List<int>();

            foreach (var obj in GetRenderables())
            {
                obj.Render(_renderer);
                if (obj is TemporaryGameObject { IsExpired: true } t)
                {
                    toRemove.Add(t.Id);
                }
            }

            foreach (var id in toRemove)
            {
                _gameObjects.Remove(id, out var go);

                if (_player == null || go == null) continue;

                var t = (TemporaryGameObject)go;
                if (Math.Abs(_player.Position.X - t.Position.X) < 32 &&
                    Math.Abs(_player.Position.Y - t.Position.Y) < 32)
                {
                    _player.GameOver();
                    _renderer.StopTimer();
                    _currentScore = (int)(DateTimeOffset.Now - _startTime).TotalSeconds;

                    int high = DatabaseManager.GetHighestScore();

                    if (_currentScore > high)
                    {
                        Console.Write("New High Score! Enter your name: ");
                        string? name = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            DatabaseManager.InsertScore(name, _currentScore);
                        }
                    }

                    _dead = true;
                }
            }

            _player?.Render(_renderer);
        }

        private void RenderTerrain()
        {
            foreach (var layer in _currentLevel.Layers)
            {
                int width = layer.Width!.Value;
                int height = layer.Height!.Value;

                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        int idx = j * width + i;
                        int gid = (int)(layer.Data[idx] - 1);
                        if (gid < 0) continue;

                        var tile = _tileIdMap[gid];
                        int tw = tile.ImageWidth!.Value;
                        int th = tile.ImageHeight!.Value;

                        _renderer.RenderTexture(
                            tile.TextureId,
                            new Rectangle<int>(0, 0, tw, th),
                            new Rectangle<int>(i * tw, j * th, tw, th));
                    }
                }
            }
        }

        public void AddBomb(int X, int Y, bool translateCoordinates = true)
        {
            if (_dead) return;

            var worldCoords = translateCoordinates
                ? _renderer.ToWorldCoordinates(X, Y)
                : new Vector2D<int>(X, Y);

            var spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
            spriteSheet.ActivateAnimation("Explode");

            var bomb = new TemporaryGameObject(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
            _gameObjects[bomb.Id] = bomb;
        }

        public void Restart()
        {
            _dead = false;
            _renderer.ShowHighScoreList = false;
            _renderer.ShowStartScreen = true;
        }

        private IEnumerable<RenderableGameObject> GetRenderables()
        {
            foreach (var go in _gameObjects.Values)
            {
                if (go is RenderableGameObject r) yield return r;
            }
        }

        public (int X, int Y) GetPlayerPosition()
        {
            return _player?.Position ?? (0, 0);
        }
    }
}
