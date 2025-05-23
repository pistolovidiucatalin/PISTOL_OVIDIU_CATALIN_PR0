// GameRenderer.cs
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgColor = SixLabors.ImageSharp.Color;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using System;
using System.IO;
using System.Collections.Generic;
using PointF = SixLabors.ImageSharp.PointF;
using Silk.NET.Maths;
using TheAdventure.Models;
using SixLabors.ImageSharp.Drawing.Processing;

namespace TheAdventure
{
    public unsafe class GameRenderer
    {
        private Sdl _sdl;
        private Renderer* _renderer;
        private GameWindow _window;
        private Camera _camera;
        private Font _font;
        private Font _largeFont;
        private Font _stdFont;
        private Font _listFont;
        private DateTime _startTime;
        private Dictionary<int, IntPtr> _texturePointers = new();
        private Dictionary<int, TextureData> _textureData = new();
        private int _textureId;
        private int _timerPosX = 650, _timerPosY = 340;
        private bool _timerRunning = true;
        private int _frozenSeconds;

        private bool _showHighScoreList;
        public bool ShowHighScoreList
        {
            get => _showHighScoreList;
            set => _showHighScoreList = value;
        }

        private bool _showStartScreen = true;
        public bool ShowStartScreen
        {
            get => _showStartScreen;
            set => _showStartScreen = value;
        }

        private int _playX, _playY, _playW, _playH;
        private int _highX, _highY, _highW, _highH;
        private int _backX, _backY, _backW, _backH;
        private int _normalX, _normalY, _normalW, _normalH;
        private int _hardX, _hardY, _hardW, _hardH;

        public GameRenderer(Sdl sdl, GameWindow window)
        {
            _sdl = sdl;
            _renderer = (Renderer*)window.CreateRenderer();
            _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
            _window = window;

            var ws = window.Size;
            _camera = new Camera(ws.Width, ws.Height);

            var fonts = new FontCollection();
            var family = fonts.Add(System.IO.Path.Combine("Assets", "ARCADECLASSIC.TTF"));
            _font = family.CreateFont(50);
            _largeFont = family.CreateFont(80);
            _stdFont = family.CreateFont(30);
            _listFont = family.CreateFont(24);

            _startTime = DateTime.Now;
            _sdl.StartTextInput();
        }

        public void SetWorldBounds(Rectangle<int> bounds) => _camera.SetWorldBounds(bounds);
        public void CameraLookAt(int x, int y) => _camera.LookAt(x, y);
        public Vector2D<int> ToWorldCoordinates(int x, int y) => _camera.ToWorldCoordinates(new Vector2D<int>(x, y));
        public void SetDrawColor(byte r, byte g, byte b, byte a) => _sdl.SetRenderDrawColor(_renderer, r, g, b, a);
        public void ClearScreen() => _sdl.RenderClear(_renderer);

        public void StopTimer()
        {
            _frozenSeconds = (int)(DateTime.Now - _startTime).TotalSeconds;
            _timerRunning = false;
        }

        public void ResetTimer()
        {
            _startTime = DateTime.Now;
            _timerRunning = true;
            _frozenSeconds = 0;
            _showHighScoreList = false;
        }

        public int LoadTexture(string fileName, out TextureData textureInfo)
        {
            using var fs = new FileStream(fileName, FileMode.Open);
            var image = Image.Load<Rgba32>(fs);
            textureInfo = new TextureData { Width = image.Width, Height = image.Height };
            var raw = new byte[textureInfo.Width * textureInfo.Height * 4];
            image.CopyPixelDataTo(raw);

            fixed (byte* ptr = raw)
            {
                var surface = _sdl.CreateRGBSurfaceWithFormatFrom(
                    ptr,
                    textureInfo.Width,
                    textureInfo.Height,
                    32,
                    textureInfo.Width * 4,
                    (uint)PixelFormatEnum.Rgba32
                );
                var tex = _sdl.CreateTextureFromSurface(_renderer, surface);
                _sdl.FreeSurface(surface);
                _textureData[_textureId] = textureInfo;
                _texturePointers[_textureId] = (IntPtr)tex;
            }

            return _textureId++;
        }

