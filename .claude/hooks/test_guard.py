#!/usr/bin/env python3
"""Feeds sample PreToolUse input to guard.py and checks each decision. Run from the repository root:
    python3 .claude/hooks/test_guard.py          (prints a Markdown table, exit 1 on any mismatch)
"""
import json
import os
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GUARD = os.path.join(ROOT, ".claude", "hooks", "guard.py")
EDITOR = "editor/MoNoon.app/Contents/MacOS/MoNoon"
PLAYER = "game-builds/KeyRoom/KeyRoom.app/Contents/MacOS/MoNoonPlayer"
HARNESS = f"python3 engine/tests/KeyRoom.Rebinding/editor.py {EDITOR} /tmp/k engine/.artifacts/bin/EditorProbe/release/EditorProbe.dll /tmp/o"
CSPROJ = "engine/src/Engine.Authoring/Engine.Authoring.csproj"


def bash(command):
    return {"tool_name": "Bash", "tool_input": {"command": command}}


def edit(path, new, old="x"):
    return {"tool_name": "Edit", "tool_input": {"file_path": path, "old_string": old, "new_string": new}}


def ref(name, version):
    return f'<PackageReference Include="{name}" Version="{version}" />'


CASES = [
    # (expected exit code, label, input)
    (2, "curl download", bash("curl -LO https://example.com/tool.tar.gz")),
    (2, "wget download", bash("wget http://example.com/x")),
    (2, "brew install", bash("brew install qemu")),
    (2, "pip3 install", bash("pip3 install requests")),
    (2, "python3 -m pip install", bash("python3 -m pip install pillow")),
    (2, "npx", bash("npx prettier --check .")),
    (2, "dotnet add package (other)", bash("dotnet add engine/src/Engine.Core package Newtonsoft.Json")),
    (2, "dotnet tool install", bash("dotnet tool install -g dotnet-trace")),
    (2, "osx-x64 runtime pack", bash("dotnet publish engine/src/Engine.Player -c Release -r osx-x64")),
    (2, "RuntimeIdentifier property", bash("dotnet build engine/src/Engine.Player -p:RuntimeIdentifier=linux-arm64")),
    (2, "SDL source download", bash("MONOON_ALLOW_SDL_DOWNLOAD=1 sh engine/scripts/build-scene-sdl-mac.sh")),
    (2, "git clone remote", bash("git clone https://github.com/libsdl-org/SDL.git /tmp/sdl")),
    (2, "curl inside bash -c", bash("bash -c 'curl -O http://example.com/a'")),
    (2, "curl on a later line", bash("cd /tmp\ncurl -O http://example.com/a")),
    (2, "curl in $(...)", bash("echo $(curl -s http://example.com)")),
    (2, "rm under .test-env", bash("rm -rf .test-env/x")),
    (2, "cd .test-env then rm", bash("cd .test-env && rm disk.qcow2")),
    (2, "redirect into .test-env", bash("echo hi > /Users/someone/repo/.test-env/notes.txt")),
    (2, "mv out of .test-env", bash("mv .test-env/base.qcow2 /tmp/")),
    (2, "cp into .test-env", bash("cp key.pub .test-env/ssh/")),
    (2, "sed -i in .test-env", bash("sed -i '' s/a/b/ .test-env/config")),
    (2, "find -delete in .test-env", bash("find .test-env -name '*.tmp' -delete")),
    (2, "git add .test-env", bash("git add -f .test-env/ssh/id_ed25519")),
    (2, "git clean -fdx", bash("git clean -fdx")),
    (2, "qemu-img commit", bash("qemu-img commit .test-env/linux/overlay.qcow2")),
    (2, "Edit under .test-env", edit("/Users/someone/repo/.test-env/linux/run.sh", "y")),
    (2, "Write under .test-env (relative)", {"tool_name": "Write", "tool_input": {"file_path": ".test-env/x.txt", "content": "x"}}),
    (2, "csproj adds other package", edit(CSPROJ, ref("Newtonsoft.Json", "13.0.3"))),
    (2, "csproj bumps SkiaSharp", edit(CSPROJ, ref("SkiaSharp", "3.116.1"), ref("SkiaSharp", "2.88.9"))),
    (2, "editor.py without lock", bash(HARNESS)),
    (2, "verify.py editor without lock", bash(f"python3 engine/tests/PlatformerLab.Effects/verify.py editor {EDITOR} /tmp/p hook.dll /tmp/o")),
    (2, "editor binary without lock", bash(f"{EDITOR} samples/KeyRoom")),
    (2, "game player without lock", bash(PLAYER)),
    (2, "open .app", bash("open game-builds/KeyRoom/KeyRoom.app")),
    (2, "dotnet run editor", bash("dotnet run --project engine/src/Engine.Editor -c Release")),
    (0, "./monoon test", bash("./monoon test")),
    (0, "./monoon test piped", bash("./monoon test 2>&1 | tail -5")),
    (0, "Engine.Checks --only", bash('dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only deck')),
    (0, "probe build", bash("dotnet build engine/tests/KeyRoom.Rebinding -c Release -m:1 -p:UsedAvaloniaProducts=")),
    (0, "editor.py under gui-lock", bash("sh engine/scripts/gui-lock.sh " + HARNESS)),
    (0, "editor binary under gui-lock", bash(f"engine/scripts/gui-lock.sh {EDITOR} samples/KeyRoom")),
    (0, "old scratchpad gui-lock path", bash("sh /private/tmp/claude-501/x/scratchpad/gui-lock.sh " + HARNESS)),
    (0, "offscreen player replay", bash(f"MONOON_REPLAY_OFFSCREEN=1 {PLAYER} --replay-input r.inputreplay slot")),
    (0, "player --smoke-test", bash(f"{PLAYER} --smoke-test /tmp/slot")),
    (0, "verify.py play (headless)", bash("python3 engine/tests/PlatformerLab.Effects/verify.py play exe /tmp/o")),
    (0, "dotnet add BRUTAL package", bash("dotnet add engine/src/Engine.Desktop package Ahwoo.BRUTAL.Sdl.Image")),
    (0, "dotnet add ColorPicker", bash("dotnet add engine/src/Engine.Editor package Avalonia.Controls.ColorPicker --version 11.3.23")),
    (0, "ReadyToRun linux-x64 (crossgen2)", bash("dotnet publish engine/src/Engine.Player -r linux-x64 -p:PublishReadyToRun=true")),
    (0, "copy out of .test-env", bash("cp .test-env/results/shot.png engine/tests/results/x/")),
    (0, "list .test-env", bash("ls -la .test-env")),
    (0, "guest runbook writes results", bash("python3 engine/tests/KeyRoom.Desktop/retest.py linux .test-env/results/linux-next")),
    (0, "git status && diff", bash("git status && git diff")),
    (0, "grep for curl", bash("grep -rn curl engine/scripts")),
    (0, "curl as an argument", bash('echo "curl is blocked"')),
    (0, "git clean without -x", bash("git clean -n")),
    (0, "csproj adds BRUTAL package", edit(CSPROJ, ref("Ahwoo.BRUTAL.Sdl.Image", "2026.10.3"))),
    (0, "csproj adds ColorPicker", edit(CSPROJ, ref("Avalonia.Controls.ColorPicker", "11.3.23"))),
    (0, "csproj keeps existing version", edit(CSPROJ, ref("SkiaSharp", "2.88.9") + "\n", ref("SkiaSharp", "2.88.9"))),
    (0, "ordinary C# edit", edit("engine/src/Engine.Core/SceneGame.cs", "var x = 1;")),
    (0, "Read tool", {"tool_name": "Read", "tool_input": {"file_path": ".test-env/x"}}),
    (0, "unreadable input (fails open)", "not json"),
]


def run(payload):
    if isinstance(payload, dict):
        payload = json.dumps(dict(payload, cwd=ROOT, hook_event_name="PreToolUse"))
    result = subprocess.run([sys.executable, GUARD], input=payload, capture_output=True, text=True, cwd=ROOT)
    return result.returncode, result.stderr.strip().replace("MoNoon guard: ", "")[:90]


failed = 0
print("| 기대 | 결과 | 경우 | 사유(첫 문장) |\n| --- | --- | --- | --- |")
for expected, label, payload in CASES:
    code, reason = run(payload)
    ok = code == expected
    failed += not ok
    word = {0: "허용", 2: "차단"}
    print(f"| {word[expected]} | {word.get(code, code)}{'' if ok else ' **불일치**'} | {label} | {reason.replace('|', '¦')} |")
print(f"\n{len(CASES) - failed}/{len(CASES)} 일치")
sys.exit(1 if failed else 0)
