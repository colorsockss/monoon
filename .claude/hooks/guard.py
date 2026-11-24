#!/usr/bin/env python3
"""PreToolUse guard for MoNoon. Rules and reasons: docs/manual/agent-workflow.md#claude-code-설정

Blocks, with exit code 2 and the reason on stderr:
  1. downloads and installs other than the approved packages (Ahwoo.BRUTAL.*, Avalonia.Controls.ColorPicker, crossgen2 packs),
  2. edits and deletes under .test-env/ (the x64 guests' disks, base images and keys),
  3. editor or player window runs that are not wrapped in engine/scripts/gui-lock.sh.
It reads the command text only, so it is a guard against the usual forms, not a sandbox: a script that downloads by itself passes.
It fails open (exit 0) on input it cannot read, because a broken guard would otherwise stop every tool call in every session.
"""
import json
import os
import re
import shlex
import sys

APPROVED = re.compile(r"^(Ahwoo\.BRUTAL\..+|Avalonia\.Controls\.ColorPicker|Microsoft\.NETCore\.App\.Crossgen2(\..+)?)$", re.I)
# Runtime packs already in the NuGet cache; any other RID downloads one (osx-x64 is on hold, docs/plans/next-work/README.md #15).
RIDS = {"osx-arm64", "linux-x64", "win-x64"}
ASK = " 필요하면 사용자에게 승인을 받고, 사용자가 직접 실행하거나 .claude/settings.local.json에 \"disableAllHooks\": true를 잠시 넣게 한다."
WRAPPERS = {"sudo", "time", "nice", "nohup", "command", "builtin", "exec", "noglob", "stdbuf", "xargs", "caffeinate"}
SHELLS = {"sh", "bash", "zsh", "dash"}
SEPARATORS = {";", "&", "&&", "|", "||", "|&", "(", ")", "\n"}
REDIRECTS = {">", ">>", ">|", "&>", "&>>", "<>", ">&"}
INPUTS = {"<", "<<", "<<<", "<&"}


class Blocked(Exception):
    pass


def in_test_env(path, cwd):
    path = os.path.normpath(os.path.join(cwd, os.path.expanduser(path)))
    return ".test-env" in path.split(os.sep)


def tokens(command):
    # A newline separates commands like ';' does, so it is punctuation here rather than whitespace.
    lex = shlex.shlex(command, posix=True, punctuation_chars="();<>|&\n")
    lex.whitespace = " \t\r"
    lex.whitespace_split = True
    try:
        return list(lex)
    except ValueError:  # unbalanced quotes: still check the words
        return command.split()


def check_bash(command, cwd, locked=False):
    segment = []
    for token in tokens(command) + [";"]:
        if token in SEPARATORS:
            if segment:
                cwd = check_segment(segment, cwd, locked)
            segment = []
        else:
            segment.append(token)


def check_segment(words, cwd, locked):
    env = {}
    redirects = []
    argv = []
    i = 0
    while i < len(words):
        if words[i] in REDIRECTS or words[i] in INPUTS:
            if words[i] in REDIRECTS and i + 1 < len(words) and not re.fullmatch(r"\d+|-", words[i + 1]):
                redirects.append(words[i + 1])
            i += 2
            continue
        argv.append(words[i])
        i += 1
    # Leading VAR=value assignments, `env`, and wrappers that run their argument.
    while argv:
        head = argv[0]
        if re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", head):
            name, value = head.split("=", 1)
            env[name] = value
            argv = argv[1:]
        elif head == "env" or os.path.basename(head) in WRAPPERS:
            argv = argv[1:]
            while argv and argv[0].startswith("-"):
                argv = argv[1:]
        elif head == "timeout":
            argv = argv[2:]
        else:
            break
    for target in redirects:
        if in_test_env(target, cwd):
            raise Blocked(f".test-env 아래에 쓰는 리다이렉트({target})는 막혀 있다. x64 게스트의 디스크·이미지·키는 수정하지 않는다.")
    if env.get("MONOON_ALLOW_SDL_DOWNLOAD") == "1":
        raise Blocked("MONOON_ALLOW_SDL_DOWNLOAD=1은 SDL 소스 묶음을 GitHub에서 내려받는다. engine/scripts/bootstrap-worktree.sh로 메인 체크아웃의 것을 쓴다." + ASK)
    if not argv:
        return cwd
    prog, args = os.path.basename(argv[0]), argv[1:]
    if prog == "cd":
        return os.path.normpath(os.path.join(cwd, os.path.expanduser(args[0]))) if args else os.path.expanduser("~")
    if prog in SHELLS and len(args) >= 2 and args[0] == "-c":
        check_bash(args[1], cwd, locked)
        return cwd
    if prog == "gui-lock.sh" or (prog in SHELLS and args and os.path.basename(args[0]) == "gui-lock.sh"):
        inner = args if prog == "gui-lock.sh" else args[1:]
        check_segment(inner, cwd, True)
        return cwd
    check_download(prog, args)
    check_test_env(prog, args, cwd)
    if not locked:
        check_window(argv[0], prog, args, env)
    return cwd


