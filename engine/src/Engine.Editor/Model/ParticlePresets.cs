using Engine.Core;

namespace Engine.Editor;

// Built-in starting points for a new .particles asset (Project + menu and the Particle panel). Editor only: a game ships a
// preset only as the asset file it created. All use the built-in particle texture, so no PNG comes with them. Dust and Hit
// are one-shot bursts for Landing or Manual emitters; the others loop for Start emitters.
internal static class ParticlePresets
{
    internal sealed record Preset(string Name, string Yaml);

    internal static readonly Preset[] All =
    [
        new("Dust", """
            # A puff of dust when something lands: one burst along the ground that spreads, grows and fades.
            duration: 0.5
            looping: false
            rate: 0
            bursts: [{time: 0, count: 18}]
            maxParticles: 64
            shape: Edge
            width: 24
            spread: 150
            speed: {min: 20, max: 60}
            lifetime: {min: 0.4, max: 0.7}
            size: {min: 6, max: 10}
            aspect: 0.6
            color: {min: "#C8B89A", max: "#A08C70"}
            endSize: 1.6
            gravity: 40

            """),
        new("Embers", """
            # Glowing embers rising from a fire, drifting on a noise field.
            rate: 24
            maxParticles: 96
            shape: Circle
            radius: 6
            spread: 40
            speed: {min: 20, max: 50}
            lifetime: {min: 1, max: 1.8}
            size: {min: 2, max: 4}
            color: {min: "#FFB347", max: "#FF5A1F"}
            gravity: -30
            noise: {strength: 40, frequency: 0.05}
            blend: Additive

            """),
        new("Smoke", """
            # Soft grey smoke that billows up, grows and fades in and out. Prewarm starts it already rising.
            duration: 2
            prewarm: true
            rate: 10
            maxParticles: 64
            shape: Circle
            radius: 8
            spread: 25
            speed: {min: 12, max: 24}
            lifetime: {min: 2, max: 3}
            size: {min: 10, max: 16}
            rotation: {min: 0, max: 360}
            color: {min: "#6E6E6E", max: "#9A9A9A"}
            sizeOverLifetime: {smooth: true, keys: [{time: 0, value: 0.6}, {time: 1, value: 2.5}]}
            colorOverLifetime: {alphas: [{time: 0, alpha: 0}, {time: 0.2, alpha: 0.7}, {time: 1, alpha: 0}]}

            """),
        new("Sparks", """
            # Hot sparks thrown up in bursts and pulled down by gravity, as from welding or a grinder.
            duration: 0.8
            rate: 0
            bursts: [{time: 0, count: 24}]
            maxParticles: 128
            spread: 120
            speed: {min: 120, max: 240}
            lifetime: {min: 0.3, max: 0.6}
            size: {min: 2, max: 3}
            color: {min: "#FFF2B0", max: "#FFC14D"}
            endSize: 0.4
            gravity: 420
            blend: Additive

            """),
        new("Hit", """
            # A hit flash: one fast ring of bright particles that shrink away.
            duration: 0.3
            looping: false
            rate: 0
            bursts: [{time: 0, count: 14}]
            maxParticles: 64
            shape: Circle
            radius: 4
            speed: {min: 90, max: 180}
            lifetime: {min: 0.15, max: 0.35}
            size: {min: 3, max: 6}
            color: {min: "#FFFFFF", max: "#FF5A5A"}
            endSize: 0.2
            blend: Additive

            """),
        new("Rain", """
            # Slanted rain streaks from a line above the view. Add collision (Tiles) to make drops hit the ground.
            rate: 60
            maxParticles: 128
            shape: Edge
            width: 200
            direction: 100
            speed: {min: 260, max: 300}
            lifetime: {min: 0.5, max: 0.6}
            size: {min: 1.5, max: 2}
            aspect: 5
            rotation: {min: 10, max: 10}
            color: {min: "#9FC8FFB0"}
            endAlpha: 1
            gravity: 200

            """),
    ];

    // Writes the preset as <folder>/<Name>.particles (Name2, Name3… when taken) and returns its project-relative path.
    internal static string Write(string root, string folder, Preset preset)
    {
        string relative = $"{folder}/{preset.Name}{ParticleEffect.Extension}";
        for (int i = 2; File.Exists(Path.Combine(root, relative)); i++) relative = $"{folder}/{preset.Name}{i}{ParticleEffect.Extension}";
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, preset.Yaml.ReplaceLineEndings("\n"));
        return relative;
    }
}
