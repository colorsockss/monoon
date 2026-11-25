using Engine.Core;
using Engine.Desktop;

var path = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "Scenes", "Showcase.scene");
using var game = new GameSession(SceneDocument.Load(path));
game.LoadFailed += Console.Error.WriteLine;
DesktopHost.Run("KeyRoom", game);
