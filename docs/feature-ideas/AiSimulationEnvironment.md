# KI-Simulationslabor — Plan

**Status:** Vorschlag, nicht gesetzt · **Strang:** Einheitenstrang (extern) · **Datum:** 2026-08-08
**Bezug:** [AIArchitecture](../tech/AIArchitecture.md), [SimulationCore](../tech/SimulationCore.md),
Issue `04-ki-die-reagiert`, `13-15_Parallelbetrieb.md`

---

## 0. Betriebsmodell

**Dieses Labor ist kein Beitrag ans Repository. Es ist Werkzeug.**

Es dient dazu, KI-Verhalten zu tunen und zu testen, indem tausende Partien
headless durchlaufen, statt jede Idee einzeln in Unity zu klicken. Es **muss
nicht gemergt werden** und bleibt lokal. Die Verhaltensänderung selbst landet
ohnehin nur über Unity im Spiel — das Labor hilft, die *richtige* schneller zu
finden.

| | **Labor** | **Beitrag** |
|---|---|---|
| Inhalt | `tools/Nova.AiLab/`, Traces, Auswertungen, dieses Dokument | Verhalten in `AI/`, `AI.Data/`, `Combat/`, `Movement/`, `Factions/` + Tests |
| Ort | Branch `lab/ai-simulation`, im Fork gesichert, **nie als PR nach `upstream`** | `feat/…`, PR nach `upstream/main` |
| Beweiskraft | **Diagnose** | Nachweis erst mit gespielter Runde |
| Scope | keine Frage — lokales Werkzeug berührt niemandes Schreibhoheit | volle Scope-Regeln |

**Drei Regeln, die daraus folgen:**

1. **Laborcode gerät nie in einen PR-Branch.** `feat/`-Branches werden frisch
   von `upstream/main` abgezweigt. Kein Cherry-Pick aus `lab/`. Leitplanke
   dazu: `.gitignore` kennt `tools/Nova.AiLab/` nicht — ein Eintrag in
   `.git/info/exclude` (lokal, berührt keine Repo-Datei) schützt vor einem
   versehentlichen `git add -A`.
2. **PR-Tests hängen nicht vom Labor ab.** Sie folgen dem Muster in
   `SkirmishAiTests.cs` — in sich geschlossen. Sonst ist der PR ohne das lokale
   Werkzeug nicht baubar.
