# Nova.AiLab — KI-Simulationslabor

**Werkzeug, kein Beitrag.** Das Labor lebt ausschliesslich im Branch
`lab/ai-simulation` — dort ist es versioniert, damit die Arbeit daran nicht
verloren geht. In `main` und in jedem `feat/`-Branch existiert es nicht, es wird
nie dorthin gecherry-pickt und es gehört in keinen PR. Plan und Begründung:
[`docs/feature-ideas/AiSimulationEnvironment.md`](../../docs/feature-ideas/AiSimulationEnvironment.md).

`.git/info/exclude` ist die zweite Sicherung, keine erste: es wirkt nur auf
*untracked* Pfade und hält deshalb genau dort, wo es darauf ankommt — auf einem
`feat/`-Branch, wo `tools/Nova.AiLab*` nicht getrackt ist, fängt es ein
versehentliches `git add -A` ab. Auf `lab/ai-simulation` ist das Verzeichnis
getrackt und die Regel folgenlos. Dieselbe Datei schliesst auch `out/` und
`run.sh` aus. Sie ist lokal und wird nicht mitgeklont: nach einem frischen
Clone gehören die Einträge von Hand nachgetragen.

Die Laborartefakte liegen seit dem Umzug **unter** `tools/Nova.AiLab/out/` und
sind damit vom selben Eintrag gedeckt, der schon das Labor schützt — eine
Leitplanke statt zwei. Der verbliebene `out/`-Eintrag deckt weiterhin ein
versehentlich auf oberster Ebene angelegtes Ausgabeverzeichnis ab.

Für `tools/Nova.AiLab/out/` gilt dieselbe Zweiteilung: der Ausschluss schützt die `feat/`-Branches,
auf `lab/ai-simulation` liegt der **jeweils letzte vollständige Lauf** unter
`tools/Nova.AiLab/out/` versioniert — bewusst per `git add -f`, damit Messwerte und der Commit,
zu dem sie gehören, zusammenbleiben. Nach einem Merge-Fenster des Maintainers ist
diese Menge nicht mehr vergleichbar und wird durch einen neuen Lauf ersetzt.

**Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Was nicht im laufenden
Spiel gesehen wurde, steht als ungesehen im PR-Text.

## Start

```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"   # falls dotnet nicht im PATH ist

# eine KI-gegen-KI-Partie, mit Metriken und Artefakten
dotnet run --project tools/Nova.AiLab -c Release -- match --trace-every 100 --out tools/Nova.AiLab/out/run1

# zwei Läufe desselben Specs, Hash-Ketten verglichen
dotnet run --project tools/Nova.AiLab -c Release -- match --repeat 2 --hash-every 100

# die laufende Partie im Terminal mitsehen
dotnet run --project tools/Nova.AiLab -c Release -- match --watch

# aufzeichnen und danach im Browser zurückspulen: tools/Nova.AiLab/out/run1/player.html öffnen
dotnet run --project tools/Nova.AiLab -c Release -- match --view-every 25 --fog --out tools/Nova.AiLab/out/run1

# Seed-Matrix über alle Kerne, jeder 20. Lauf doppelt zur Selbstkontrolle
dotnet run --project tools/Nova.AiLab -c Release -- sweep --seeds 24 --out tools/Nova.AiLab/out/sweep

# die Gegentabelle: 576 Duelle in Sekunden (Issues 01/02)
dotnet run --project tools/Nova.AiLab -c Release -- duel --out tools/Nova.AiLab/out/duel

# die vier Bewegungsszenarien (Issue 03)
dotnet run --project tools/Nova.AiLab -c Release -- movement --out tools/Nova.AiLab/out/movement

# alle Kandidatenprofile gegen die eingefrorene Referenz: tools/Nova.AiLab/out/compare/report.html
dotnet run --project tools/Nova.AiLab -c Release -- compare --out tools/Nova.AiLab/out/compare

# gegen eine archivierte Ergebnismenge — verweigert bei fremdem Commit oder Definitionstabelle
dotnet run --project tools/Nova.AiLab -c Release -- compare --against tools/Nova.AiLab/out/alt/resultset.json --out tools/Nova.AiLab/out/compare2

# vier Slots (die Karte hat vier Eckplätze)
dotnet run --project tools/Nova.AiLab -c Release -- match --slots 4

dotnet test tools/Nova.AiLab.Tests/Nova.AiLab.Tests.csproj -c Release

# alle vier Laufarten in eine Seite: tools/Nova.AiLab/out/dashboard.html
python3 tools/Nova.AiLab/report/build_dashboard.py tools/Nova.AiLab/out
```

## Was hier liegt

Ein Ordner je Laufart — man findet alles über das Kommando, das man gerade fährt.
Alle Dateien liegen im selben Namespace `Nova.AiLab`; die Ordner gliedern, sie
trennen nicht. (Ein `Nova.AiLab.Movement` neben dem benutzten
`Nova.Simulation.Movement` wäre eine Namenskollision, die man sich einhandelt,
ohne etwas dafür zu bekommen.)

