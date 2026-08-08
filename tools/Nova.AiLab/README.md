# Nova.AiLab — KI-Simulationslabor

**Werkzeug, kein Beitrag.** Dieses Verzeichnis ist nicht Teil des Repositories:
`.git/info/exclude` hält es aus jedem `git add -A` heraus, es wird nie
gecherry-pickt und es gehört in keinen `feat/`-Branch. Plan und Begründung:
[`docs/feature-ideas/AiSimulationEnvironment.md`](../../docs/feature-ideas/AiSimulationEnvironment.md).

**Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Was nicht im laufenden
Spiel gesehen wurde, steht als ungesehen im PR-Text.

## Start

```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"   # falls dotnet nicht im PATH ist

# eine KI-gegen-KI-Partie, mit Metriken und Artefakten
dotnet run --project tools/Nova.AiLab -c Release -- match --trace-every 100 --out out/run1

# zwei Läufe desselben Specs, Hash-Ketten verglichen
dotnet run --project tools/Nova.AiLab -c Release -- match --repeat 2 --hash-every 100

# Seed-Matrix über alle Kerne, jeder 20. Lauf doppelt zur Selbstkontrolle
dotnet run --project tools/Nova.AiLab -c Release -- sweep --seeds 24 --out out/sweep

# vier Slots (die Karte hat vier Eckplätze)
dotnet run --project tools/Nova.AiLab -c Release -- match --slots 4

dotnet test tools/Nova.AiLab.Tests/Nova.AiLab.Tests.csproj -c Release
```

## Was hier liegt

| Datei | Inhalt |
|---|---|
| `MultiSlotAiHost.cs` | der Match-Host: `MatchRunner.InitializeMatch` von einem KI-Slot auf N verallgemeinert, sonst nichts |
| `CanonicalOpening.cs` | die D-077-Startaufstellung aus `MatchBootstrap`, Spawnreihenfolge inbegriffen |
| `MatchSpec.cs` / `SpecFile.cs` | Eingabevertrag (§3.2) und sein JSON-Leser — unbekannte Schlüssel sind Fehler, keine Vorgabewerte |
| `MatchRun.cs` | fährt eine Partie, liefert Outcome, Entscheidungstick, Hash-Kette, Trace |
| `SlotMetrics.cs` / `TraceCollector.cs` | der Metrikkatalog aus §3.3, reiner Beobachter, nur Ganzzahlen |
| `CountingAiPeerTransport.cs` | zählt Intent-Verdikte — die einzige Stelle, an der `intentsRejected` ehrlich entsteht |
| `RunArtifacts.cs` | `result.json`, `trace.ndjson`, `hashchain.json` |
| `SweepRunner.cs` / `SeedSeries.cs` | Parallellauf mit Determinismus-Stichprobe (jeder 20. Lauf doppelt) |
| `Program.cs` | Kommandozeile |

## Zwei Dinge, die man wissen muss, bevor man Zahlen liest

**Der Seed ändert die Partie nicht.** Kein Simulationssystem zieht aus dem
Kernel-PRNG; der Seed geht in Zustands-Hash und Snapshot, sonst nirgendwohin.
Ein Sweep über 24 Seeds ist *eine* Beobachtung. Der Sweep sagt das selbst hin,
wenn alle Läufe gleich ausgehen — nicht überlesen.

**Messen darf nichts kosten.** Trace-Collector und Intent-Zählung sind reine
Beobachter, und zwei Tests halten fest, dass ein Lauf mit und ohne sie dieselbe
Hash-Kette liefert. Wenn diese Tests je rot werden, sind alle damit erhobenen
Zahlen wertlos — nicht nur die neuen.

## Die eine Regel, die dieses Labor trägt

Der Host muss dieselbe Partie sein wie das Spiel. Ein abgedrifteter Harness
misst etwas, das es nicht gibt — und meldet dabei weiter saubere Zahlen.
`Nova.AiLab.Tests` prüft das gegen einen handgespiegelten `AiHost` aus
`SkirmishAiTests.cs` und vergleicht Zustands-Hashes, keine abgeschriebenen
Konstanten. Wenn diese Suite rot wird, ist nicht der Test kaputt, sondern die
Spiegelung: dann zuerst `MatchRunner.InitializeMatch` und `MatchBootstrap`
nachziehen, nicht die Erwartung anpassen.

Die Tick-Reihenfolge ist Vertrag, kein Implementierungsdetail. Neue Systeme
werden **eingeordnet, nicht angehängt**, und das Einordnen ist eine Absprache.
