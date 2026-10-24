using System;
using System.Collections.Generic;
using RetroArcade.Games;

namespace RetroArcade
{
    /// <summary>Chiavi delle scene: le stesse stringhe usate da <c>scene.start('...')</c> in Phaser.</summary>
    public static class SceneKeys
    {
        public const string Launcher = "LauncherScene";
        public const string TicTacToe = "TicTacToeScene";
        public const string Snake = "SnakeScene";
        public const string Tetris = "TetrisScene";
        public const string Pong = "PongScene";
        public const string Breakout = "BreakoutScene";
        public const string SpaceInvaders = "SpaceInvadersScene";
        public const string MazeRunner = "MazeRunnerScene";
        public const string MemoryGame = "MemoryGameScene";
        public const string Puzzle15 = "Puzzle15Scene";
    }

    /// <summary>
    /// Equivalente dell'array <c>scene: [...]</c> in <c>main.js</c>.
    /// Per aggiungere un decimo gioco: crea la classe, registrala qui e aggiungi la tessera nel Launcher.
    /// </summary>
    public static class SceneRegistry
    {
        public static readonly Dictionary<string, Func<ArcadeScene>> Factories = new Dictionary<string, Func<ArcadeScene>>
        {
            [SceneKeys.Launcher] = () => new LauncherScene(),
            [SceneKeys.TicTacToe] = () => new TicTacToeScene(),
            [SceneKeys.Snake] = () => new SnakeScene(),
            [SceneKeys.Tetris] = () => new TetrisScene(),
            [SceneKeys.Pong] = () => new PongScene(),
            [SceneKeys.Breakout] = () => new BreakoutScene(),
            [SceneKeys.SpaceInvaders] = () => new SpaceInvadersScene(),
            [SceneKeys.MazeRunner] = () => new MazeRunnerScene(),
            [SceneKeys.MemoryGame] = () => new MemoryGameScene(),
            [SceneKeys.Puzzle15] = () => new Puzzle15Scene(),
        };
    }
}