### `Match/` — eine Partie fahren

| Datei | Inhalt |
|---|---|
| `MatchSpec.cs` / `SpecFile.cs` | Eingabevertrag (§3.2) und sein JSON-Leser — unbekannte Schlüssel sind Fehler, keine Vorgabewerte |
| `CanonicalOpening.cs` | die D-077-Startaufstellung aus `MatchBootstrap`, Spawnreihenfolge inbegriffen |
| `MultiSlotAiHost.cs` | der Match-Host: `MatchRunner.InitializeMatch` von einem KI-Slot auf N verallgemeinert, sonst nichts |
| `CountingAiPeerTransport.cs` | zählt Intent-Verdikte — die einzige Stelle, an der `intentsRejected` ehrlich entsteht |
| `MatchRun.cs` | fährt eine Partie, liefert Outcome, Entscheidungstick, Hash-Kette, Trace |
| `RunArtifacts.cs` | `result.json`, `trace.ndjson`, `hashchain.json`, `view.ndjson`, `player.html` |

### `Metrics/` — messen, ohne einzugreifen

| Datei | Inhalt |
|---|---|
| `SlotMetrics.cs` / `TraceCollector.cs` | der Metrikkatalog aus §3.3, reiner Beobachter, nur Ganzzahlen |

### `View/` — hinsehen

| Datei | Inhalt |
|---|---|
| `ViewFrame.cs` / `ViewRecorder.cs` | die Sichtframes aus §3.4 — Tätigkeit, nicht nur Position; reiner Beobachter |
| `TerminalView.cs` | ANSI-Liveansicht, beantwortet „läuft gerade etwas schief?" |
| `HtmlPlayer.cs` | eine selbstständige Seite mit canvas: Scrubber, Einzeltick, Ebenen. Kein Build, kein Server |

### `Sweep/` — dieselbe Spec über viele Seeds

| Datei | Inhalt |
|---|---|
| `SeedSeries.cs` / `SweepRunner.cs` | Parallellauf mit Determinismus-Stichprobe (jeder 20. Lauf doppelt) |

### `Duel/` — die Gegentabelle

| Datei | Inhalt |
|---|---|
| `DuelArena.cs` / `DuelTable.cs` | AE-Parität, drei Abstände, beide Richtungen, Belagerung |

### `Movement/` — die vier Bewegungsszenarien

| Datei | Inhalt |
|---|---|
| `MovementScenarios.cs` | `arrival`, `blocking`, `standoff`, `detour` — Hindernisse sind Daten, nicht Code |

### `Compare/` — Kandidat gegen Referenz

| Datei | Inhalt |
|---|---|
| `LabProfiles.cs` | die Kandidatenprofile — heute die einzige Achse mit echter Varianz |
| `TournamentRunner.cs` | jeder Kandidat gegen die Referenz, in **beiden** Fraktionsrollen |
| `ResultSet.cs` / `ResultSetFile.cs` | Ergebnismenge mit Herkunft; verweigert den Vergleich, statt Unvergleichbares zu mischen |
| `ComparisonReport.cs` | der Bericht — Kennzahlen nebeneinander, **keine Rangliste** |
| `PrDraft.cs` | PR-Entwurf mit ausschliesslich Gemessenem; Beobachtungsabschnitt bleibt leer |

### `Cli/` — die Kommandozeile

| Datei | Inhalt |
|---|---|
| `Usage.cs` | der Hilfetext — die einzige Stelle, an der ein Modus in Worten steht |
| `Options.cs` | alle Flags; Spec-Datei als Basis, explizite Flags überschreiben sie |
| `MatchCommand.cs` … `CompareCommand.cs` | je ein Modus, je eine Datei: `match`, `sweep`, `duel`, `movement`, `compare` |

### Wurzel und `report/`

| Datei | Inhalt |
|---|---|
| `Program.cs` | nur `Main` und die Modus-Weiche — rund 50 Zeilen, sonst nichts |
| `report/build_dashboard.py` | fasst die Artefakte **aller vier Laufarten** zu `tools/Nova.AiLab/out/dashboard.html` zusammen — Kurven, Gegentabelle als Heatmap, Belagerung, Bewegung. Verdichtet nur, rechnet nichts dazu und vergibt keine Note |
| `report/dashboard.tpl.html` | die Seite dazu: eine Datei, kein Build, kein Server, kein Netzzugriff |

Das Testprojekt `../Nova.AiLab.Tests/` zieht diese Ordner mit **einem** Glob ein;
eine neue Datei steht damit automatisch unter Test. Ausgenommen sind nur
`Program.cs` und `Cli/` (Einstiegspunkt und sein Drumherum) sowie `bin/`, `obj/`
(generierter Code).

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
