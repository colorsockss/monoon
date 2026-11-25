using System.Drawing;
using System.Numerics;
using Engine.Core;
using Engine.Editor;

// Undo records keep only the object entries an edit changed. Random mixed edits (move, rename, sprite fields, add, delete,
// duplicate, reparent, reorder, start camera, settings, grouped typing, multi-selection, prefab place/revert/apply, tile
// strokes, object active and component enabled toggles, component Reset / Paste Values / Move / Remove, colour picks (13.9),
// object-field drop / clear / replace and multi-selection drops (13.7/13.17), Inspector particle curve and gradient edits written
// to the .particles file (13.15), multi-selection field commits (13.5), an Inspector row Revert (13.6) and a label scrub recorded once
// (13.10), script list / dictionary / struct value edits (13.8), Input Actions interactions and vectors and Audio Mixer snapshots and
// ducking saved to GameSettings (16.12/16.10)) must give back the exact scene text on every Undo and Redo, keep IsDirty and the recovery text in step, and
// Undo all / Redo all must save the first and the edited scene files byte for byte.
internal static class HistoryChecks
{
    [Check(9600, "history")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static System.Text.Json.JsonElement Json(string text) => System.Text.Json.JsonDocument.Parse(text).RootElement.Clone();
    private static readonly ScriptField[] QaFields =
    [
        new("qaWaves", typeof(System.Text.Json.JsonElement), Json("[1,2]")) { Element = typeof(int) },
        new("qaPrices", typeof(System.Text.Json.JsonElement), Json("{\"herb\":3}")) { Element = typeof(int), Key = typeof(string) },
        new("qaWave", typeof(System.Text.Json.JsonElement), Json("{\"Count\":0,\"Label\":null}")) { Members = [new("Count", typeof(int), 0), new("Label", typeof(string), null)] },
    ];

    internal static void Run(string repository, string scratch)
    {
        Check(repository, scratch, "KeyRoom", "Showcase", seed: 7, steps: 90, fullEvery: 1);
        Check(repository, scratch, "CollisionLab", "Field10000", seed: 11, steps: 30, fullEvery: 10);
        InspectorChecks.Run(repository, scratch); // coordinator: may move to Program.cs as its own group
    }

    private static void Check(string repository, string scratch, string sample, string sceneName, int seed, int steps, int fullEvery)
    {
        var root = Path.Combine(scratch, "history-" + sample);
        TileChecks.Prepare(Path.Combine(repository, "samples", sample), root);
        var path = Path.Combine(root, "Scenes", sceneName + ".scene");
        var scene = SceneDocument.OpenForEditing(path);
        scene.Save(path);
        var firstFile = File.ReadAllBytes(path);
        // 13.15: a .particles asset the Inspector curve rows edit in place.
        var particles = Path.Combine(root, "Assets", "HistoryQa.particles");
        ProjectYaml.WriteBytes(particles, ProjectYaml.Encode(new ParticleEffect { SizeOverLifetime = new([new(0, 1), new(1, 0)]),
            ColorOverLifetime = new([new(0, "#FFFFFF"), new(1, "#FF8800")], [new(0, 1), new(1, 0)]) }));
        var particleBytes = File.ReadAllBytes(particles);
        var prefabs = Directory.GetFiles(Path.Combine(root, "Assets"), "*.prefab*", SearchOption.AllDirectories).Order().ToArray();
        var prefabBytes = prefabs.Select(File.ReadAllBytes).ToArray();
        scene.Settings.Save(root); // the file then always holds the serialized settings of the current state
        var history = new SceneHistory();
        history.Reset(scene);
        var states = new List<(string Text, GameSettings Settings)> { (scene.CaptureObjects(), scene.Settings) };
        string savedText = states[0].Text;
        var random = new Random(seed);
        void Require(bool ok, string message)
        {
            if (!ok) throw new Exception($"Undo diff {sceneName}: {message}");
        }
        void Expect(int index, string what)
        {
            var (text, settings) = states[index];
            Require(scene.CaptureObjects() == text, $"{what}: scene text of state {index}");
            Require(Equals(scene.Settings, settings), $"{what}: settings of state {index}");
            Require(File.ReadAllBytes(Path.Combine(root, GameSettings.FileName)).AsSpan().SequenceEqual(settings.SerializedSettings()), $"{what}: GameSettings file of state {index}");
            Require(history.CurrentSnapshot() == text, $"{what}: recovery text of state {index}");
            Require(history.IsDirty == (text != savedText), $"{what}: IsDirty at state {index}");
        }
        SceneObject Any() => scene.Objects[random.Next(scene.Objects.Count)];
        string Relative(string file) => Path.GetRelativePath(root, file).Replace('\\', '/');
        var prefabFiles = prefabs.Where(p => p.EndsWith(".prefab", StringComparison.Ordinal)).ToArray();
        var scanned = ProjectAssets.Scan(root);
        var pngs = scanned.Entries.Where(e => SpriteAssets.IsPng(e.Path)).Select(e => (e.Id, e.Path)).Take(6).ToArray();
        var sounds = scanned.Entries.Where(e => e.Path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || e.Path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToArray();

        bool fileEdit = false; // an asset-only record (13.15) changes a file, not the scene text
        // Each edit changes the scene the way the editor command does and records it; null means "not applicable here".
        Func<bool?>[] edits =
        [
            () => { var o = Any(); o.X += random.Next(1, 40); o.Y -= random.Next(0, 9); return history.Record(scene, o.Id, o.Id, null); },
            () => { var o = Any(); o.Name = "Edited " + random.Next(1000); return history.Record(scene, o.Id, o.Id, null); },
            () =>
            {
                if (scene.Objects.Where(o => o.GetComponent<SpriteRendererComponent>() != null).ToArray() is not { Length: > 0 } sprites) return null;
                var o = sprites[random.Next(sprites.Length)];
                var sprite = o.GetComponent<SpriteRendererComponent>()!;
                sprite.Order = random.Next(-5, 50);
                sprite.SquareColor = $"#{random.Next(0x1000000):X6}";
                return history.Record(scene, o.Id, o.Id, null);
            },
            () =>
            {
                var o = new SceneObject { Name = "Added " + random.Next(1000), X = random.Next(400), Y = random.Next(300) };
                o.AddComponent(new SpriteRendererComponent { SquareColor = "#336699", Order = random.Next(9) });
                scene.Objects.Insert(random.Next(scene.Objects.Count + 1), o);
                return history.Record(scene, null, o.Id, null);
            },
            () =>
            {
                if (scene.Objects.Count < 8) return null;
                var o = Any();
                scene.RemoveObject(o);
                return history.Record(scene, o.Id, null, null);
            },
            () => { var copy = scene.DuplicateObject(Any()); return history.Record(scene, null, copy.Id, null); },
            () =>
            {
                var o = Any();
                var parent = random.Next(3) == 0 ? null : Any();
                if (!scene.Reparent(o, parent)) return null;
                return history.Record(scene, o.Id, o.Id, null);
            },
            () =>
            {
                int from = random.Next(scene.Objects.Count), to = random.Next(scene.Objects.Count);
                if (from == to) return null;
                var o = scene.Objects[from];
                scene.Objects.RemoveAt(from);
                scene.Objects.Insert(to, o);
                return history.Record(scene, o.Id, o.Id, null);
            },
            () =>
            {
                if (scene.Cameras.Count() < 2) { scene.CreateCamera(new(random.Next(500), random.Next(300))); scene.NormalizeCamera(); }
                else { scene.DefaultCameraId = scene.Cameras.First(c => c.Id != scene.DefaultCameraId).Id; scene.NormalizeCamera(); }
                return history.Record(scene, null, null, null);
            },
            () =>
            {
                scene.Settings = scene.Settings with { GameTitle = "History " + random.Next(1000) };
                scene.Settings.Save(root);
                return history.Record(scene, null, null, null);
            },
            () =>
            {
                // 16.12/16.10 editor fields: the Input Actions Save and the Audio Mixer Save defaults each replace one GameSettings value.
                var s = scene.Settings;
                var actions = s.Input.AsMap().Actions!;
                var picked = actions[random.Next(actions.Length)];
                var rule = random.Next(4) switch { 0 => null, 1 => ActionBinding.Hold, 2 => ActionBinding.Tap, _ => ActionBinding.DoubleTap };
                var input = s.Input.AsMap() with
                {
                    Actions = [.. actions.Select(a => a == picked ? a with { Interaction = rule, Ticks = random.Next(2) == 0 ? null : random.Next(1, 60), Buffer = random.Next(3) == 0 ? random.Next(0, 9) : null } : a)],
                    Vectors = random.Next(2) == 0 ? null : [new("Aim" + random.Next(9), actions[0].Name, actions[1 % actions.Length].Name, actions[2 % actions.Length].Name, actions[3 % actions.Length].Name)]
                };
                scene.Settings = s with
                {
                    Input = input,
                    AudioSnapshots = random.Next(2) == 0 ? null : [new("Night", new(Music: random.Next(101) / 100f, Voice: random.Next(2) == 0 ? null : .5f))],
                    AudioDucking = random.Next(2) == 0 ? null : [new(AudioBus.Voice, AudioBus.Music, -random.Next(81), random.Next(11) / 20f, .4f)]
                };
                scene.Settings.Save(root);
                return history.Record(scene, null, null, null);
            },
            () =>
            {
                // Typing into one field: the second keystroke merges into the first record, or removes it when it restores the start.
                var o = Any();
                var field = new object();
                float start = o.X;
                o.X = start + 3;
                history.Record(scene, o.Id, o.Id, field);
                o.X = random.Next(2) == 0 ? start : start + 7;
                history.Record(scene, o.Id, o.Id, field);
                history.EndGroup();
                return o.X != start; // one record from the start to the last value, none when the field returned to the start
            },
            () =>
            {
                var picked = Enumerable.Range(0, 3).Select(_ => Any()).Distinct().ToArray();
                foreach (var o in picked) o.Y += 5;
                var ids = picked.Select(o => o.Id).ToArray();
                return history.Record(scene, ids[^1], ids[^1], null, multiSelection: ids);
            },
            () =>
            {
                if (prefabFiles.Length == 0) return null;
                var placed = scene.PlacePrefab(Relative(prefabFiles[random.Next(prefabFiles.Length)]), new(random.Next(300), random.Next(200)));
                return history.Record(scene, null, placed.Id, null);
            },
            () =>
            {
                if (scene.Objects.Where(o => o.Prefab != null).ToArray() is not { Length: > 0 } roots) return null;
                var o = roots[random.Next(roots.Length)];
                o.X += 11;
                if (o.GetComponent<SpriteRendererComponent>() is { } sprite) sprite.Order += 3;
                var reverted = scene.RevertPrefab(o);
                return history.Record(scene, o.Id, reverted.Id, null);
            },
            () =>
            {
                if (scene.Objects.Where(o => o.Prefab != null && o.Parent == null).ToArray() is not { Length: > 0 } roots) return null;
                var o = roots[random.Next(roots.Length)];
                var assets = ProjectAssets.Scan(root);
                if (assets.PathFor(o.Prefab!.AssetId) is not { } relative) return null;
                var file = Path.Combine(root, relative);
                if (o.GetComponent<SpriteRendererComponent>() is { } sprite) sprite.SquareColor = $"#{random.Next(0x1000000):X6}";
                else o.Width += 2;
                var before = PrefabFileEdit.Read(file);
                scene.ExportPrefab(o, file, update: true);
                scene.RefreshPrefabs(ProjectAssets.Scan(root));
                var edit = new PrefabFileEdit(file, before, PrefabFileEdit.Read(file));
                return history.Record(scene, o.Id, o.Id, null, edit.HasChanges ? edit : null);
            },
            () =>
            {
                var o = new SceneObject { Name = "Tiles " + random.Next(1000), X = random.Next(200), Y = random.Next(200) };
                o.AddComponent(new Tilemap2DComponent());
                scene.Objects.Insert(random.Next(scene.Objects.Count + 1), o);
                return history.Record(scene, null, o.Id, null);
            },
            () =>
            {
                if (scene.Objects.Where(o => o.GetComponent<Tilemap2DComponent>() != null).ToArray() is not { Length: > 0 } maps) return null;
                var o = maps[random.Next(maps.Length)];
                var start = new Point(random.Next(-6, 6), random.Next(-6, 6));
                var cells = new TileStroke(o.GetComponent<Tilemap2DComponent>()!, random.Next(4) == 0 ? TileTool.Eraser : TileTool.Brush,
                    random.Next(1, 4), start).Finish(new(start.X + random.Next(0, 5), start.Y + random.Next(-2, 3)));
                if (cells.Length == 0) return false;
                history.RecordStroke(scene, o.Id, cells);
                return true;
            },
            // Inspector Active check (13.2): one record per toggle.
            () => { var o = Any(); o.Active = !o.Active; return history.Record(scene, o.Id, o.Id, null); },
            // Component header check (13.2).
            () =>
            {
                var owned = scene.Objects.SelectMany(o => o.Components.Where(c => c.CanDisable).Select(c => (Owner: o, Component: c))).ToArray();
                if (owned.Length == 0) return null;
                var (o, c) = owned[random.Next(owned.Length)];
                c.Enabled = !c.Enabled;
                return history.Record(scene, o.Id, o.Id, null);
            },
            // Gear menu (13.4): Reset, Copy + Paste Component Values (same type on another object), Move Up/Down, Remove.
            () =>
            {
                var owned = scene.Objects.SelectMany(o => o.Components.Select(c => (Owner: o, Component: c))).ToArray();
                var (o, c) = owned[random.Next(owned.Length)];
                return ComponentEdits.Reset(o, c) && history.Record(scene, o.Id, o.Id, null);
            },
            () =>
            {
                var owned = scene.Objects.SelectMany(o => o.Components.Select(c => (Owner: o, Component: c))).ToArray();
                var (from, source) = owned[random.Next(owned.Length)];
                var targets = owned.Where(p => p.Component != source && p.Component.GetType() == source.GetType() && p.Component.Kind == source.Kind).ToArray();
                if (targets.Length == 0) return null;
                var (o, c) = targets[random.Next(targets.Length)];
                return ComponentEdits.PasteValues(o, c, ComponentEdits.Copy(source)) && history.Record(scene, o.Id, o.Id, null);
            },
            () =>
            {
                if (scene.Objects.Where(o => o.Components.Length > 1).ToArray() is not { Length: > 0 } many) return null;
                var o = many[random.Next(many.Length)];
                return ComponentEdits.Move(o, o.Components[random.Next(o.Components.Length)], random.Next(2) == 0 ? -1 : 1) &&
                    history.Record(scene, o.Id, o.Id, null);
            },
            () =>
            {
                if (scene.Objects.Where(o => o.Components.Any(c => c is not Camera2DComponent)).ToArray() is not { Length: > 0 } owners) return null;
                var o = owners[random.Next(owners.Length)];
                var removable = o.Components.Where(c => c is not Camera2DComponent).ToArray();
                return o.RemoveComponent(removable[random.Next(removable.Length)]) && history.Record(scene, o.Id, o.Id, null);
            },
            // 13.9: the colour popup applies live several times, then closing it records once.
            () =>
            {
                if (scene.Objects.Where(o => o.GetComponent<SpriteRendererComponent>() != null).ToArray() is not { Length: > 0 } sprites) return null;
                var o = sprites[random.Next(sprites.Length)];
                var sprite = o.GetComponent<SpriteRendererComponent>()!;
                for (int i = 0; i < 4; i++) sprite.Tint = $"#{random.Next(0x1000000):X6}";
                return history.Record(scene, o.Id, o.Id, null);
            },
            // 16.2: the Sprite Renderer's Draw row (Simple, Tiled, Sliced) and one committed Slice border.
            () =>
            {
                if (scene.Objects.Where(o => o.GetComponent<SpriteRendererComponent>() != null).ToArray() is not { Length: > 0 } sprites) return null;
                var o = sprites[random.Next(sprites.Length)];
                var sprite = o.GetComponent<SpriteRendererComponent>()!;
                var mode = (SpriteDrawMode)random.Next(3);
                sprite.DrawMode = mode == SpriteDrawMode.Simple ? null : mode;
                sprite.Slice = mode == SpriteDrawMode.Sliced ? new UiBorders(random.Next(9), random.Next(9), random.Next(9), random.Next(9)) : null;
                return history.Record(scene, o.Id, o.Id, null);
            },
            // 13.7/13.17: drop a PNG on the Sprite field (replace), clear it to Square, drop a sound on an Audio Source.
            () =>
            {
                if (pngs.Length == 0 || scene.Objects.Where(o => o.GetComponent<SpriteRendererComponent>() != null).ToArray() is not { Length: > 0 } sprites) return null;
                var o = sprites[random.Next(sprites.Length)];
                var sprite = o.GetComponent<SpriteRendererComponent>()!;
                var (id, path) = pngs[random.Next(pngs.Length)];
                (sprite.Asset, sprite.AssetId) = random.Next(4) == 0 ? ((string?)null, (Guid?)null) : (path, id);
                return history.Record(scene, o.Id, o.Id, null);
            },
            () =>
            {
                if (sounds.Length == 0) return null;
                var o = Any();
                var audio = o.GetComponent<AudioSourceComponent>();
                if (audio == null) { audio = new AudioSourceComponent(); o.AddComponent(audio); }
                audio.Sound = random.Next(3) == 0 ? null : sounds[random.Next(sounds.Length)];
                return history.Record(scene, o.Id, o.Id, null);
            },
            // 13.17: one drop on a multi-selection's common Sprite field sets every object and records once.
            () =>
            {
                if (pngs.Length == 0) return null;
                var picked = scene.Objects.Where(o => o.GetComponent<SpriteRendererComponent>() != null).OrderBy(_ => random.Next()).Take(3).ToArray();
                if (picked.Length < 2) return null;
                var (id, path) = pngs[random.Next(pngs.Length)];
                foreach (var o in picked) (o.GetComponent<SpriteRendererComponent>()!.Asset, o.GetComponent<SpriteRendererComponent>()!.AssetId) = (path, id);
                var ids = picked.Select(o => o.Id).ToArray();
                return history.Record(scene, ids[^1], ids[^1], null, multiSelection: ids);
            },
            // 13.15: an Inspector curve or gradient commit rewrites the .particles file once; Undo/Redo write it back.
            () =>
            {
                var fx = ParticleEffect.Load(particles);
                var keys = fx.SizeOverLifetime!.Keys.ToArray();
                keys[0] = keys[0] with { Value = random.Next(1, 40) / 10f };
                var colors = fx.ColorOverLifetime!.Colors!.ToArray();
                colors[1] = colors[1] with { Color = $"#{random.Next(0x1000000):X6}" };
                var next = fx with { SizeOverLifetime = fx.SizeOverLifetime with { Keys = keys }, ColorOverLifetime = fx.ColorOverLifetime with { Colors = colors } };
                var before = PrefabFileEdit.Read(particles);
                ProjectYaml.WriteBytes(particles, ProjectYaml.Encode(next));
                var edit = new PrefabFileEdit(particles, before, PrefabFileEdit.Read(particles));
                return fileEdit = edit.HasChanges && history.Record(scene, null, null, null, edit);
            },
            // 13.5: one multi-selection field commit (JSON path) on objects that share a component, one record.
            () =>
            {
                var picked = scene.Objects.Where(o => o.GetComponent<SpriteRendererComponent>() != null).OrderBy(_ => random.Next()).Take(4).ToArray();
                if (picked.Length < 2) return null;
                bool order = random.Next(2) == 0;
                if (!MultiEdits.Set(picked, "spriteRenderer/", null, order ? "order" : "flipX",
                        order ? System.Text.Json.Nodes.JsonValue.Create(random.Next(1, 60)) : System.Text.Json.Nodes.JsonValue.Create(random.Next(2) == 0))) return false;
                var ids = picked.Select(o => o.Id).ToArray();
                return history.Record(scene, ids[^1], ids[^1], null, multiSelection: ids);
            },
            // 13.6: an Inspector row's Revert puts back one property of a prefab instance.
            () =>
            {
                var candidates = scene.Objects.Where(o => o.Prefab != null).SelectMany(r => scene.PropertyOverrides(r).Select(c => (Root: r, Change: c))).ToArray();
                if (candidates.Length == 0) return null;
                var (r, change) = candidates[random.Next(candidates.Length)];
                scene.EditPrefabProperty(r, change, apply: false);
                return history.Record(scene, r.Id, r.Id, null);
            },
            // 13.10: a label scrub applies many values live and records once at the release.
            () =>
            {
                var o = Any();
                float start = o.Width;
                for (int px = 1; px <= 12; px++) scene.Resize(o, start + px, o.Height);
                return history.Record(scene, o.Id, o.Id, null);
            },
            // 13.8: a list element added or removed, a dictionary row added or renamed, a struct member changed (back to the
            // default removes the saved key), each written whole by ScriptValueEdits and recorded once.
            () =>
            {
                var owners = scene.Objects.SelectMany(o => o.Components.OfType<ScriptComponent>().Select(c => (Owner: o, Script: c))).ToArray();
                if (owners.Length == 0) return null;
                var (o, script) = owners[random.Next(owners.Length)];
                var field = QaFields[random.Next(QaFields.Length)];
                var value = ScriptValueEdits.Current(script, field);
                switch (value)
                {
                    case System.Text.Json.Nodes.JsonArray list when list.Count > 0 && random.Next(3) == 0: list.RemoveAt(list.Count - 1); break;
                    case System.Text.Json.Nodes.JsonArray list: list.Add(random.Next(100)); break;
                    case System.Text.Json.Nodes.JsonObject map when field.Key != null:
                        if (random.Next(2) == 0 || map.Count == 0) map[ScriptValueEdits.NewKey(map, field.Key)] = random.Next(9);
                        else ScriptValueEdits.RenameKey(map, map.First().Key, "renamed" + random.Next(1000), field.Key);
                        break;
                    case System.Text.Json.Nodes.JsonObject wave: wave["Count"] = random.Next(3); break;
                }
                ScriptValueEdits.Store(script, field, value);
                return history.Record(scene, o.Id, o.Id, null);
            },
            () => history.Record(scene, null, null, null) ? throw new Exception("an unchanged scene must not record") : false,
            () => { history.MarkSaved(); savedText = states[^1].Text; return false; },
        ];

        var counts = new int[edits.Length];
        int tries = 0;
        for (int step = 0; step < steps; step++)
        {
            int kind = tries < edits.Length ? tries : random.Next(edits.Length); // every kind once first, then at random
            tries++;
            var before = states[^1];
            if (edits[kind]() is not { } recorded) { step--; continue; }
            var text = scene.CaptureObjects();
            counts[kind]++;
            bool changed = text != before.Text || !Equals(scene.Settings, before.Settings) || fileEdit;
            fileEdit = false;
            Require(recorded == changed, $"step {step} kind {kind}: recorded {recorded} but changed {changed}");
            if (!recorded) { Expect(states.Count - 1, $"step {step} kind {kind} unchanged"); continue; }
            states.Add((text, scene.Settings));
            Expect(states.Count - 1, $"step {step} kind {kind}");
            history.Restore(scene, false);
            Expect(states.Count - 2, $"step {step} kind {kind} undo");
            history.Restore(scene, true);
            Expect(states.Count - 1, $"step {step} kind {kind} redo");
            if (step % fullEvery != 0) continue;
            for (int i = states.Count - 2; i >= 0; i--) { history.Restore(scene, false); Expect(i, $"step {step} undo all"); }
            Require(!history.CanUndo, "undo all reaches the first state");
            for (int i = 1; i < states.Count; i++) { history.Restore(scene, true); Expect(i, $"step {step} redo all"); }
            Require(!history.CanRedo, "redo all reaches the last state");
        }

        // A change the history never recorded is discarded by Undo, as the former whole-scene restore did.
        Require(history.CanUndo, "records to undo");
        scene.Objects[random.Next(scene.Objects.Count)].Name += " (unrecorded)";
        history.Restore(scene, false);
        Expect(states.Count - 2, "undo over an unrecorded change");
        history.Restore(scene, true);
        Expect(states.Count - 1, "redo after an unrecorded change");

        scene.Save(path);
        history.MarkSaved(scene);
        var editedFile = File.ReadAllBytes(path);
        var editedPrefabs = prefabs.Select(File.ReadAllBytes).ToArray();
        var editedParticles = File.ReadAllBytes(particles);
        for (int i = states.Count - 2; i >= 0; i--) history.Restore(scene, false);
        scene.Save(path);
        Require(File.ReadAllBytes(path).SequenceEqual(firstFile), "undo all saves the first scene bytes");
        Require(prefabs.Select(File.ReadAllBytes).Zip(prefabBytes).All(p => p.First.SequenceEqual(p.Second)), "undo all restores prefab sources");
        Require(File.ReadAllBytes(particles).SequenceEqual(particleBytes), "undo all restores the .particles curves");
        for (int i = 1; i < states.Count; i++) history.Restore(scene, true);
        scene.Save(path);
        Require(File.ReadAllBytes(path).SequenceEqual(editedFile), "redo all saves the edited scene bytes");
        Require(prefabs.Select(File.ReadAllBytes).Zip(editedPrefabs).All(p => p.First.SequenceEqual(p.Second)), "redo all restores applied prefab sources");
        Require(File.ReadAllBytes(particles).SequenceEqual(editedParticles), "redo all restores the edited .particles curves");
        Console.WriteLine($"PASS undo diff {sceneName} ({scene.Objects.Count} objects): {states.Count - 1} records from {steps} mixed edits, " +
            $"each Undo/Redo exact, undo all/redo all saved byte-identical scenes; edits per kind {string.Join(",", counts)}.");
    }
}