def check_download(prog, args):
    sub = args[0] if args else ""
    rest = args[1:]
    installers = {
        "brew": {"install", "reinstall", "upgrade", "fetch", "tap", "bundle"},
        "pip": {"install", "download"}, "pip3": {"install", "download"}, "pipx": {"install", "run"},
        "npm": {"install", "i", "ci", "add", "exec"}, "pnpm": {"install", "i", "add", "dlx"}, "yarn": {"install", "add", "dlx"},
        "gem": {"install"}, "cargo": {"install"}, "go": {"install", "get"}, "port": {"install"},
        "apt": {"install"}, "apt-get": {"install"}, "mas": {"install"}, "uv": {"pip", "tool", "add"},
        "nuget": {"install", "restore"},
    }
    reason = None
    if prog in ("curl", "wget"):
        reason = prog
    elif prog == "npx":
        reason = "npx(패키지 내려받기)"
    elif prog in installers and sub in installers[prog]:
        reason = f"{prog} {sub}"
    elif prog.startswith("python") and args[:2] == ["-m", "pip"] and len(args) > 2 and args[2] in ("install", "download"):
        reason = "pip install"
    elif prog == "softwareupdate" and any(a in ("-i", "--install") for a in args):
        reason = "softwareupdate --install"
    elif prog == "xcode-select" and "--install" in args:
        reason = "xcode-select --install"
    elif prog == "git" and sub == "clone" and any(re.match(r"^(https?|ssh|git)://|^[\w.-]+@[\w.-]+:", a) for a in rest):
        reason = "원격 저장소 git clone"
    elif prog == "dotnet":
        if sub in ("tool", "workload") and rest[:1] and rest[0] in ("install", "update", "restore", "repair"):
            reason = f"dotnet {sub} {rest[0]}"
        elif sub == "new" and rest[:1] == ["install"]:
            reason = "dotnet new install"
        elif sub == "add" and "package" in rest:
            name = rest[rest.index("package") + 1] if rest.index("package") + 1 < len(rest) else ""
            if not APPROVED.match(name):
                reason = f"dotnet add package {name}"
        else:
            for i, a in enumerate(args):
                rid = None
                if a in ("-r", "--runtime") and i + 1 < len(args):
                    rid = args[i + 1]
                elif m := re.match(r"^(?:--runtime=|[-/]p(?:roperty)?:RuntimeIdentifier=)(.+)$", a, re.I):
                    rid = m.group(1)
                if rid and rid.lower() not in RIDS:
                    reason = f"{rid} 런타임 팩(dotnet {sub} -r {rid})"
    if reason:
        raise Blocked(f"{reason}: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.ColorPicker뿐이다." + ASK)


def check_test_env(prog, args, cwd):
    operands = [a for a in args if not a.startswith("-")]
    touched = [a for a in operands if in_test_env(a, cwd)]
    sub = args[0] if args else ""
    hit = False
    if prog in ("rm", "rmdir", "unlink", "shred", "truncate", "mv", "ln", "chmod", "chown", "chflags", "touch", "tee", "install", "dd"):
        hit = bool(touched) or (prog == "dd" and any(a.startswith("of=") and in_test_env(a[3:], cwd) for a in args))
    elif prog in ("cp", "rsync", "ditto", "scp"):
        hit = bool(operands) and in_test_env(operands[-1], cwd)
    elif prog in ("sed", "perl", "gsed"):
        hit = bool(touched) and any(a.startswith("-i") or a == "--in-place" for a in args)
    elif prog == "find":
        hit = bool(touched) and any(a in ("-delete", "-exec", "-execdir", "-ok", "-fprint") for a in args)
    elif prog == "git" and sub == "clean":
        hit = any(re.match(r"^-[a-zA-Z]*[xX]", a) for a in args[1:])
        if hit:
            raise Blocked("git clean -x/-X는 무시된 파일을 지운다. 메인 체크아웃에서는 .test-env, 템플릿 캐시와 빌드 결과까지 사라진다." + ASK)
    elif prog == "git" and sub in ("add", "rm", "mv"):
        hit = bool(touched)
    elif prog == "qemu-img" and sub in ("commit", "rebase", "resize", "amend", "snapshot"):
        hit = bool(touched)
    if hit:
        raise Blocked(f".test-env 아래를 바꾸거나 지우는 명령({prog} {' '.join(touched) or sub})은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는 삭제하거나 저장소에 추가하지 않는다(AGENTS.md)." + ASK)


