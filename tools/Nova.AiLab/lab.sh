#!/usr/bin/env bash
#
# lab.sh — ein Laborlauf und seine Berichte, in einem Kommando.
#
# Faehrt alle vier Laufarten nach `tools/Nova.AiLab/out/` und schreibt danach
# `out/dashboard.html` sowie den Markdown-Satz unter `tools/Nova.AiLab/reports/`
# (latest.md, runs/<id>.md, README.md). Der Berichtsteil laeuft auch allein:
#
#   ./tools/Nova.AiLab/lab.sh                messen und berichten
#   ./tools/Nova.AiLab/lab.sh --reports-only nur berichten, nichts messen
#   ./tools/Nova.AiLab/lab.sh --regenerate   nur neu rendern, nichts einlesen
#
# WERKZEUG, KEIN BEITRAG. Ein gruener Laborlauf ist Diagnose, kein Nachweis:
# was nicht im laufenden Spiel gesehen wurde, steht als ungesehen im PR-Text.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="tools/Nova.AiLab/out"
REPORT="tools/Nova.AiLab/report/build_reports.py"

MEASURE=1
REGENERATE=0
for arg in "$@"; do
    case "$arg" in
        --reports-only) MEASURE=0 ;;
        --regenerate)   MEASURE=0; REGENERATE=1 ;;
        -h|--help)      sed -n '3,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "lab.sh: unbekannte Option '$arg' (--reports-only, --regenerate, --help)" >&2; exit 2 ;;
    esac
done

cd "$ROOT"

if [[ -d .dotnet ]]; then
    export DOTNET_ROOT="$PWD/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
fi

if (( MEASURE )); then
    # Reihenfolge wie in AGENTS.md §1. Exit-Code 2 heisst NON-DETERMINISTIC —
    # dann ist jede Zahl aus diesem Lauf wertlos, auch die gruenen, und es wird
    # nicht weitergerechnet. `set -e` bricht deshalb hier absichtlich ab.
    dotnet run --project tools/Nova.AiLab -c Release -- \
        match --trace-every 50 --hash-every 500 --view-every 25 --fog --out "$OUT/match"
    dotnet run --project tools/Nova.AiLab -c Release -- duel     --out "$OUT/duel"
    dotnet run --project tools/Nova.AiLab -c Release -- movement --out "$OUT/movement"
    dotnet run --project tools/Nova.AiLab -c Release -- compare  --out "$OUT/compare"
fi

if (( REGENERATE )); then
    python3 "$REPORT" --regenerate
else
    python3 "$REPORT" "$OUT"
fi

# Neue Berichte sind auf `lab/ai-simulation` getrackt, aber `.git/info/exclude`
# haelt `tools/Nova.AiLab/` aus `git status` heraus — eine neue Datei muss
# deshalb mit `git add -f` hinein, sonst faellt sie still unter den Tisch.
echo
echo "Berichte: tools/Nova.AiLab/reports/ (README.md, latest.md, runs/)"
echo "Neu hinzugekommene Dateien brauchen 'git add -f', siehe .git/info/exclude."