        public void RenderTexture(int textureId, Rectangle<int> src, Rectangle<int> dst,
            RendererFlip flip = RendererFlip.None, double angle = 0.0, Silk.NET.SDL.Point center = default)
        {
            if (_texturePointers.TryGetValue(textureId, out var ptr))
            {
                var tex = (Texture*)ptr;
                var translated = _camera.ToScreenCoordinates(dst);
                _sdl.RenderCopyEx(_renderer, tex, in src, in translated, angle, in center, flip);
            }
        }

        public bool IsPlayButtonClicked(int x, int y)
            => !_timerRunning && !_showHighScoreList
               && x >= _playX && x <= _playX + _playW
               && y >= _playY && y <= _playY + _playH;

        public bool IsHighScoreClicked(int x, int y)
            => !_timerRunning && !_showHighScoreList
               && x >= _highX && x <= _highX + _highW
               && y >= _highY && y <= _highY + _highH;

        public bool IsBackClicked(int x, int y)
            => !_timerRunning && _showHighScoreList
               && x >= _backX && x <= _backX + _backW
               && y >= _backY && y <= _backY + _backH;

        public bool IsNormalClicked(int x, int y)
            => _showStartScreen
               && x >= _normalX && x <= _normalX + _normalW
               && y >= _normalY && y <= _normalY + _normalH;

        public bool IsHardClicked(int x, int y)
            => _showStartScreen
               && x >= _hardX && x <= _hardX + _hardW
               && y >= _hardY && y <= _hardY + _hardH;

        public void PresentFrame()
        {
            if (_showStartScreen)
            {
                DrawStartMenu();
                _sdl.RenderPresent(_renderer);
                return;
            }

            DrawTime();
            if (!_timerRunning)
            {
                if (_showHighScoreList)
                    DrawHighScoreList();
                else
                    DrawDeathUI();
            }
            _sdl.RenderPresent(_renderer);
        }

        private void DrawStartMenu()
        {
            var ws = _window.Size;
            DrawButton("NORMAL MODE", ws.Width / 2, ws.Height / 2 - 40, out _normalX, out _normalY, out _normalW, out _normalH);
            DrawButton("HARD MODE", ws.Width / 2, ws.Height / 2 + 40, out _hardX, out _hardY, out _hardW, out _hardH);
        }

        private void DrawDeathUI()
        {
            var ws = _window.Size;
            var overlay = new Rectangle<int>(0, 0, ws.Width, ws.Height);
            _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
            _sdl.SetRenderDrawColor(_renderer, 128, 128, 128, 180);
            _sdl.RenderFillRect(_renderer, in overlay);

            DrawTextCentered("GAME OVER", ws.Width / 2, 80, _largeFont, ImgColor.White);
            DrawTextCentered($"SCORE {_frozenSeconds}", ws.Width / 2, 160, _stdFont, ImgColor.White);

            DrawButton("HIGH SCORES", ws.Width / 2, 250, out _highX, out _highY, out _highW, out _highH);
            DrawButton("PLAY AGAIN", ws.Width / 2, 300, out _playX, out _playY, out _playW, out _playH);
        }

        private void DrawHighScoreList()
        {
            var ws = _window.Size;
            var overlay = new Rectangle<int>(0, 0, ws.Width, ws.Height);
            _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
            _sdl.SetRenderDrawColor(_renderer, 0, 0, 0, 200);
            _sdl.RenderFillRect(_renderer, in overlay);

            DrawTextCentered("TOP 5 PLAYERS", ws.Width / 2, 60, _largeFont, ImgColor.White);

            var list = DatabaseManager.GetTopScores(5);
            int startX = 100, startY = 140, lineHeight = 32;
            for (int i = 0; i < list.Count; i++)
            {
                var (name, score) = list[i];
                var entry = $"{i + 1} {name.PadRight(10)} {score}";
                DrawTextLeft(entry, startX, startY + i * lineHeight, _listFont, ImgColor.White);
            }

            DrawButton("BACK", ws.Width / 2, 300, out _backX, out _backY, out _backW, out _backH);
        }

        private void DrawTextCentered(string text, int cx, int cy, Font font, ImgColor color)
        {
            var opts = new TextOptions(font) { Origin = new PointF(0, 0) };
            var sz = TextMeasurer.MeasureSize(text, opts);
            int w = (int)Math.Ceiling(sz.Width), h = (int)Math.Ceiling(sz.Height);

            using var img = new Image<Rgba32>(w, h, ImgColor.Transparent);
            img.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(0, 0)));
            var raw = new byte[w * h * 4];
            img.CopyPixelDataTo(raw);