def check_window(path, prog, args, env):
    headless = "MONOON_REPLAY_OFFSCREEN" in env or "MONOON_REPLAY_SNAPSHOTS" in env or any(a in ("--smoke-test", "--migrate-project", "--version", "--help") for a in args)
    reason = None
    if prog in ("MoNoon", "MoNoon.exe", "MoNoonPlayer", "MoNoonPlayer.exe") or re.search(r"\.app/Contents/MacOS/[^/]+$", path):
        reason = None if headless else path
    elif prog == "open" and any(a == "-a" or a.rstrip("/").endswith(".app") for a in args):
        reason = "open <앱>"
    elif prog.startswith("python"):
        script = next((a for a in args if a.endswith(".py")), "")
        if os.path.basename(script) == "editor.py" or ("engine/tests/" in script and "editor" in args[args.index(script) + 1:args.index(script) + 2]):
            reason = f"{script} (에디터 하네스)"
    elif prog == "dotnet" and args[:1] == ["run"] and any(re.search(r"Engine\.(Editor|Player)\b", a) for a in args) and not headless:
        reason = "dotnet run (에디터·플레이어)"
    if reason:
        raise Blocked(f"{reason}: 창을 여는 명령이다. sh engine/scripts/gui-lock.sh <명령...>으로 감싸 실행한다(GUI 실행을 에이전트 사이에서 하나씩 줄 세우고 화면을 깨워 둔다).")


def check_file(tool, data, cwd):
    path = data.get("file_path") or data.get("notebook_path") or ""
    if path and in_test_env(path, cwd):
        raise Blocked(f"{tool}로 .test-env 아래 파일({path})을 바꿀 수 없다. x64 게스트 환경은 수정하지 않는다(AGENTS.md).")
    if not re.search(r"\.(csproj|fsproj|props|targets)$", path):
        return
    new = data.get("content") or data.get("new_string") or ""
    new += "".join(e.get("new_string", "") for e in data.get("edits") or [])
    try:
        with open(os.path.join(cwd, path), encoding="utf-8") as f:
            old = f.read()
    except OSError:
        old = ""
    known = {(name.lower(), version) for name, version in packages(old)}
    for name, version in packages(new):
        if not APPROVED.match(name) and (name.lower(), version) not in known:
            raise Blocked(f"{path}에 패키지 {name} {version or ''}를 추가하거나 버전을 바꾸면 NuGet에서 새로 내려받는다. 승인된 것은 Ahwoo.BRUTAL.*, crossgen2 팩, Avalonia.Controls.ColorPicker뿐이다." + ASK)


def packages(text):
    for element in re.finditer(r"<(?:PackageReference|PackageVersion|PackageDownload)\b[^>]*>", text):
        name = re.search(r'\b(?:Include|Update)\s*=\s*"([^"]+)"', element.group(0))
        version = re.search(r'\bVersion\s*=\s*"([^"]*)"', element.group(0))
        if name:
            yield name.group(1), version.group(1) if version else None


def main():
    try:
        event = json.load(sys.stdin)
        tool, data = event.get("tool_name", ""), event.get("tool_input") or {}
        cwd = event.get("cwd") or os.getcwd()
    except Exception:
        return 0
    try:
        if tool == "Bash":
            check_bash(data.get("command", ""), cwd)
        elif tool in ("Edit", "Write", "MultiEdit", "NotebookEdit"):
            check_file(tool, data, cwd)
    except Blocked as blocked:
        print("MoNoon guard: " + str(blocked), file=sys.stderr)
        return 2
    except Exception:
        return 0
    return 0


if __name__ == "__main__":
    sys.exit(main())
