using Engine.Core;

internal static class VorbisChecks
{
    internal static void CheckLongTrack(string file)
    {
        var info = AudioFile.Read(file);
        long pcmBytes = checked(info.Frames * info.Channels * sizeof(float));
        if (pcmBytes < 16 * 1024 * 1024) throw new Exception("Use a test track with at least 16 MiB of decoded PCM.");
        using var mixer = new AudioMixer();
        var source = new AudioSourceComponent { Bus = AudioBus.Music, Loop = true };
        var block = new float[1024];
        long before = GC.GetAllocatedBytesForCurrentThread();
        long resident = Environment.WorkingSet;
        mixer.Prepare(file);
        mixer.Start(source, file, source, default, 0);
        mixer.Render(block);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long native = Environment.WorkingSet - resident;
        if (allocated >= pcmBytes / 4 || native >= pcmBytes / 4 || !mixer.Playing(source) || !block.Any(v => v != 0))
            throw new Exception("Expected bounded preparation and an audible first block.");
        Console.WriteLine($"PASS long Ogg: {info.Frames} source frames, {pcmBytes} decoded PCM bytes; prepare, open and first render allocated {allocated} managed bytes, working set +{native} bytes.");
    }

    internal static void Run(string repo, string scratch)
    {
        string sample = Path.Combine(repo, "samples", "AudioLab");
        string original = Path.Combine(sample, "Assets", "Tide.ogg");
        string project = Directory.CreateDirectory(Path.Combine(scratch, "vorbis-import")).FullName;
        string[] imported = ProjectImport.Copy(project, "Assets", [original]);
        string path = Path.Combine(project, imported.Single());
        var info = AudioFile.Read(path);
        if (info.Rate != 24000 || info.Frames != 28800 || info.Channels != 2 ||
            !ProjectCatalog.Search(imported, "t:audio").SequenceEqual(imported))
            throw new Exception("Vorbis import lost its exact loop length, format or catalog type.");

        // Each voice owns a decoder: two simultaneous voices of one file sum to exactly twice one voice.
        float[] Mix(int count)
        {
            using var mixer = new AudioMixer();
            for (int i = 0; i < count; i++) mixer.Start(new object(), path, new AudioSourceComponent { Loop = true, Volume = .5f }, default, 0);
            var output = new float[2 * 3 * 57600];
            mixer.Render(output);
            return output;
        }
        var one = Mix(1);
        if (!Mix(2).SequenceEqual(one.Select(v => 2 * v)) || !one.AsSpan(2 * 57600, 2 * 57600).SequenceEqual(one.AsSpan(4 * 57600)))
            throw new Exception("Simultaneous Vorbis voices interfere, or the loop does not repeat sample for sample.");
        byte[] bytes = File.ReadAllBytes(path);
        string bad = Path.Combine(scratch, "truncated.ogg");
        File.WriteAllBytes(bad, bytes[..64]);
        RejectImport(bad);
        bad = Path.Combine(scratch, "wrong-codec.ogg");
        File.WriteAllBytes(bad, File.ReadAllBytes(Path.Combine(sample, "Assets", "Tide.wav")));
        RejectImport(bad);
        RejectImport(Path.Combine(repo, "engine", "tests", "fixtures", "AudioLab", "Opus.ogg"));
        void RejectImport(string file)
        {
            try { ProjectImport.Copy(project, "Assets", [file]); }
            catch (InvalidDataException)
            {
                if (!File.Exists(Path.Combine(project, "Assets", Path.GetFileName(file)))) return;
            }
            throw new Exception("Invalid compressed asset was imported: " + file);
        }
        Console.WriteLine("PASS Vorbis: staged import, audio search, sample counts, independent simultaneous decoders, repeating loop and invalid files.");
    }
}
