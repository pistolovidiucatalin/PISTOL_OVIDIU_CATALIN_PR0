using System;
using Silk.NET.SDL;
using Thread = System.Threading.Thread;
using SQLitePCL;

namespace TheAdventure
{
    public static class Program
    {
        public static void Main()
        {
            Batteries_V2.Init();
            DatabaseManager.PurgeScores();

            var sdl = new Sdl(new SdlContext());
            if (sdl.Init(Sdl.InitVideo | Sdl.InitAudio | Sdl.InitEvents |
                         Sdl.InitTimer | Sdl.InitGamecontroller |
                         Sdl.InitJoystick) < 0)
            {
                throw new InvalidOperationException("Failed to initialise SDL.");
            }

            using var gameWindow = new GameWindow(sdl);
            var input = new Input(sdl);
            var gameRenderer = new GameRenderer(sdl, gameWindow);
            var engine = new Engine(gameRenderer, input);
            engine.SetupWorld();

            var quit = false;
            while (!quit)
            {
                quit = input.ProcessInput();
                if (quit) break;

                engine.ProcessFrame();
                engine.RenderFrame();
                Thread.Sleep(13);
            }

            sdl.Quit();
        }
    }
}