3. **Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Das Repo behandelt
   seine `output/`-Artefakte schon so (D-061/D-063, D-067 K1; der SimRunner
   sagt es selbst: „values outside the … reference method are DIAGNOSIS").
   Was im Spiel nicht gesehen wurde, steht genau so im PR-Text.

---

## 1. Befund: die Simulation hängt nicht an Unity

| Beleg | Fundstelle |
|---|---|
| Kernel engine-frei | `Simulation/SimulationKernel.cs:13` |
| KI engine-frei | `AI/SkirmishAiSystem.cs:88` |
| Headless-Läufer, net8.0 | `tools/Nova.SimRunner/Program.cs` — `Main` in ~24 Zeilen, der Rest der 450-Zeilen-Datei ist Szenario-Dispatch |
| Vollständige KI-Partie headless | `tools/Nova.SimRunner.Tests/SkirmishAiTests.cs` — Entscheidung bei Tick **2241** (gemessen; die Testdoku in `SkirmishAiTests.cs:52` sagt 2242, asserted ist ohnehin nur `<= 6000`) |
| 10.000-Tick-Determinismuslauf | `tools/Nova.SimRunner/Determinism10000Scenario.cs` |

Entscheidend ist das *Wie*: Die Headless-Lane kompiliert per
`<Compile Include="..\..\Assets\_Project\Scripts\...">` **dieselben
Quelldateien**, die Unity lädt — `SimulationCore.md` §9 („Plattform- und
Assembly-Parität": gleiche Quellen, gleiche Defines, kopierte Logik ist
unzulässig).

**Eine Lücke im Ist-Zustand, die das Labor selbst schließen muss** — seit E1
geschlossen, `Nova.AiLab.csproj` linkt beide Verzeichnisse:
`Nova.SimRunner.csproj` linkt `Core`, `Simulation` und `Networking` — **nicht
`AI/`**, obwohl §9 die `Nova.AI`-Quellen nennt. Die KI-Partie läuft headless
heute nur über das Tests-Projekt. `Nova.AiLab` bindet die `AI/`- und
`AI.Data/`-Quellen deshalb selbst per `<Compile Include>` ein; das
SimRunner-csproj taugt als Muster, nicht als Kopiervorlage.

> „1:1 wie im echten Spiel" ist damit eine **strukturelle Eigenschaft**, keine
> Disziplinleistung: es kann nicht auseinanderlaufen, weil es nur eine Quelle
> gibt.

Jeder Nachbau in einer zweiten Sprache würde diese Eigenschaft gegen eine
dauerhafte Synchronisationspflicht eintauschen — bei ~15.000 Zeilen, von denen
jede determinismusrelevant ist (Q16.16 mit round-half-even, xorshift128+,
xxHash64, byte-genaue Serialisierung, bis hin zur Duell-Asymmetrie im Combat).
**Also: kein Nachbau, sondern eine Umgebung um das Vorhandene, in C#.**

---

## 2. Anforderungen und Stand

| # | Anforderung | Stand |
|---|---|---|
| **R1** | Simulation ohne Unity | ✅ vorhanden |
| **R2** | Parallel, vielfach, schnell | ✅ seit E2 — 143.000 Ticks/s über 24 Kerne |
| **R3** | Bit-identisch zum Spiel | ✅ strukturell garantiert |
| **R4** | KI gegen KI | ⚠️ im Labor baubar, im Spiel gesperrt |
| **R5** | 2 gegen 2 | ❌ kein Team-Begriff |
| **R6** | Verhalten A–Z | ⚠️ 5 von 13 Befehlsarten genutzt (§6) |
| **R7** | Reagierendes Goal-System | ❌ KI ist bewusst zustandslos |
| **R8** | Eine Stelle zum Ändern | ❌ Werte stecken im Code |
| **R9** | 2D-Sichtfenster | ✅ seit E3 — Terminal live, HTML-Abspieler zur Nachschau |
| **R10** | Waffen-, Rüstungs- und Bewegungsarbeit messbar (Issues 01–03) | ✅ seit E5 — `duel` und `movement`, Sekunden statt Partieauswertung |

R6–R10 sind die Arbeit.

---

## 3. Aufbau des Labors

```
tools/Nova.AiLab/  (eigenes csproj, net8.0, linkt dieselben Quellen)
  ├─ MultiSlotAiHost   2..8 Slots, je KI-Slot eigene Session/Ingress/Transport
  ├─ MatchSpec/Result  Ein- und Ausgabevertrag
  ├─ TraceCollector    Integer-Metriken je n Ticks
  ├─ ViewRecorder      Sichtframes — reiner Beobachter (§3.4)
  ├─ SweepPlanner      Matrix aus Seeds × Profilen
  └─ Program           Parallel.For über Kerne
        │                              │
        ▼ live                         ▼ nach dem Lauf
  Terminalansicht (ANSI)        HTML-Abspieler (canvas, eine Datei)
```

### 3.1 `MultiSlotAiHost`

Grundlage ist der `AiHost` aus `SkirmishAiTests.cs` — laut eigener Doku ein
*„byte-exact wiring mirror of MatchRunner.InitializeMatch"*. Zu wissen: die
Datei existiert **doppelt** (`tools/Nova.SimRunner.Tests/` und
`Assets/Tests/EditMode/AI/`, manuell synchron zu halten); das Labor spiegelt
nur die tools-Seite, Verhaltens-PRs ziehen beide Kopien nach.

Zu verallgemeinern: Slots von 2 auf N (≤ 8), je KI-Slot eigene `MatchSession` +
`CommandIngress` + `AiPeerCommandTransport` in die eine Host-Ingress. Offen
dafür: `CommandLimits.ReservedPlayerSlots = 8`, `FogOfWarSystem.MaxTeams = 8`,
`VictorySystem.MaxSlots = 8`.

**Registrierungsreihenfolge unverändert** — sie ist Vertrag
(`13-15_Parallelbetrieb.md`, Abschnitt „Neue Systeme — wer die Tick-Reihenfolge
setzt"): Economy → Construction → Production → Pathfinding → Movement →
FogOfWar → Combat → [KI-Slots] → Victory. Ein Test nagelt sie gegen
`MatchRunner` fest — Vorbild existiert schon: `CanonicalMatchSetupTests.cs:91`
pinnt genau diese Reihenfolge inklusive `SkirmishAiSystem`.

**Isolation:** Jedes Match baut Kernel, `EntityManager` und alle Systeme frisch.
Geteilt wird nur Unveränderliches (`SimDefinitions`, `WeaponProfiles`,
`DamageMatrix` sind `static readonly`, damit thread-sicher) — N Matches auf N
Kernen, ohne Sperren. Geprüft: Unter `Simulation/`, `Core/` und `AI/` gibt es
**kein einziges nicht-readonly statisches Feld**; alle Systeme sind
Instanzklassen. Die Stichproben-Doppelläufe aus §3.7 bewachen, dass das so
bleibt.

### 3.2 Ein- und Ausgabe

```json
{ "specVersion": 1, "mode": "match", "seed": "0xA17E57DE57", "tickBudget": 27000,
  "mapWidth": 128, "mapHeight": 128, "entityCapacity": 1024,
  "slots": [
    { "slot": 0, "faction": "legion",   "controller": "ai", "profile": "legion-aggressive" },
    { "slot": 1, "faction": "alliance", "controller": "ai", "profile": "alliance-turtle" }],
  "traceIntervalTicks": 10, "viewIntervalTicks": 5, "hashIntervalTicks": 100 }
```

| Datei | Inhalt |
|---|---|
| `result.json` | Outcome, Siegerslot, Entscheidungstick, Endzustands-Hash, `ComputeDefinitionsHash64()`, Fingerprint |
| `trace.ndjson` | eine Zeile je Metriktick, ausschließlich Ganzzahlen |
| `view.ndjson` | eine Zeile je Sichtframe (§3.4) |
| `hashchain.json` | `kernel.CalculateStateHash()` alle *n* Ticks |
| `match.replay` | `ReplayRecorder.Finalize(...)` — im echten Spiel abspielbar |

**Harte Regel:** Kein Float verlässt die Simulation. Positionen als
Q16.16-Rohwerte, alles andere ganzzahlig — sonst ist der Vergleich zweier Läufe
Glückssache statt Rechnung.

`tickBudget` steht standardmäßig auf **27.000 wie im Spiel**
(`VictorySystem.TimeLimitTick`, 45 Min Simzeit), ist aber je Spec überschreibbar.
Der Standard bleibt der Spielwert, damit ein Laborergebnis ohne Fußnote gilt; wer
kürzt, verzerrt zugunsten schneller Strategien und muss das wissen. `mode` wählt
die Laufart (§3.9).

### 3.3 Metriken

Je Metriktick und Slot, alles aus dem committed State:

| Gruppe | Metriken | Quelle |
|---|---|---|
| Wirtschaft | `credits`, `powerProvided`, `powerRequired`, `isLowPower` | `PlayerEconomyState` |
| Ernte | `harvesters`, `idleHarvesters`, `cargoInTransit`, `fieldReserveAE` | `UnitState`, `TryGetField` |
| Bau | `sitesOpen`, `buildingsByRole[9]` | `TryGetSite`, `HasFinishedBuilding` |
| Produktion | `queuedByRole`, `queueStallTicks` | `TryGetProducer`/`TryGetQueueEntry` |
| Armee | `armySize`, `armyHealthSum`, `losses` | Entity-Scan |
| Gefecht | `damageDealt`, `damageTaken`, `kills` | Differenz je Intervall |
| Sicht | `visibleEnemyUnits`, `visibleEnemyBuildings` | `GetVisibleEntities` |
| KI | `activeGoal`, `goalUtility`, `goalSwitches`, `intentsSubmitted`, `intentsRejected` | Goal-System, `AiPeerCommandTransport.LastResult` |

`intentsRejected` ist die unterschätzte Zahl: Sie zeigt, wo die KI gegen
Executor-Regeln anrennt — heute schweigend, weil `Submit()` den Verdikt
absichtlich nicht auswertet. `goalSwitches` ist das Frühwarnsignal für
Zielzappeln. Keine dieser Zahlen wird zu einer Gesamtnote verrechnet — warum
nicht, steht in §3.6.

### 3.4 Das 2D-Sichtfenster

Zahlen sagen *dass* etwas schiefging, nicht *was*: eine Siegrate von 40 %
erklärt nicht, dass die halbe Armee an einer Gebäudeecke hängt.

**Harte Bedingung: reiner Beobachter.** Liest den committed State nach
`StepTick()`, schreibt nie zurück, ist nicht Teil von Tickreihenfolge,
Zustands-Hash oder Snapshot. Ein Lauf mit und ohne Sichtfenster muss dieselbe
Hash-Kette liefern — als Test, nicht als Vorsatz.

Beide Darstellungen lesen denselben Frame-Strom:

- **Terminalansicht (live)** — ANSI, Raster heruntergerechnet, keine
  Abhängigkeiten. Beantwortet „läuft gerade etwas schief?"
- **HTML-Abspieler (Nachschau)** — eine statische Datei mit `<canvas>`, lädt
  `view.ndjson` daneben. Scrubber, Einzeltick, zuschaltbare Ebenen, zwei Läufe
  nebeneinander. Kein Build, kein Server.

Ein echtes Fenster (Avalonia/SDL) lohnt nicht: Fremdabhängigkeit und
Plattformpflege ohne Mehrwert gegenüber dem Abspieler.

**Kodiert wird Tätigkeit, nicht nur Position:**

| Kanal | Bedeutung |
|---|---|
| Grundfarbe | Besitzer-Slot (0..7) |
| Form | Gebäude ▣ · Baustelle ▢ · Builder ✚ · Harvester ● · Kampfeinheit ▲ |
| Helligkeit | `CurrentHealth * 100 / MaxHealth` |
| Linie zum Ziel | rot `AttackTarget` · grün `HarvestFieldId` · blau `GoalGridPos` bei `IsMoving` |
| Hohle Füllung | `IsReturningCargo` |
| Randmarkierung | unter Rückzugsschwelle |
| Kopfzeile je Slot | aktuelles Ziel, Nutzwert, Credits, Strommarge, Armeegröße |

**Zwei Ebenen, die beim Debuggen den Unterschied machen:** Fog of War je Team
(`GetTeamView`) — die häufigste Erklärung für „die KI hat nicht reagiert" ist,
dass sie nichts sehen konnte; und verworfene Intents als kurzes Aufblinken am
Ort, was `intentsRejected` räumlich macht.

Größe: ~200 Entitäten × ~30 Byte × 600 Frames ≈ 4 MB je Partie. Über
`viewIntervalTicks` regelbar.

### 3.5 Wie „1:1" bewiesen wird

1. **Gleiche Quellen** — Divergenz ist nicht unwahrscheinlich, sondern unmöglich.
2. **Gleiche Verdrahtung** — Reihenfolge-Test gegen `MatchRunner`.
3. **Gleiche Hash-Kette** — zwei Läufe mit gleichem Spec liefern identische
   Ketten; der Selbsttest, den `Determinism10000Scenario` vormacht.
4. **Replay-Konformanz** — der aufgezeichnete Command-Strom wird im echten
   Unity-Spiel abgespielt; gleicher Endzustands-Hash = nachgewiesen, nicht
   behauptet. `MatchFingerprint` verweigert den Start bei Abweichung — heute
   wirksam über `DefinitionsHash64` plus Schema-Versionen; Rules- und Map-Hash
   sind noch leere Q-040-Stubs, gegen geänderte Regeln oder Karten schützt der
   Fingerprint also (noch) nicht. **Das ist die Brücke zurück:** Was das Labor
   findet, wird so gegengeprüft, bevor es als „gesehen" gilt.
   Offene Abhängigkeit: Diese Brücke braucht ein spielbares Build auf unserer
   Plattform — der **Linux-Build ist laut v1.1.0 Bringschuld des Netzstrangs**
   und steht aus. Bis dahin trägt die Konformanzprüfung nur so weit, wie ein
   Testbuild reicht; was nicht gespielt wurde, steht als ungespielt im PR.

### 3.6 Bewertung: Vergleich statt Rangliste

**Das Labor rankt nicht. Es legt nebeneinander.**

Es gibt bewusst *keine* skalare Gütefunktion, aus der eine Bestenliste fällt.
Ein Vergleichsbericht zeigt Siegrate, Zeit bis Entscheidung, Wirtschaftskurve,
Verluste, `goalSwitches` und `intentsRejected` nebeneinander; die Auswahl trifft
ein Mensch — mit dem Sichtfenster daneben.

Der Grund ist nicht Bequemlichkeit. Eine einzelne Zahl belohnt zuverlässig das
Falsche: eine KI, die 5 % häufiger gewinnt, weil sie den Gegner mit Bauarbeitern
zumüllt, ist keine bessere KI. Und für „sieht im Spiel richtig aus" existiert
keine Kennzahl.

**Folgen, die man aussprechen muss:**

- **Kein automatischer Optimierer.** Rastersuche erzeugt Kandidaten, sie wählt
  nicht aus. Verfahren, die einen Skalar brauchen, sind damit aus dem Plan —
  nicht vertagt, sondern nicht vorgesehen.
- **Der Vergleichsbericht ist ein Produkt, kein Nebenprodukt.** Er muss so
  lesbar sein, dass die Auswahl in Minuten fällt, nicht in einer Stunde
  Tabellenlesen. Kernform: eine Zeile je Kandidat, Spalten je Kennzahl,
  Abweichung zur Referenz farbig, Link zum Sichtfenster-Lauf.
- **Das Sichtfenster wird dadurch Teil der Bewertung**, nicht nur der
  Fehlersuche. Das ist der Grund, warum es vor dem Sweep steht (E3 vor E4).

### 3.7 Gegnerarchiv: gegen frühere Fassungen seiner selbst

Getunt wird gegen zwei Sorten Referenz: die **eingefrorene heutige KI** als
unbeweglicher Maßstab, und **Momentaufnahmen früherer eigener Fassungen**, damit
Fortschritt über Monate vergleichbar bleibt und ein Rückschritt auffällt.

Dabei gibt es einen Unterschied, an dem man sich sonst verrechnet:

| Was eingefroren wird | Mechanik | Vergleich |
|---|---|---|
| **Profil** (ab E6 nur Daten) | alte Profildatei läuft im aktuellen Binary | echtes Kopf-an-Kopf im selben Lauf |
| **Codestand** (Goal-System, E7+) | läuft *nicht* im selben Binary | nur über **eingefrorene Ergebnismengen**: gleiche Seeds, gleiche Spec, gespeicherte Kennzahlen |

Der zweite Fall ist der Normalfall bei Verhaltensarbeit — und er stellt eine
Bedingung: Die **Referenz-Seedmenge und die Spec-Version müssen fixiert sein**.
Ändert sich die Startaufstellung oder die Spec, ist jeder Vergleich mit alten
Ergebnismengen wertlos, und das muss auffallen statt durchzurutschen. Deshalb
trägt jede Ergebnismenge Spec-Version, Seedliste und `ComputeDefinitionsHash64()`
mit; passt eines nicht, verweigert der Bericht den Vergleich.

**Tuningmenge und Referenzmenge bleiben getrennt.** Sonst gewinnt die KI
Benchmarks und verliert Partien.

**Alterung durch Merge-Fenster.** Der Maintainer sammelt simulationsändernde
PRs und mergt sie in Fenstern; jedes Fenster verschiebt das Verhalten und macht
eingefrorene Vergleichsdaten ungültig. Antwort: **nach jedem Fenster werden
Referenzmenge und Archiv einmal komplett neu vermessen.** Alte Mengen werden mit
ihrem Commit archiviert, nicht gelöscht — sie bleiben lesbar, nur nicht mehr
vergleichbar. Jede Menge trägt den Commit; der Bericht verweigert den Vergleich
über Commit-Grenzen.

Damit hängt die Laborkadenz an den Merge-Fenstern, und das hat eine Folge, die
den Etappenplan stützt:

> **Ein Profil-Archiv überlebt ein Merge-Fenster, ein Code-Archiv nicht.** Eine
> alte Profildatei lässt sich auf dem neuen Stand neu vermessen; ein alter
> Codestand ist weg und seine Ergebnismenge geht mit ihm in Rente. Je früher
> Verhaltensunterschiede in Daten liegen (E6), desto länger bleiben Vergleiche
> gültig.

**Selbstkontrolle im Sweep:** Jeder zwanzigste Lauf wird doppelt gefahren und
die Hash-Kette verglichen. Kostet 5 % Rechenzeit und fängt genau den Fehler, der
sonst unentdeckt bliebe — geteilter Zustand zwischen parallel laufenden Matches,
der erst bei voller Kernauslastung auftritt und sich als „unerklärliche Streuung"
tarnt. Ein einzelner Determinismus-Test in der Suite würde ihn nie sehen.

### 3.8 Befundliste für fremdes Terrain

Das Labor wird Dinge aufdecken, die uns nicht gehören — in Economy,
Pathfinding, Production, Vision. Diese Funde werden gesammelt und weitergegeben,
nicht selbst repariert (Arbeitsvertrag §6: „fragen, nicht entscheiden").

Je Befund im Labor unter `findings/`: Beobachtung, betroffener Pfad und
Eigentümer, **Seed und MatchSpec zur Reproduktion**, das `match.replay`, und die
Fundstelle im Code. Damit ist ein Fund für den Maintainer nachvollziehbar,
statt eine Behauptung zu sein — und das ist der einzige Weg, wie er je behoben
wird.

**Der Weg nach draußen ist nicht der PR.** `docs/production/hashkrieg/` gehört
laut v1.1.0 dem Maintainer, und dort steht ausdrücklich: „Befunde kommen per Mail
oder Issue, nicht per PR." Die `findings/`-Einträge sind also Vorlage für eine
Meldung, nicht selbst der Beitrag.

Ein erster Kandidat: **explizite `AttackTarget`-Befehle prüfen die `PlayerId`
nicht.** `ValidateDomain` (`UnitCommandStateView.cs:174`) hat für
`AttackTarget` keinen Case, und die Feuerphase im `CombatSystem` prüft nur
Reichweite und Sichtbarkeit — Friendly Fire per Befehl ist möglich, während
die Auto-Acquisition (D-087) strikt feindlich filtert. Für die KI-Arbeit heißt
das: Das Score-Targeting muss eigene Einheiten selbst ausfiltern, der Executor
tut es nicht.


### 3.9 Drei Laufarten

Eine 20-Minuten-Partie ist ein schlechtes Messgerät für eine Waffenzahl: zu viel
Rauschen, zu wenig Wiederholung. Deshalb kennt das Labor neben der Partie zwei
schmale Laufarten. Sie kosten wenig, weil sie denselben Host benutzen.

**Bedingung, die alle drei teilen: identische Systemregistrierung.** Auch die
Arena registriert Economy, Construction und Production — sie ticken nur über
leere Tabellen. Ein weggelassenes System wäre eine andere Tick-Reihenfolge und
damit ein anderes Spiel; dann misst man etwas, das es nicht gibt.

Bekannte harte Kappen für Skalierungsläufe: `MaxProducers = 64` und
`MaxRepairOrders = 64` — jenseits davon wird abgelehnt, nicht gepuffert. Für
kanonische Partien irrelevant, für einen Massen-Sweep eine Grenze, die der
Bericht ausweisen muss statt sie als Verhalten zu deuten.

| Laufart | Aufbau | Dauer | Für |
|---|---|---|---|
| `match` | kanonische Aufstellung, KI je Slot | Minuten | Goal-System, R6–R9 |
| `duel` | N gegen M Einheiten auf leerem Feld, keine Wirtschaft | Sekunden | **Issues 01/02** — Legion-Waffen, Rüstungsklassen |
| `movement` | eine Gruppe, ein Zielbefehl, Hindernisse gesetzt | Sekunden | **Issue 03** — Bewegung am Ziel |

**`duel` — die Gegentabelle empirisch.** Misst über alle Rollenpaare beider
Fraktionen, was Issue 02 verlangt: *welche Waffe schlägt welche Rüstung wie
deutlich* — gemessen statt aus `DamageMatrix` abgelesen. Der Unterschied ist
wesentlich: Der Matrixwert nennt den Multiplikator, der Duellausgang zeigt, was
Reichweite, Nachladezeit und Lebenspunkte daraus machen.

Vier Festlegungen, ohne die eine Duelltabelle nichts aussagt:

**Parität über AE-Kosten, nicht über Stückzahl.** Beide Seiten bekommen dasselbe
Budget, die Stückzahl folgt daraus (`floor(budget / CostAE)`). Gleiche Stückzahl
wäre kein Befund — ein doppelt so teurer Panzer *soll* einen Infanteristen
schlagen. Der Rest, der nicht aufgeht, wird je Seite protokolliert; Paarungen mit
über 10 % Restbetrag markiert der Bericht, weil dort die Parität selbst wackelt.
Das Budget wird so gewählt, dass beide Seiten mindestens vier Einheiten stellen.

**Echter Fog of War, wie im Spiel.** `CombatSystem` verlangt das Ziel als
`Visible` in der committed Team-Sicht — Standardsichtweite ist 10 m, Artillerie
schießt darüber hinaus (Allianz 20, Legion 18 Tiles). Dass Artillerie ihre
Reichweite ohne Aufklärung nicht nutzen kann, ist damit ein echter
Balance-Befund und kein Messfehler. Zu beachten: Die Sicht wird nur mit 5 Hz
neu berechnet, zwischen zwei Commits gilt die letzte Maske — ein Ziel bleibt
also bis zu zwei Ticks länger beschießbar.

**Jede Einheiten-Zahl ist fraktionsgebunden.** Die Werte sind absichtlich
asymmetrisch — Artillerie 20/18 Tiles und 110/60 Schaden, Harvester-Cargo
330/300 AE (Allianz/Legion). Genau diese Asymmetrie ist die
Legion-Waffenidentität aus Issue 01; ein Bericht, der „die Artillerie" ohne
Fraktion nennt, mittelt zwei verschiedene Waffen. Dazu gehört die Einordnung:
Die Duell-Arena *misst* Issue 01, die *Umsetzung* neuer Legion-Werte hängt an
der `Definitions/`-Absprache — die kanonischen Werte liegen in
`SimDefinitions.cs`, geteilte Vertragsfläche; Issue 01 ist deshalb bis zur
Absprache als blockiert markiert.

**Drei Startabstände statt einem.** Ein einzelner Abstand entscheidet die halbe
Tabelle vor:

| Staffel | Abstand | Was sie misst |
|---|---|---|
| kurz | Berührung | Schaden, Rüstung, Nachladezeit — Reichweite spielt keine Rolle |
| mittel | längste Reichweite der Paarung | ob die längere Waffe ihre Freischüsse tatsächlich bekommt |
| lang | außerhalb jeder Sicht | Annäherung und Aufklärung, nicht nur Feuerkraft |

**Auf lange Distanz braucht es einen Bewegungsbefehl** — sonst passiert nichts.
Auto-Acquisition (D-087) greift nur auf *sichtbare Ziele in Reichweite*; ohne
Sicht steht beides still, bis das Tickbudget abläuft. Beide Seiten bekommen
deshalb einen Move-Intent auf die Mitte der Gegenseite. Damit misst die lange
Staffel zusätzlich das Annäherungsverhalten — was gewollt ist, aber beim Lesen
der Zahlen mitgedacht gehört.

**Belagerung als eigene Staffel.** Die Rüstungsklasse `Building` ist eine ganze
Spalte der Gegenmatrix — Kinetik trifft sie mit 30 %, Explosiv mit 75 %. Ohne
sie bleibt ein Drittel der Gegenlogik ungeprüft, und „womit reiße ich eine Basis
ein" ist die Hälfte dessen, was eine Waffe leisten muss. Gebäude schießen nicht
zurück, einzige Ausnahme ist die `DefensePlatform`; gemessen wird deshalb anders:
Ticks bis zum Abriss, eingesetztes AE gegen Gebäudekosten, und bei der
`DefensePlatform` zusätzlich die Verluste des Angreifers. Erwartung aus der
Matrix ist ein Faktor 2,5 zwischen Kinetik und Explosiv — die Messung zeigt, was
Nachladezeit, Reichweite und Gebäude-Lebenspunkte daraus machen.

**Jede Paarung läuft in beide Richtungen.** Die dokumentierte
**Duell-Asymmetrie** — bei gegenseitigem Kill im selben Tick gewinnt der
niedrigere Entity-Index — macht A-gegen-B und B-gegen-A zu zwei verschiedenen
Messungen. Weichen sie auseinander, ist die Paarung so knapp, dass die
Spawnreihenfolge entscheidet. Das ist selbst ein Befund und gehört in den
Bericht, nicht wegkalibriert.

**`movement` — vier Szenarien.** Aufbau ist jeweils Daten, nicht Code: eine
Hindernisliste (`PlaceCompletedBuilding` — Fußabdrücke sind seit dem
Truppenführungs-Sprint unpassierbar), eine Einheitengruppe, ein Befehl.

| Szenario | Aufbau | Gemessen |
|---|---|---|
| `arrival` | Gruppe, freies Feld, ein Zielbefehl | Ticks bis zur Ankunft, Anteil angekommen, Streuung als größte Chebyshev-Distanz zum Zielzentrum |
| `blocking` | zwei kreuzende Gruppen; große Gruppe durch eine Engstelle zwischen Fußabdrücken | Einheiten mit `IsMoving` und unveränderter Position über K Ticks: Anzahl, Gesamtdauer, längste einzelne Blockade |
| `standoff` | Fernkämpfer mit Angriffsbefehl auf ein stehendes Ziel | kleinster erreichter Zentrumsabstand gegen die eigene `AttackRange` — der „Überlauf" ist die Zahl, die Issue 03 meint |
| `detour` | Ziel hinter einer Gebäudewand mit einem Durchlass | Weglänge gegen Luftlinie, Ankunftszeit, ob überhaupt jemand ankommt |

`standoff` braucht Combat im Lauf und ist damit ein Mischszenario — das ist
gewollt: Abstandhalten *ist* eine Kampfeigenschaft, kein reines Bewegungsthema.
`detour` prüft Flow-Field und `CostField` unmittelbar.

Seit v1.1.0 gehört auch `Simulation/Pathfinding/` uns — Flow-Field und
`CostField` inbegriffen, unter der `IsWalkable`-Auflage. Der ganze Weg vom
Befehl bis zur Ankunft liegt damit im eigenen Scope, und diese Laufart deckt
ihn ab.

### 3.10 Berichte und PR-Entwurf

**Der Vergleichsbericht ist eine HTML-Datei**, gleiche Machart wie der
Abspieler: eine selbstständige Seite, sortierbare Tabelle, Verlaufskurven inline,
Abweichung zur Referenz farbig, Klick führt in den zugehörigen
Sichtfenster-Lauf. Weil es keine Rangliste gibt (§3.6), *ist* die Lesbarkeit das
Produkt — und Technik wie Machart teilt sich der Bericht mit dem Abspieler, der
Zusatzaufwand ist entsprechend klein.

**PR-Textentwurf.** Zu jedem Vorher/Nachher-Vergleich schreibt das Labor einen
Entwurf für den späteren PR: geänderte Kennzahlen mit alten und neuen Werten,
verwendete Seeds, Verweis auf das `match.replay`, betroffene Dateien und der
Hinweis, welche der vier Baseline-Dateien dadurch rot wird.

Eine Grenze gilt dabei absolut: **Der Entwurf enthält ausschließlich Gemessenes.**
Der Abschnitt für die gespielte Beobachtung bleibt leer und ist als leer
erkennbar; kein generierter Satz formuliert ein Laborergebnis so, als sei es im
Spiel gesehen worden. „Nichts als fertig melden, was nicht gelaufen ist" ist die
wichtigste Regel des Repos — ein Werkzeug, das sie bequem umgehen lässt, wäre
schlechter als gar keins.

---

## 4. Das Goal-System

### 4.1 Warum die heutige KI nicht reagieren *kann*

> *„the system is STATELESS (every decision is a pure function of the tick and
> the committed state — no timers, no memory)"* — `SkirmishAiSystem.cs:40`

Für G1 richtig — und genau der Grund, warum sie beim Angriff weiter Harvester
queued. Ein Ziel über mehrere Entscheidungsticks *ist* Gedächtnis.

### 4.2 Der Trick: die Welt ist das Gedächtnis

Ein Snapshot-Block wäre eine Inhaberentscheidung (`Simulation/Snapshots/` steht
auf „niemand ohne D-ID"). Vorher lohnt die Prüfung, wie weit man ohne kommt —
und das ist weit, weil der committed State die Vergangenheit implizit trägt.

**Die stehenden Befehle der eigenen Einheiten *sind* der Zustand des
Goal-Systems.** `TargetGridPos`, `AttackTarget`, `HarvestFieldId`,
`IsReturningCargo` speichern, was die KI zuletzt wollte — an einer Stelle, die
ohnehin serialisiert wird. Die heutige KI nutzt das schon, nur zur
Doppelbefehl-Unterdrückung (`AlreadyHeadingTo`). Dasselbe Signal trägt: *„meine
Armee steht auf Heimatkurs → ich bin bereits im Verteidigungsziel."* Damit ist
Hysterese ohne Sidecar möglich.

| Signal | Ableitung | API |
|---|---|---|
| Angriff auf Basis | Summe Waffenschaden sichtbarer Feinde in Radius *r* um HQ | `GetVisibleEntities` + `WeaponProfiles.Get` |
| Angriff aufs Feld | dito um das eigene Aetherium-Feld | + `TryGetField` |
| verliere gerade | `Σ(MaxHealth − CurrentHealth)` eigener Einheiten | Entity-Scan |
| Geld fehlt | Credits unter Schwelle **und** Bauwunsch offen | `PlayerEconomyState` |
| Ernte steht | `HarvestFieldId == 0 && !IsReturningCargo` | `UnitState` |
| Strom fehlt | `PowerProvided − PowerRequired < Reserve` | `PlayerEconomyState` |
| Einheit muss weg | `CurrentHealth * 100 / MaxHealth < Schwelle` | `UnitState` |
| aktuelles Ziel | stehende Befehle der eigenen Einheiten | `TargetGridPos`, `AttackTarget` |

**Die Grenze davon** — das sind später die Sidecar-Argumente: keine Timer
(Mindeststandzeit in Ticks), kein Aufklärungsgedächtnis, keine Squad-Identität
über das hinaus, was Befehle kodieren, keine Zeitreihen (Schadens*rate* statt
Schadenssumme).

### 4.3 Zielauswahl

Ganzzahliger Nutzwert 0..1000, höchster gewinnt, Gleichstand nach fester
Zielreihenfolge:

```
U(DefendBase)  = min(1000, threatAtBase  * W_defBase  / 10)
U(DefendField) = min(1000, threatAtField * W_defField / 10)
U(Farm)        = credits >= C_target ? 0 : (C_target - credits) * W_farm / C_target
U(PowerUp)     = powerMargin < reserve ? W_power : 0
U(ArmyUp)      = max(0, armyTarget - army) * W_army
U(Push)        = (army >= pushThreshold && threatAtBase == 0)
                 ? W_push + min(200, army * 10) : 0
```

`threatAtBase` = Summe von `WeaponProfiles.Get(faction, role).AttackDamage` über
sichtbare Feinde in Reichweite. Ganzzahlig — und ein unbewaffneter Harvester am
Zaun erzeugt korrekt 0 Bedrohung. Zielwechsel weg vom abgeleiteten aktuellen
Ziel verlangt Vorsprung `Δ`, nicht nur `>`.

| Ziel | Auslöser | Aktion (alles über `CommandIntent`) |
|---|---|---|
| `Bootstrap` | kein Refinery | Refinery setzen, Builder nachziehen |
| `Expand` | Wirtschaft läuft, Platz frei | Barracks / Power / VehicleFactory |
| `Farm` | Credits unter Schwelle | Harvester bauen, Untätige aufs Feld |
| `PowerUp` | Stromreserve unter Profilwert | Power vorziehen |
| `TechUp` | T2 verlangt, ResearchLab fehlt | ResearchLab setzen |
| `ArmyUp` | Armee unter Soll, Geld da | Einheiten queuen |
| `Scout` | keine Feindsichtung | billige Einheit Richtung Feindbasis |
| `DefendBase` | sichtbarer Feind nahe HQ/Produktion | Armee heim, Ziele nach Score |
| `DefendField` | sichtbarer Feind nahe eigenem Feld (13B · B4) | Teilarmee ans Feld, Harvester ausweichen |
| `Push` | Armee über Schwelle, Basis sicher | Vormarsch auf Feindbasis |
| `Retreat` | *pro Einheit*, unter Lebensschwelle | Move-Intent Richtung Basis |

`Retreat` ist bewusst kein globales Ziel, sondern ein Filter über Einheiten —
sonst zöge sich die Armee wegen eines angeschlagenen Spähers zurück.

### 4.4 Zielpriorisierung als Score

Ersetzt das heutige „HQ vor Gebäude vor Einheit" aus `FindPreferredVisibleEnemy`:

```
score = W_dmg    * DamageMatrix.Resolve(my.AttackDamage, my.DamageType, target.ArmorClass)
      + W_threat * targetProfile.AttackDamage
      + W_finish * (100 - target.CurrentHealth * 100 / target.MaxHealth)
      - W_dist   * chebyshevCells
Gleichstand → niedrigere rohe Entity-Id
```

`DamageMatrix` liefert **Integer-Prozent** (100 == 1.00) — passt ohne Umrechnung
ins Scoring. Damit läuft die Armee nicht mehr am Panzer vorbei aufs Lagerhaus:
Kinetik trifft Medium mit 50 % und Gebäude mit 30 %, Explosiv mit 100 % / 75 %.

### 4.5 GB-002 gegen D-087

Die Klassendoku von `SkirmishAiSystem` beruft sich auf GB-002 („kein
Auto-Acquire, Befehle sind zwingend"). `CombatSystem.cs:20` beschreibt unter
**D-087 bereits Auto-Acquisition**.

**Der Code gilt** — D-087 ist implementiert und neuer. Konsequenz, die man
kennen muss: *explizite* Angriffsbefehle werden nie überschrieben („explicit
orders are never retargeted"), das Score-Targeting hat also Vorrang vor der
Automatik und übernimmt die Verantwortung, nicht schlechter zu zielen als sie.
Die veraltete Doku-Passage ist ein eigener, winziger PR in `AI/` — zusammen
mit der zweiten veralteten Passage aus §5 (`SetRallyPoint`/Refinery,
`SkirmishAiSystem.cs:63`). Kein Blocker.

### 4.6 `AI.Data/` — die eine Stelle zum Tunen

Enthält heute **nur ein asmdef**, gehört uns exklusiv, ist genau dafür gedacht.
Die Werte stecken verstreut in `AiFactionProfile`-Defaults (`TargetPowerMargin
= 30`, `TargetArmySize = 15`, `AttackSquadThreshold = 8`,
`TargetHarvesterCount = 2`) und `const` im System (`DecisionTickInterval = 20`,
`PlacementSearchRadius = 8`, `InfantryQueueBatch = 2`,
`HarvesterQueueBatch = 2`).

Drei Vorarbeiten, die die Migration sonst stolpern lassen:

- `AiFactionProfile.Equals`/`GetHashCode` vergleichen **nur den
  `FactionName`** — zwei Profile mit gleichem Namen und verschiedenen Zahlen
  gelten als gleich. Die `profileId`-Identität muss das explizit auflösen,
  nicht erben.
- Das asmdef in `AI.Data/` steht auf `"noEngineReferences": false` (anders als
  `Nova.AI` mit `true`). Vor E6 auf `true` ziehen — die Datenschicht soll
  strukturell enginefrei sein, nicht nur zufällig.
- Das Labor-csproj muss `AI.Data/` mitlinken (§1: der SimRunner linkt heute
  nicht einmal `AI/`).

```json
{ "profileId": "legion-aggressive", "schemaVersion": 1,
  "cadence": { "decisionTickInterval": 20 },
  "economy": { "creditTarget": 2000, "targetHarvesters": 4, "powerReserve": 20 },
  "army":    { "targetSize": 20, "pushThreshold": 10, "queueBatch": 2 },
  "goalWeights":   { "defBase": 40, "defField": 25, "farm": 30, "power": 500,
                     "army": 20, "push": 300, "switchHysteresis": 80 },
  "targetWeights": { "dmg": 10, "threat": 6, "finish": 3, "dist": 4 },
  "retreat": { "enterHealthPercent": 25, "exitHealthPercent": 60 } }
```

Zwei bindende Regeln:

- **Nur ganze Zahlen.** Ein Float in einer Profildatei ist ein Float in der
  Simulation — `NoFloatInSimulationTests` prüft mit.
- **Ausgelieferte Profile behalten exakt die heutigen Werte.** Dann ändert die
  Umstellung von `const` auf Daten **kein Verhalten**, die Baselines bleiben
  grün, und genau das ist der Beweis, dass der Umbau sauber war.

Abweichende Profile existieren zunächst nur im Labor. Ob eine Profil-ID
fingerprintrelevanter Inhalt ist (analog zur Fraktion je Slot), stellt sich erst,
wenn ein abweichendes Profil ausgeliefert werden soll.

**Damit ist R8 gelöst:** Verhalten einmal in C#, Zahlen einmal in `AI.Data/`.

### 4.7 Zweistufigkeit

**Stufe 1** — alles aus §4.2–4.4, soweit ohne Timer erreichbar. Kein
`IStatefulSimSystem`, keine D-ID, keine Golden-Bytes-Berührung.

**Stufe 2 — `AiSidecar`.** Erst was Stufe 1 nachweislich nicht kann,
rechtfertigt die Anfrage. `AIArchitecture.md` §4 **spezifiziert den Sidecar
bereits** (Schema-/Profil-ID, eigener PRNG-State, Plan-/Task-/Squad-IDs, Timer
in Ticks, offene Intents), und `MatchFingerprint` führt schon
`SidecarSchemaVersion` (`V1 = 1`). Der Platz ist reserviert, nur unbelegt — die
Anfrage wäre das Einlösen eines vorgesehenen Vertrags, keine
Architekturänderung.

---

## 5. Verhaltenslücke A–Z

Der Command-Vertrag kennt 13 Befehlsarten im Simulationsstrom. **Die KI benutzt
fünf:** `Move`, `AttackTarget`, `Harvest`, `PlaceBuilding`, `QueueUnit`.

| Fehlt | Wofür |
|---|---|
| `InstallDefenseModule` (13) | **MG-/Raketen-Verteidigungsmodule** — von `AIArchitecture.md` §2 gefordert |
| `Repair` (8) | Builder repariert Gebäude, 10 HP/Tick |
| `SetRallyPoint` (12) | Nachschub sammelt sich, statt einzeln zu sterben |
| `ReturnCargo` (5) | Harvester bei Gefahr mit Teilladung heimschicken |
| `Stop` (2) | verlorenes Gefecht abbrechen, Rückzug sauber beenden |
| `CancelConstruction` (7) | Baustelle aufgeben, wenn die Lage kippt (75 % zurück) |
| `Sell` (9) | Notliquidität (50 % zurück) |
| `CancelProduction` (11) | falsche Einheiten aus der Queue nehmen |

Verteidigung ohne Verteidigungsmodule ist nur die halbe Antwort.

**Korrektur einer früheren Annahme:** `SetRallyPoint` akzeptiert das Refinery
heute. `ValidateSetRallyPoint` prüft über `IsProducerRole`, und die liest aus
der Definitionstabelle statt aus einer harten Liste — laut eigenem Kommentar
genau deshalb, damit der D-077-Umzug sie nicht strandet
(`ProductionSystem.cs:477`); der Harvester trägt in beiden Fraktionen
`producerRole: UnitRole.Refinery`. Die gegenteilige Behauptung lebt nur noch
als veralteter Doku-Kommentar in `SkirmishAiSystem.cs:63` (Mini-Doku-Fix,
§4.5). Folge: `SetRallyPoint` ist **sofort nutzbar**, und das
Harvester-Micromanagement der heutigen KI hat seinen dokumentierten Grund
verloren — das hebt den Befehl in der E8-Priorität.

Randnotiz zu den Zahlen der Tabelle: Repair 10 HP/Tick, Sell 50 % und
CancelConstruction 75 % stimmen mit dem Code überein, sind dort aber als
provisorisch markiert („Q-040 candidate") — bei einer Inhaberentscheidung
können sie sich ändern.

---

## 6. KI gegen KI und 2 gegen 2

**KI gegen KI im Labor: ja.** `SkirmishAiTests` fährt heute KI gegen einen
passiven Slot. Zwei KI-Instanzen zu verdrahten ist eine überschaubare
Erweiterung. Im *Spiel* ist es gesperrt (`MatchConfig`: `AiSlots.Length > 1`
wirft; `mvp-v1.json` schreibt `solo-human-vs-ai` bindend fest) — das betrifft
das Labor nicht, weil der Harness den Host direkt baut wie `SimRunner` und nicht
durch `MatchConfig` geht. Es entsteht kein Spielmodus, nur ein Testaufbau.

**2 gegen 2: strukturell offen, inhaltlich blockiert.** Da sind 8 Slots, 8
Team-Masken, 8 Victory-Slots. Es fehlt ein Team-Begriff: Feind ist heute
schlicht jede fremde `PlayerId` — der Kandidaten-Scan überspringt nur die
eigene (`CombatSystem.cs:192`) — und FoW setzt `team == PlayerSlot`
(MS-1-Vereinfachung, `FogOfWarSystem.cs:28`, im Rahmen von D-058). Ein
Verbündeter wäre ein Ziel, zwei Verbündete teilten keine Sicht.

| Nötig | Datei | Eigentümer |
|---|---|---|
| Freund/Feind | `Simulation/Combat/` | **uns** ✅ |
| geteilte Sicht | `Simulation/Vision/` | Netzstrang ❌ |
| Niederlage je Seite | `Simulation/Victory/` | Netzstrang ❌ |
| Slot-/Modusvertrag | `MatchConfig`, `mvp-v1.json` | Netzstrang / Governance ❌ |

Seit v1.1.0 hat jeder dieser Pfade einen Eigentümer — der Vorschlag aus E11 hat
damit einen Adressaten statt eines offenen Endes.

Im Labor ist eine 4-Slot-Partie *ohne* Bündnisse sofort machbar und liefert
schon viel. Echte Teams sind ein Vorschlag, keine einseitige Umsetzung. Der
Harness wird ab E1 N-Slot-fähig gebaut.

---

## 7. Scope-Landkarte (nur für PR-Inhalte)

Stand nach `13-15_Parallelbetrieb.md` **v1.1.0** (Commit `c107c1f`, 2026-08-08).
Die Schreibhoheitstabelle ist seitdem **vollständig** — ein unzugeordneter Pfad
ist ein Fehler im Dokument, kein Freiraum.

| Vorhaben | Pfad | Status |
|---|---|---|
| Goal-System, Angriffserkennung, Rückzug, Score-Targeting | `Scripts/AI/` | ✅ **uns** |
| Profile und Gewichte als Daten | `Scripts/AI.Data/` | ✅ **uns** (leer, Datenschicht entsteht hier) |
| Freund/Feind, Waffen, Rüstung | `Simulation/Combat/` | ✅ **uns** |
| Bewegung am Ziel | `Simulation/Movement/` | ✅ **uns** |
| Flow-Field, Wegfindung | `Simulation/Pathfinding/` | ✅ **uns (13–15)** — neu seit v1.1.0 |
| Legion-Waffenidentität | `Simulation/Factions/` | ✅ **uns** |
| Tests zu neuen Entscheidungen | `tools/Nova.SimRunner.Tests/` | ✅ **uns** (außer den 4 Baselines) |
| `WeaponDefinition`/`UnitDefinition` | `Simulation/Definitions/` | ⚠️ geteilt — Absprache nötig |
| Fog of War, Team-Sicht | `Simulation/Vision/` | ❌ **Netzstrang** |
| Victory, Commanders | `Simulation/{Victory,Commanders}/` | ❌ **Netzstrang** |
| Economy, Construction, Production | `Simulation/{Economy,Construction,Production}/` | ❌ Netzstrang ab Sprint 16, in 13–15 niemand |
| Systemregistrierung, Modus, Slots | `Gameplay/Match/` | ❌ **Netzstrang** |
| Kernel, `ISimSystem`, CommandsV1, Snapshots, Replays, State | `Simulation/…` | ❌ **niemand ohne D-ID** |

**Zwei Vertragsflächen** — Verhalten frei, Vertrag nur nach Absprache:

| Fläche | Wir sind | Regel |
|---|---|---|
| `Pathfinding.CostField` | Eigentümer | `ConstructionSystem` konsumiert ab Sprint 16 die Platzierungsprüfung. Flow-Field-Erzeugung und Interna sind frei; Signatur und Begehbarkeits-Semantik von `IsWalkable` nur nach Absprache |
| `FogOfWarSystem.GetTeamView` | Konsument | `CombatSystem` braucht sie für die Zielerlaubnis. Wird **benutzt, nicht geändert** |

**Wo neues Verhalten hingehört.** Die Tick-Reihenfolge ist die
Registrierungsreihenfolge in `MatchRunner.cs` — und die gehört dem Netzstrang.
Auflösung laut v1.1.0: Reaktionsverhalten bevorzugt **in `SkirmishAiSystem`**, das
zwischen `Combat` und `Victory` bereits registriert ist und den Reaktionsraum
abdeckt. Damit wird `MatchRunner` gar nicht angefasst.

Das bestätigt den Ansatz aus §4: Das Goal-System wird **kein eigenes System**,
sondern wächst in `SkirmishAiSystem`. Bräuchte es doch eines, käme es **ohne**
Registrierung in den PR, mit gewünschter Position und Begründung im Text; ein
Maintainer setzt die Zeile in einem Mini-PR nach.

---

## 8. Determinismus-Leitplanken

Unter `Scripts/Simulation/` und `Scripts/AI*`, ohne Ausnahme: kein
`float`/`double` (`NoFloatInSimulationTests` prüft), kein `System.Random`, keine
Wanduhr — Zeit ist in Ticks, keine Abhängigkeit von
`Dictionary`/`HashSet`-Iterationsreihenfolge (aufsteigende Index-Scans),
ganzzahliges Scoring inklusive der Gewichte, deterministisches Kriterium bei
Gleichstand.

**Folge für jeden Verhaltens-PR:** Die vier Baseline-Dateien werden rot. Das ist
ihr Zweck. Verhaltens-PR **ohne** Baseline-Änderung (er ist dann rot, korrekt
so), neue Baseline in einem **eigenen PR** mit altem Wert, neuem Wert und
Begründung. Ausnahmslos — das ist der eine Fehler, gegen den die ganze Regel
gebaut ist.

Das Labor hilft dabei: Die Hash-Kette zeigt, **ab welchem Tick** zwei Stände
auseinanderlaufen, statt nur *dass* eine Baseline rot ist.

---

## 9. Etappen

**E1–E5 bauen das Labor fertig, bevor die erste Verhaltenszeile geschrieben
wird.** Das kostet Vorlauf, hat aber den Zweck, dass jede spätere Änderung von
Anfang an vergleichbar und archiviert ist — nachträglich lässt sich ein
Vergleich gegen einen Stand, der nie vermessen wurde, nicht herstellen.

### E0 — .NET-SDK ✅ **erledigt (2026-08-08)**

SDK 8.0.318 unter `Project_Nova/.dotnet/` (`global.json` pinnt hart mit
`rollForward: disable`). Nachweis: **549 Tests, 0 Fehler, 10 s.** Die komplette
Suite inklusive Determinismus-Baselines und End-to-End-KI-Partie braucht zehn
Sekunden — die Durchsatzerwartung aus E2 ist damit eher konservativ.

### E1 — Harness, KI gegen KI ✅ **erledigt (2026-08-08)**

`tools/Nova.AiLab/` anlegen, `MultiSlotAiHost` aus dem `AiHost`-Muster.
N-Slot-fähig gebaut, **erst mit 2 Slots belegt** — 4 Slots sind danach eine
Konfigurationszeile. Reihenfolge-Test gegen `MatchRunner`.

**Startaufstellung exakt kanonisch** (`MatchBootstrap`): je Slot ein
Aetherium-Feld, fertiges HQ, ein Builder, 3.000 AE — und dieselbe Spawn-
Reihenfolge, Slot 0 vor Slot 1, weil sie Entity-Ids und Snapshots bestimmt.
Kartenvarianz kommt erst nach E7: Die KI setzt heute voraus, dass das
entfernteste Feld die Feindbasis markiert (`GetEnemyStartAreaCell`) — bei freier
Aufstellung bricht diese Annahme, und man tunt gegen einen Fehler statt gegen
das Verhalten.

*Fertig, wenn:* Eine KI-gegen-KI-Partie liefert Outcome und Endzustands-Hash,
und zwei Läufe mit gleichem Seed liefern identische Hashes.

**Nachweis.** `dotnet run --project tools/Nova.AiLab -- match --repeat 2 --hash-every 100`:
Seed `0xA17E57DE57`, zwei KI-Slots, `VictoryElimination` für Slot 0 (Allianz)
bei Tick 12.975, Endzustand `0x4947D4769384585C`, beide Läufe über alle 130
Kettenglieder identisch. `tools/Nova.AiLab.Tests/` ist grün (12 Tests, 4 s),
die bestehende Suite unverändert bei 549/0.

Wie „gleiche Verdrahtung" belegt wird, ohne eine Konstante abzuschreiben: Die
Testsuite baut den `AiHost` aus `SkirmishAiTests.cs` von Hand nach — dieselbe
Technik, mit der `CanonicalMatchSetupTests` seine zwei Lanes verbindet — und
vergleicht **Zustands-Hashes**, nicht Zahlen: Registrierungsliste, Tick-0-Hash,
alle 100 Ticks über 2.000 Ticks, und Entscheidungstick plus Endzustand. Das
Labor entscheidet die Referenzpartie bei Tick **2241** mit Endzustand
`0x07CA2429C5FE7E5A` — Wert für Wert wie die bestehende Lane.

Drei Beobachtungen, die der Lauf nebenbei liefert:

- **Durchsatz** (E2-Vorgriff, noch nicht parallel): 2 Slots über 12.975 Ticks
  ≈ 0,6 s, 4 Slots über 27.000 Ticks ≈ 1,4 s je Kern. Die Erwartung aus E0
  war konservativ.
- **Die 4-Slot-Partie endet im Zeitlimit-Unentschieden.** Genau die Vorhersage
  aus Entscheidung 13: Die KI setzt voraus, dass das entfernteste Feld die
  Feindbasis markiert (`GetEnemyStartAreaCell`), und bei vier Basen stimmt das
  für niemanden. Der Lauf ist reproduzierbar, sein *Ausgang* ist damit kein
  Befund — die N-Slot-Fähigkeit ist belegt, mehr behauptet er nicht.
- **Sitzplätze statt Slots.** Der Host trägt die vertraglichen acht Slots, die
  kanonische Karte hat aber vier Eckplätze. `CanonicalOpening` wirft ab Slot 4
  laut, statt Positionen zu erfinden, über die nie jemand entschieden hat.

Zwei Dinge, die E1 bewusst *nicht* getan hat: Der Metrikkatalog aus §3.3 bleibt
E2 — insbesondere `intentsRejected`, das eine Zähl-Hülle um den Transport
bräuchte und damit die byte-exakte Spiegelung aufweichen würde, die E1 gerade
erst belegt hat. Und `AI.Data/` ist im csproj verdrahtet, aber leer: die Werte
stehen weiter dort, wo das Spiel sie hat (`SlotSpec.CanonicalProfile` spiegelt
`MatchRunner`), bis E6 sie zu Daten macht.

### E2 — Lauftreiber, Metriken, Parallelität ✅ **erledigt (2026-08-08)**

`MatchSpec` einlesen, `Parallel.For`, Artefakte je Lauf, Metrikkatalog aus §3.3.

Bekannte Durchsatzgrenze, vorab nicht umbauen: `Decide()` allokiert je
Entscheidungstick rund elf Listen — im Spiel belanglos, bei tausenden
parallelen Partien GC-Druck. Erst messen; ein Umbau wäre ein Verhaltens-PR-
Kandidat mit eigener Begründung, kein Labor-Nebeneffekt.

*Fertig, wenn:* Ein Kommando fährt *n* Matches parallel. **Durchsatz gemessen
und notiert.**

**Durchsatz.** `sweep --seeds 24` auf 24 Kernen: 24 volle Partien
(311.400 Ticks) in **2,2 s = 143.000 Ticks/s** über alle Kerne, seriell rund
13.000 Ticks/s je Kern. Die GC-Sorge aus dem Absatz oben hat sich nicht
bestätigt und bleibt unangetastet — es gibt keinen Grund, `Decide()` anzufassen.

**Messen kostet nichts, und das ist geprüft.** Zwei Tests nageln fest, dass
Trace-Collector und Intent-Zählung dieselbe Hash-Kette liefern wie ein Lauf
ohne sie — dieselbe Bedingung, die §3.4 ans Sichtfenster stellt, „als Test,
nicht als Vorsatz". Ein Beobachter, der die Partie verändert, würde alles
entwerten, was durch ihn gemessen wurde.

**`intentsRejected` braucht eine eigene Verdrahtung.** Die naheliegende
Ableitung — vergebene Sequenzen minus `SealedWatermark` — ist *falsch*, und
zwar lautlos: Der Watermark ist ein Hochwasserstand, keine Zählung. Eine
abgelehnte Sequenz mitten im Strom hinterlässt eine Lücke, über die spätere
Records hinwegversiegeln; die Ablehnung verschwindet aus der Rechnung. Gezählt
wird deshalb dort, wo das Verdikt entsteht: `CountingAiPeerTransport` benutzt
den `ICommandTransport`-Vertrag, ersetzt `AiPeerCommandTransport` nur in
Metrikläufen und ändert `AI/` nicht. **Gemessenes Ergebnis: die heutige KI wird
nie abgelehnt (0 von 541 Intents).** Die Zahl wird erst mit E7/E8 interessant.

Drei Abweichungen vom Katalog in §3.3, jede weil die genannte Größe im
committed State nicht existiert — benannt statt erfunden:

| §3.3 nennt | Labor liefert | Grund |
|---|---|---|
| `damageDealt`/`damageTaken`/`kills` | `unitsLost`, `healthLost` je Slot | Es gibt kein Schadensbuch im State. Bei zwei Slots rechnet der Bericht „dealt" aus dem „taken" der Gegenseite; bei mehr Slots ist Schaden gar nicht zuordenbar |
| `queueStallTicks` | `lowPowerTicks` | Eine Stockung hat keine Markierung im State, die dokumentierte Produktionsbremse schon (`ProductionSpeedMultiplierQ16` halbiert) |
| `activeGoal`, `goalUtility`, `goalSwitches` | — | Das Goal-System entsteht erst in E7. Nullen, die wie Messwerte aussehen, wären schlechter als eine Lücke |

#### Der Befund, der die Sweep-Methodik ändert

> **Kein einziges Simulationssystem zieht aus dem Kernel-PRNG.** `SimRandom`
> wird im Kernel gehalten, in Zustands-Hash und Snapshot geschrieben — und nie
> gezogen. Belegt per Codesuche über `Scripts/Simulation/` und per Messung:
> drei völlig verschiedene Seeds (`0x1`, `0xDEADBEEF`, `0xA17E57DE57`) liefern
> denselben Trace, dieselbe Armeegröße, dieselben Credits. 24 Seeds im Sweep
> entscheiden alle bei Tick 12.975 für Slot 0.

Der Seed verändert den **Zustands-Hash**, nicht die **Partie**. Das ist kein
Defekt — eine zufallsfreie Simulation ist für Lockstep eine saubere
Entscheidung — aber es hebelt eine Annahme aus, die an mehreren Stellen im Plan
steckt:

- **§3.7 „Referenz-Seedmenge"** ist heute kein Messinstrument. Eine Seedmenge
  fixiert nichts, weil es nichts zu fixieren gibt.
- **E4 „Matrix aus Seeds × Profilen"** hat heute nur eine echte Achse. Ein
  Sweep über *n* Seeds ist *eine* Beobachtung, keine *n*.
- **Entscheidung 13** (Kartenvarianz erst nach E7) verschärft das: Ohne Zufall
  *und* ohne Kartenvarianz gibt es bis E6 überhaupt keine Varianzquelle außer
  dem Profil.

Das Labor versteckt das nicht: `sweep` zählt verschiedene Entscheidungen und
schreibt „the seed axis is empty" hin, wenn alle Läufe gleich ausgehen. Ein
Test hält den Befund fest — und dokumentiert, dass sein Fehlschlagen eine gute
Nachricht wäre, weil dann etwas zieht und die Achse echt wird.

**Was daraus folgt, ohne den Plan einseitig umzuschreiben:** Die Reihenfolge
E4-vor-E6 verliert ihren Sinn, solange Profile die einzige Varianzquelle sind
und es nur ein Profil gibt. Der Vergleichsbericht braucht etwas zu vergleichen.
Naheliegend wäre, **E6 (Profile zu Daten) vor E4 zu ziehen** — das ist aber
eine Planänderung und keine Laborentscheidung, deshalb steht sie hier als
Vorschlag und nicht als Tatsache.

### E3 — 2D-Sichtfenster, beide Darstellungen ✅ **erledigt (2026-08-08)**

`ViewRecorder` nach §3.4, Terminalansicht live und HTML-Abspieler zur Nachschau
in einem Zug — beide lesen denselben Frame-Strom, der Mehraufwand gegenüber
einer Darstellung ist gering. Ebenen: Fog of War je Team, verworfene Intents.

Bewusst **vor** dem Sweep: Tausend Läufe auszuwerten hilft wenig, solange man an
einem einzelnen nicht erkennt, was schiefging.

*Fertig, wenn:* Eine laufende Partie ist im Terminal verfolgbar, ein
abgeschlossener Lauf im Browser zurückspulbar — **und ein Test belegt, dass ein
Lauf mit und ohne Sichtfenster dieselbe Hash-Kette liefert.**

**Nachweis.** `match --watch` zeichnet die laufende Partie im Terminal
(ANSI, 64×32 heruntergerechnet, beide Basen und die Armeen auf dem Weg zur
Mitte sichtbar). `match --view-every 25 --fog --out <dir>` schreibt
`view.ndjson` und `player.html` daneben; die Seite lädt die Frames, hat
Scrubber, Einzeltick, Abspielen und drei zuschaltbare Ebenen. 520 Frames einer
vollen Partie sind 2,1 MB — die Schätzung aus §3.4 hat gestimmt. Der
Beobachter-Beweis steht als Test, ebenso dass Terminal und Datei **denselben
Frame-Strom** lesen (Entscheidung 10: kein zweiter Aufzeichnungspfad, der
auseinanderlaufen könnte).

Zwei Abweichungen von §3.4, beide bewusst:

- **Helligkeit auf einer Baustelle ist der Baufortschritt, nicht die
  Gesundheit.** Eine Baustelle steht ihr ganzes Leben auf 1 HP; die Gesundheit
  würde dort nichts kodieren, der Fortschritt beantwortet „kommt das Ding hoch
  oder hängt es?".
- **Verworfene Intents als Aufblinken fehlen.** Die Zahl dafür ist heute 0
  (E2), und der Ort einer Ablehnung stünde nur in der Payload des abgelehnten
  Records. Eine Ebene zu bauen, die garantiert nie etwas zeigt, wäre Attrappe;
  sie kommt, wenn E7/E8 Ablehnungen erzeugen.

Der Fog-Layer ist zuschaltbar statt immer an: Er dominiert die Dateigröße
(RLE über 16.384 Zellen je Slot und Frame). Angeschaltet beantwortet er die
Frage, die §3.4 zu Recht hervorhebt — *konnte die KI es überhaupt sehen?*

#### Ein Fehler, den E3 in E2 gefunden hat

> **Eine Baustelle trägt `UnitRole.Unit`, nicht ihre Gebäuderolle.** Erst bei
> Fertigstellung wechselt sie auf `def.Role`
> (`ConstructionSystem.SpawnBuildingEntity`, `ConstructionSystem.cs:742`).

Der Metrik-Sammler aus E2 fragte `IsBuildingRole` **vor** `TryGetSite` — damit
war der Baustellen-Zweig unerreichbar und `sitesOpen` eine dauerhafte Null, ohne
dass irgendetwas das gemeldet hätte. Genau die Sorte Fehler, die ein Labor
gefährlich macht: eine Kennzahl, die still nichts misst. Gefunden hat ihn nicht
die Metrik, sondern das Bild — der Sichtfenster-Test verlangte eine Baustelle
und bekam keine. Beide Stellen sind korrigiert, ein Regressionstest hält den
Befund fest.

Das ist auch das Argument aus §3.4 in einem Satz: *Zahlen sagen, dass etwas
schiefging, nicht was* — und manchmal sagen sie nicht einmal das.

### E4 — Vergleichsbericht und Gegnerarchiv *(lokal)*

Matrix aus Seeds × Profilen × Fraktionen. Ergebnis ist der Vergleichsbericht aus
§3.6 — **Kennzahlen nebeneinander, keine Rangliste**: eine Zeile je Kandidat,
Abweichung zur Referenz hervorgehoben, Link zum Sichtfenster-Lauf.

Dazu das Archiv aus §3.7: eingefrorene heutige KI als Maßstab, Momentaufnahmen
eigener Fassungen als Verlaufsvergleich. Ergebnismengen tragen Spec-Version,
Seedliste und `ComputeDefinitionsHash64()`; passt eines nicht, verweigert der
Bericht den Vergleich statt still Unvergleichbares zu mischen.

Berichtsform ist HTML nach §3.10, dazu der PR-Textentwurf mit ausschließlich
gemessenen Angaben.

Dazu die Selbstkontrolle: jeder zwanzigste Lauf doppelt, Hash-Ketten
verglichen — 5 % Rechenzeit gegen geteilten Zustand zwischen parallelen Matches.

*Fertig, wenn:* Zwei Kandidaten sind in Minuten gegeneinander beurteilbar —
Bericht lesen, auffälligen Lauf im Sichtfenster nachschauen, entscheiden.

### E5 — Duell-Arena und Bewegungsszenario ✅ **erledigt (2026-08-08)**

Die zwei schmalen Laufarten aus §3.9, gleicher Host, identische
Systemregistrierung. `duel` über alle Rollenpaare beider Fraktionen, mit
AE-Parität, echtem Fog of War, drei Startabständen, beiden Laufrichtungen je
Paarung und einer eigenen Belagerungs-Staffel; `movement` mit den vier Szenarien
`arrival`, `blocking`, `standoff` und `detour` für Issue 03.

Beide laufen in Sekunden — damit werden Waffen- und Bewegungsfragen zur
Sekundenschleife statt zur Partieauswertung. Bewusst am Ende des Laborteils:
Sie erben Lauftreiber, Sichtfenster und Vergleichsbericht, statt sie zu
duplizieren.

*Fertig, wenn:* Die Gegentabelle fällt aus einem Kommando, und ein
Bewegungsszenario zeigt im Sichtfenster, wo eine Gruppe hängenbleibt.

**Nachweis.** `duel` fährt **576 Duelle in 2,2 s** — beide Fraktionen, alle
sechs Kampfrollen gegeneinander, drei Startabstände, beide Laufrichtungen, dazu
die Belagerungsstaffel. `movement` fährt die vier Szenarien je Fraktion in
Sekunden. Beide teilen den Host mit `match`; ein Test nagelt fest, dass die
Arena **dieselbe G1-Registrierung** benutzt — Economy, Construction und
Production ticken über leere Tabellen mit, nur die KI-Systeme fehlen, weil das
Szenario befiehlt. Befehle laufen über eine eigene Session je Slot
(`SlotController.Scripted`), also über den kanonischen Befehlspfad statt über
direkten Zugriff auf Entity-Zustand.

**Vier Befunde aus dem ersten Lauf** — drei davon Fehler im *Messaufbau*, die
erst auffielen, weil die Zahlen unmöglich aussahen:

| Was die erste Fassung zeigte | Was tatsächlich los war |
|---|---|
| 83 Einheiten je Seite | Ein globales AE-Budget ist falsch. Das Budget wird jetzt **je Paarung** so bemessen, dass die *teurere* Seite sechs Einheiten stellt; die billigere stellt, was dasselbe AE kauft — das *ist* die Parität aus Entscheidung 20 |
| 144 von 144 Weitdistanz-Duellen „unentschieden", niemand verletzt | Beide Seiten liefen auf die *Position* der Gegenseite, tauschten die Plätze und standen wieder 34 Zellen auseinander. Jetzt laufen beide auf den **Mittelpunkt** — 144 von 144 entscheiden |
| 0 abgelehnte Befehle, obwohl nichts passierte | `TrySubmitIntent` liefert am Peer-Ingress **immer `Accepted`**; das Host-Verdikt kennt nur der Transport. Gezählt wird jetzt dort |
| Belagerung: 6 bis 12 Gebäude je Zeile | Die Belagerung ist keine AE-Paritätsfrage. Ziel ist jetzt **ein** Gebäude, gemessen werden Ticks bis Abriss und eingesetztes AE — wie §3.9 es beschreibt |

**Und zwei Befunde über das Spiel**, die genau das sind, wofür die Laufarten
gebaut wurden:

> **Explosiv reißt ein Kraftwerk in 27 Ticks ein, Kinetik braucht 236** — Faktor
> **8**, nicht die 2,5, die der Matrixmultiplikator allein vorhersagt (30 % gegen
> 75 % auf `Building`). Nachladezeit und Gebäude-Lebenspunkte machen aus dem
> Faktor 2,5 einen Faktor 8. Genau der Unterschied, den §3.9 zwischen
> abgelesenem Matrixwert und gemessenem Duellausgang meint. Basisinfanterie
> stirbt an der `DefensePlatform`, dem einzigen Gebäude, das zurückschießt.

> **Fernkämpfer halten überhaupt keinen Abstand.** Allianz-Artillerie mit
> **20 Zellen Reichweite** läuft auf **0 Zellen** an den Feind heran, Legion mit
> 18 ebenso — Überlauf 20 bzw. 18, also der volle Reichweitenvorteil verschenkt.
> Das ist die Zahl, die Issue 03 meint, jetzt als Kommando messbar. Ein Test
> hält sie fest und schlägt fehl, sobald der Überlauf schrumpft — dann landet
> die Verhaltensarbeit.

Zwei Nebenbefunde: Ein **`AttackTarget`-Befehl allein bewegt nichts** — Artillerie
40 Zellen vor einem Ziel steht und feuert nie (GB-002 in der Praxis, kein
Attack-Move); die Annäherung muss explizit befohlen werden, wie die KI es tut.
Und **16 Einheiten fädeln ohne messbare Blockade durch eine Ein-Zellen-Engstelle**
(erste Ankunft Tick 161, letzte 176) — ein positiver Befund über die Bewegung,
kein Problem.

Die 44 Duelle „ohne Berührung" auf Waffenreichweite sind ein eigener Ausgang im
Bericht, kein Unentschieden: Eine Waffe, die weiter reicht als ihre Sicht
(Artillerie 20/18 Tiles gegen 10 Tiles Standardsicht), kann ihre Reichweite ohne
Aufklärung nicht nutzen — der Befund, den Entscheidung 21 vorhergesagt hat.

### E6 — Profile zu Daten *(PR, verhaltensneutral)*

`AI.Data/`-Format aus §4.6, `const` wandert hinüber, ausgelieferte Werte
numerisch identisch → Baselines bleiben grün.

### E7 — Reaktive KI, Stufe 1 *(PR, verhaltensändernd)*

`DefendBase`, `DefendField`, `Retreat`, Score-Targeting, `Farm`. Vorher der
Doku-Fix zu GB-002/D-087. Baselines werden rot → getrennte PRs.

*Fertig, wenn:* Definition of Done aus Issue `04` erfüllt — Verteidiger kehren
zurück, beschädigte Einheiten ziehen sich zurück, die Armee schießt aufs
gefährlichste erreichbare Ziel — **plus Spielbericht aus einer echten Partie**,
inklusive eines Falls, in dem die Reaktion falsch war, mit Einschätzung warum
das akzeptabel ist.

Abhängigkeit: Der Spielbericht braucht ein spielbares Build auf unserer
Plattform — der Linux-Build ist laut v1.1.0 Bringschuld des Netzstrangs und
steht aus (§3.5). Bis dahin gilt §3.10 wörtlich: Der Beobachtungsabschnitt
bleibt sichtbar leer, der PR ist damit unfertig und sagt das auch.

### E8 — Fehlende Befehlsarten *(PR, je Verhalten einer)*

Nach Nutzen sortiert aus §5, jeweils klein und einzeln. Das Labor liefert je
Verhalten den Vorher/Nachher-Vergleich.

### E9 — Sidecar-Vorschlag *(kein Code)*

Aus den E4-Auswertungen belegen, wo Zustandslosigkeit schadet; daraus die
D-ID-Anfrage nach §4.7 bauen. Vorschlag mit Belegen, keine Umsetzung.

### E10 — Goal-System mit Zustand *(nur nach D-ID)*

`IStatefulSimSystem` mit eigenem Block, echte Hysterese in Ticks, Squads,
Aufklärungsgedächtnis. Metamorphic-Tests nach `AIArchitecture.md` §6.

### E11 — Mehr Slots, Teams *(Vorschlag, blockiert)*

4-Slot-Freiforall im Labor sofort; echte Teams als ausgearbeiteter Vorschlag.

---

## 10. Getroffene Entscheidungen

| # | Entscheidung | Begründung |
|---|---|---|
| 1 | Kein Sim-Nachbau in einer zweiten Sprache | Zerstört die strukturelle 1:1-Eigenschaft (§1) |
| 2 | Alles in C#, ein Werkzeugkasten | SDK ist ohnehin Voraussetzung; Ausgabe ist NDJSON, spätere Auswerter lesen sie ohne Umbau |
| 3 | Labor bleibt lokal, nicht merge-pflichtig | Werkzeug, kein Beitrag; löst die meisten Scope-Fragen auf (§0) |
| 4 | Eigenes `tools/Nova.AiLab/` | Kann fremde Projekte nicht versehentlich berühren, leicht wieder zu löschen |
| 5 | KI gegen KI im Labor ohne Rückfrage | Harness umgeht `MatchConfig` wie `SimRunner`; kein Spielmodus (§6) |
| 6 | Gegen den Code bauen, nicht gegen veraltete Doku | D-087 ist implementiert (§4.5) |
| 7 | Stateless zuerst, Sidecar erst mit Belegen | Issue `04` verlangt das; vermeidet eine unbegründete D-ID-Anfrage |
| 8 | Datenumstellung verhaltensneutral, getrennt von Verhalten | Grüne Baselines beweisen, dass nichts verschoben wurde (§4.6) |
| 9 | Laborergebnisse sind Diagnose, nie Nachweis | Deckungsgleich mit der `output/`-Praxis (D-061/D-063, D-067 K1) |
| 10 | Beide Sichtdarstellungen in einem Zug | Gemeinsamer Frame-Strom, Mehraufwand gering (§3.4) |
| 11 | **Keine skalare Gütefunktion, kein Auto-Optimierer** | Eine Zahl belohnt das Falsche; für „sieht im Spiel richtig aus" gibt es keine Kennzahl (§3.6) |
| 12 | Referenz: eingefrorene heutige KI + eigene Momentaufnahmen | Fester Maßstab plus Verlaufsvergleich; Rückschritt fällt auf (§3.7) |
| 13 | Kanonische Startaufstellung, Kartenvarianz erst nach E7 | Sonst tunt man gegen die gebrochene `GetEnemyStartAreaCell`-Annahme (E1) |
| 14 | Fremde Befunde sammeln und melden, nicht reparieren | Arbeitsvertrag §6; per Mail oder Issue, nicht per PR (§3.8) |
| 15 | Goal-System wächst in `SkirmishAiSystem`, wird kein eigenes System | `MatchRunner` gehört dem Netzstrang; v1.1.0 nennt genau diesen Weg als bevorzugt (§7) |
| 16 | Labor erst fertig (E1–E5), dann Verhalten | Ein Vergleich gegen einen nie vermessenen Stand lässt sich nachträglich nicht herstellen (§9) |
| 17 | Drei Laufarten: `match`, `duel`, `movement` | Eine Partie ist ein schlechtes Messgerät für eine Waffenzahl; alle drei teilen die Systemregistrierung (§3.9) |
| 18 | `tickBudget` 27.000 wie im Spiel, je Spec überschreibbar | Standard = Spielwert, damit ein Ergebnis ohne Fußnote gilt; Kürzen verzerrt zugunsten schneller Strategien (§3.2) |
| 19 | Determinismus-Stichprobe: jeder 20. Lauf doppelt | 5 % Rechenzeit gegen geteilten Zustand, den ein einzelner Suite-Test nie sähe (§3.7) |
| 20 | Duell-Parität über AE-Kosten, nicht Stückzahl | Gleiche Stückzahl ist kein Befund — ein teurerer Panzer *soll* gewinnen (§3.9) |
| 21 | Echter Fog of War auch im Duell | Ungenutzte Artilleriereichweite ist ein Balance-Befund, kein Messfehler (§3.9) |
| 22 | Drei Startabstände, jede Paarung in beide Richtungen | Ein Abstand entscheidet die halbe Tabelle vor; die Duell-Asymmetrie macht die Richtung zur eigenen Messung (§3.9) |
| 23 | Nach jedem Merge-Fenster Referenz und Archiv neu vermessen | Hält Vergleiche ehrlich; alte Mengen werden mit Commit archiviert, nicht gelöscht (§3.7) |
| 24 | Vier Bewegungsszenarien statt eines | `arrival`, `blocking`, `standoff`, `detour` decken Issue 03 vollständig ab; Aufbau ist Daten, nicht Code (§3.9) |
| 25 | Eigene Belagerungs-Staffel im Duell | Die `Building`-Spalte trägt ein Drittel der Gegenlogik (§3.9) |
| 26 | Vergleichsbericht als HTML, Machart wie der Abspieler | Ohne Rangliste ist Lesbarkeit das Produkt; teilt Technik mit dem Abspieler (§3.10) |
| 27 | PR-Entwurf nur mit Gemessenem, Beobachtungsabschnitt bleibt leer | Ein Werkzeug, das „nichts als fertig melden, was nicht gelaufen ist" bequem umgehen lässt, wäre schlechter als keins (§3.10) |

## 11. Was Inhaberentscheidung bleibt

Genau zwei — beide als vorbereiteter Vorschlag statt als Vorbedingung, beide
blockieren zwischenzeitlich nichts:

1. **`AiSidecar` / KI-Snapshot-Block** — braucht eine D-ID. Vorschlag in E9 mit
   Messmaterial aus dem Labor.
2. **Teams und mehr als zwei Slots im Spiel** — berührt Vision, Victory,
   MatchConfig und `mvp-v1.json`. Vorschlag in E11; das Labor kommt bis dahin
   mit Freiforall aus.