            fixed (byte* ptr = raw)
            {
                var surf = _sdl.CreateRGBSurfaceWithFormatFrom(
                    ptr, w, h, 32, w * 4, (uint)PixelFormatEnum.Rgba32);
                var tex = _sdl.CreateTextureFromSurface(_renderer, surf);
                _sdl.FreeSurface(surf);

                var dst = new Rectangle<int>(cx - w / 2, cy - h / 2, w, h);
                _sdl.RenderCopy(_renderer, tex, null, in dst);
                _sdl.DestroyTexture(tex);
            }
        }

        private void DrawTextLeft(string text, int x, int y, Font font, ImgColor color)
        {
            var opts = new TextOptions(font) { Origin = new PointF(0, 0) };
            var sz = TextMeasurer.MeasureSize(text, opts);
            int w = (int)Math.Ceiling(sz.Width), h = (int)Math.Ceiling(sz.Height);

            using var img = new Image<Rgba32>(w, h, ImgColor.Transparent);
            img.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(0, 0)));
            var raw = new byte[w * h * 4];
            img.CopyPixelDataTo(raw);

            fixed (byte* ptr = raw)
            {
                var surf = _sdl.CreateRGBSurfaceWithFormatFrom(
                    ptr, w, h, 32, w * 4, (uint)PixelFormatEnum.Rgba32);
                var tex = _sdl.CreateTextureFromSurface(_renderer, surf);
                _sdl.FreeSurface(surf);

                var dst = new Rectangle<int>(x, y, w, h);
                _sdl.RenderCopy(_renderer, tex, null, in dst);
                _sdl.DestroyTexture(tex);
            }
        }

        private void DrawButton(string text, int cx, int cy, out int bx, out int by, out int bw, out int bh)
        {
            var opts = new TextOptions(_font) { Origin = new PointF(0, 0) };
            var sz = TextMeasurer.MeasureSize(text, opts);
            int tw = (int)Math.Ceiling(sz.Width), th = (int)Math.Ceiling(sz.Height);
            bw = tw + 20; bh = th + 10;
            bx = cx - bw / 2; by = cy - bh / 2;

            var rect = new Rectangle<int>(bx, by, bw, bh);
            _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
            _sdl.SetRenderDrawColor(_renderer, 0, 0, 0, 200);
            _sdl.RenderFillRect(_renderer, in rect);
            _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
            _sdl.SetRenderDrawColor(_renderer, 255, 255, 255, 255);
            _sdl.RenderDrawRect(_renderer, in rect);

            DrawTextCentered(text, cx, cy, _font, ImgColor.White);
        }

        private void DrawTime()
        {
            var secs = _timerRunning
                ? (int)(DateTime.Now - _startTime).TotalSeconds
                : _frozenSeconds;
            var t = secs.ToString();

            var opts = new TextOptions(_font) { Origin = new PointF(0, 0) };
            var sz = TextMeasurer.MeasureSize(t, opts);
            int pad = 16,
                w = (int)Math.Ceiling(sz.Width) + pad * 2,
                h = (int)Math.Ceiling(sz.Height) + pad * 2;

            using var img = new Image<Rgba32>(w, h, ImgColor.Transparent);
            img.Mutate(ctx => ctx.DrawText(t, _font, ImgColor.White, new PointF(pad, pad)));
            var raw = new byte[w * h * 4];
            img.CopyPixelDataTo(raw);

            fixed (byte* ptr = raw)
            {
                var surf = _sdl.CreateRGBSurfaceWithFormatFrom(
                    ptr, w, h, 32, w * 4, (uint)PixelFormatEnum.Rgba32);
                var tex = _sdl.CreateTextureFromSurface(_renderer, surf);
                _sdl.FreeSurface(surf);

                int x = _timerPosX - w, y = _timerPosY;
                var dst = new Rectangle<int>(x, y, w, h);
                _sdl.RenderCopy(_renderer, tex, null, in dst);
                _sdl.DestroyTexture(tex);
            }
        }
    }
}
