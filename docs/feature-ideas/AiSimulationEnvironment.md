# KI-Simulationslabor — Plan

**Status:** Vorschlag, nicht gesetzt · **Strang:** Einheitenstrang (extern) · **Datum:** 2026-08-08
**Bezug:** [AIArchitecture](../tech/AIArchitecture.md), [SkirmishAi_Spec](../tech/modules/SkirmishAi_Spec.md),
[SimulationCore](../tech/SimulationCore.md), Issue `04-ki-die-reagiert`, `13-15_Parallelbetrieb.md`

---

## 0. Betriebsmodell — was hier eigentlich gebaut wird

**Dieses Labor ist kein Beitrag ans Repository. Es ist Werkzeug.**

Es dient dazu, KI- und NPC-Verhalten schnell zu tunen, zu trainieren und zu
testen, indem tausende Partien headless durchlaufen, statt jede Idee einzeln in
Unity zu klicken. Es **muss nicht gemergt werden** und darf dauerhaft lokal in
der Hand des Beitragenden bleiben. Das ist ausdrücklich kein Notbehelf, sondern
die richtige Einordnung: die Verhaltensänderung selbst landet ohnehin nur über
Unity im Spiel — das Labor hilft nur, die *richtige* Verhaltensänderung
schneller zu finden.

Daraus folgt eine saubere Zweiteilung, die den ganzen Plan trägt:

| | **Labor (lokal, nie im PR)** | **Beitrag (PR-fähig)** |
|---|---|---|
| Inhalt | Harness, Lauftreiber, Metriken, Traces, Auswertungen, dieses Dokument | Verhaltensänderungen in `AI/`, `AI.Data/`, `Combat/`, `Movement/`, `Factions/` + zugehörige Tests |
| Ort | Branch `lab/ai-simulation`, im eigenen Fork gesichert, **nie als PR nach `upstream`** | Topic-Branches `feat/…`, PR nach `upstream/main` |
| Beweiskraft | **Diagnose** | Nachweis erst mit gespielter Runde |
| Scope-Fragen | keine — lokales Werkzeug berührt niemandes Schreibhoheit | volle Scope-Regeln des Arbeitsvertrags |

Damit lösen sich fast alle Scope-Konflikte von selbst auf: Ein lokales Werkzeug
braucht keine Zuteilung für `tools/`, keine Freigabe für `docs/`, keine
Absprache über Slotzahlen. Nur das, was tatsächlich in einen PR geht, folgt der
Schreibhoheitstabelle.

**Zwei Regeln, die daraus zwingend folgen:**

1. **Labor-Code darf nie in einen PR-Branch geraten.** Der Harness lebt auf
   `lab/ai-simulation`; PR-Branches werden von `upstream/main` abgezweigt und
   nehmen ausschließlich die Verhaltensdateien mit. Kein Cherry-Pick von
   Labor-Commits.
2. **Tests, die eine Verhaltensänderung belegen, dürfen nicht vom Labor
   abhängen.** Sie folgen dem vorhandenen Muster in `SkirmishAiTests.cs` —
   in sich geschlossen, eigener `AiHost` in der Testdatei. Sonst ist der PR
   ohne das lokale Werkzeug nicht baubar.

**Und die wichtigste Einschränkung, damit sie nicht untergeht:** Ein grüner
Laborlauf ist Diagnose, kein Nachweis. Das Repo behandelt seine eigenen
`output/`-Artefakte schon genauso („DIAGNOSIS, not gate evidence", D-061/D-064).
Was im laufenden Spiel nicht gesehen wurde, steht genau so im PR-Text.

---

## 1. Der Befund, der die Richtung bestimmt

Ziel war ursprünglich: *„die gesamte KI 1:1 in Python nachbauen, um parallel
und ohne Unity simulieren zu können"*. Der erste Teil davon existiert bereits —
und zwar in einer Form, die ein Nachbau nur verschlechtern könnte.

| Beleg | Fundstelle |
|---|---|
| Kernel engine-frei | `Simulation/SimulationKernel.cs:13` — „Engine-decoupled (no UnityEngine dependency)" |
| KI engine-frei | `AI/SkirmishAiSystem.cs:88` — „Zero engine dependencies (no UnityEngine types)" |
| Headless-Läufer, net8.0 | `tools/Nova.SimRunner/Program.cs` — vollständiger Host in ~40 Zeilen |
| Vollständige KI-Partie headless | `tools/Nova.SimRunner.Tests/SkirmishAiTests.cs` — `AiHost`, Entscheidung bei Tick 2242 |
| 10.000-Tick-Determinismuslauf | `tools/Nova.SimRunner/Determinism10000Scenario.cs` |

Entscheidend ist das *Wie*:

```xml
<!-- Nova.SimRunner.Tests.csproj -->
<Compile Include="..\..\Assets\_Project\Scripts\Core\**\*.cs" ... />
<Compile Include="..\..\Assets\_Project\Scripts\Simulation\**\*.cs" ... />
<Compile Include="..\..\Assets\_Project\Scripts\AI\**\*.cs" ... />
```

Die Headless-Lane kompiliert **dieselben Quelldateien**, die Unity lädt — der
*shared-sources contract* (G0-B, `SimulationCore.md` §9). „1:1 wie im echten
Spiel" ist damit eine strukturelle Eigenschaft, keine Disziplinleistung: es kann
nicht auseinanderlaufen, weil es nur eine Quelle gibt.

Ein Nachbau in einer zweiten Sprache würde genau diese Eigenschaft gegen eine
dauerhafte Synchronisationspflicht eintauschen. Bit-genau nachzubauen wären
unter anderem: Q16.16 mit *round-half-even* bei Multiplikation, Division und
Shift plus `OverflowException` statt Sättigung (`Core/SimFixed.cs`);
xorshift128+ mit (23,17,26) und SplitMix64-Seeding (`Core/SimRandom.cs`);
xxHash64 über kanonisch geordnete Snapshot-Blöcke; byte-genaue Serialisierung
von Commands, Snapshots und Replays; und Feinheiten wie die dokumentierte
Duell-Asymmetrie im Combat, bei der ein gegenseitiger Kill innerhalb eines Ticks
vom **niedrigeren Entity-Index** gewonnen wird.

Umfang: ~15.000 Zeilen, jede determinismusrelevant, jede spätere C#-Änderung
doppelt.

→ **Kein Nachbau. Eine Umgebung um das Vorhandene.**

---

## 2. Sprachentscheidung: C#, kein Python

Ursprünglich war Python als Steuer- und Auswertungsschicht vorgesehen. Nach
Prüfung fällt es aus dem Kernplan — mit klarem Wiedereinstiegspunkt.

**Warum nicht:**

- **Zum Laufenlassen bringt es nichts.** Ein C#-Prozess fährt N Matches über
  `Parallel.For` auf allen Kernen. Jedes Match baut Kernel, `EntityManager` und
  alle Systeme frisch; nichts wird geteilt. `WeaponProfiles` und `DamageMatrix`
  sind einmalig gebaute, unveränderliche `static readonly`-Tabellen — read-only
  und damit thread-sicher. Ein Python-Orchestrator davor bedeutet
  Prozessstarts und JSON-Serialisierung als reine Zusatzkosten.
- **Zum Auswerten lohnt es sich erst bei Volumen.** Rastersuche, Aggregation
  und Bewertung sind verschachtelte Schleifen über Integer-Metriken — in C#
  genauso kurz. Und weil die Ausgabe schlicht NDJSON/CSV ist, kostet ein
  späterer Zusatz nichts außer den paar Zeilen, wenn er tatsächlich gebraucht
  wird.
- **Eine Toolchain statt zwei.** Das .NET-SDK ist ohnehin Voraussetzung. Eine
  zweite Sprache — auch rein lokal — ist Pflegeaufwand ohne Gegenwert.

**Wiedereinstiegspunkt, konkret benannt:** Sobald Läufe *quer* verglichen werden
(Plots über hunderte Läufe, Notebook-Exploration) oder echte Optimierer statt
Rastersuche gewünscht sind — CMA-ES, Bandits, irgendwann RL. Dann liest ein
Auswertungsskript die vorhandenen NDJSON-Dateien. Die Architektur muss dafür
nicht angefasst werden; genau deshalb ist die Ausgabe von Anfang an eine Datei
und kein In-Memory-Objekt.

---

## 3. Anforderungen und Stand

| # | Anforderung | Stand |
|---|---|---|
| **R1** | Simulation ohne Unity | ✅ vorhanden |
| **R2** | Parallel, vielfach, schnell | ⚠️ möglich, keine Ansteuerung |
| **R3** | Verhalten bit-identisch zum Spiel | ✅ strukturell garantiert |
| **R4** | KI gegen KI | ⚠️ im Labor baubar, im Spiel gesperrt |
| **R5** | 2 gegen 2 | ❌ kein Team-Begriff |
| **R6** | Verhalten A–Z: Aufbau, Ernten, Wegbringen, Bauen, Rekrutieren, Angriff, Verteidigung | ⚠️ 5 von 13 Befehlsarten genutzt (§7) |
| **R7** | Goal-System, das dauerhaft reagiert | ❌ KI ist bewusst zustandslos |
| **R8** | Eine Stelle zum Ändern | ❌ Werte stecken im Code |

R6, R7 und R8 sind die Arbeit. R1–R3 sind fast geschenkt. R4/R5 siehe §8.

---

## 4. Aufbau des Labors

```
┌─────────────────────────────────────────────────────────────┐
│ LabRunner (Konsole, net8.0)                                 │
│   liest MatchSpec-Liste │ Parallel.For über Kerne           │
│   schreibt je Lauf: result.json, trace.ndjson, hashchain    │
├─────────────────────────────────────────────────────────────┤
│ MatchLab (Bibliothek)                                       │
│   MultiSlotAiHost   2..8 Slots, je KI-Slot eigene Session   │
│   MatchSpec/Result  Ein- und Ausgabevertrag                 │
│   TraceCollector    Integer-Metriken je n Ticks             │
│   SweepPlanner      Matrix aus Seeds × Profilen aufspannen  │
├─────────────────────────────────────────────────────────────┤
│ dieselben .cs-Quellen wie Unity   ← die 1:1-Eigenschaft     │
│   Core/ · Simulation/ · AI/                                 │
└─────────────────────────────────────────────────────────────┘
```

### 4.1 `MultiSlotAiHost` — Verallgemeinerung des vorhandenen `AiHost`

Grundlage ist der `AiHost` aus `SkirmishAiTests.cs`, laut eigener Doku ein
*„byte-exact wiring mirror of MatchRunner.InitializeMatch"*. Genau die
Verdrahtung des echten Spiels, nur ohne Unity darum herum.

Zu verallgemeinern:

```csharp
sealed class MultiSlotAiHost
{
    // je Slot: Fraktion, Steuerung (ai | passiv), Profil
    // je KI-Slot: eigene MatchSession + CommandIngress + AiPeerCommandTransport
    //             → in die eine Host-Ingress forwarden (Muster steht bereits)
    // Systemreihenfolge UNVERÄNDERT:
    //   Economy → Construction → Production → Pathfinding → Movement
    //   → FogOfWar → Combat → [alle KI-Slots] → Victory
    void Step();                       // Spiegel von MatchRunner.StepFixedTick
    uint RunUntilDecided(int budget);
}
```

Strukturell offen für mehr Slots: `CommandLimits.ReservedPlayerSlots = 8`,
`FogOfWarSystem.MaxTeams = 8`, `VictorySystem.MaxSlots = 8`.

**Nicht verhandelbar:** Die Registrierungsreihenfolge ist Vertrag
(`13-15_Parallelbetrieb.md`, Punkt 3 der unantastbaren Verträge). Das Labor
spiegelt sie, es erfindet sie nicht. Ein Test nagelt sie gegen `MatchRunner`
fest — bricht jemand die Reihenfolge, wird dieser Test rot, nicht erst eine
Netzpartie.

**Isolation für echte Parallelität:** Jedes Match baut Kernel, `EntityManager`,
`PathfindingSystem`, alle Systeme und beide Ingress-Ketten neu. Geteilt wird nur
Unveränderliches (`SimDefinitions`, `WeaponProfiles`, `DamageMatrix`). Damit
sind N Matches auf N Kernen echt parallel, ohne Sperren.

### 4.2 Ein- und Ausgabe

**`MatchSpec`** — was einen Lauf vollständig bestimmt:

```json
{
  "specVersion": 1,
  "seed": "0xA17E57DE57",
  "mapWidth": 128, "mapHeight": 128, "entityCapacity": 1024,
  "tickBudget": 27000,
  "slots": [
    { "slot": 0, "faction": "legion",   "controller": "ai", "profile": "legion-aggressive" },
    { "slot": 1, "faction": "alliance", "controller": "ai", "profile": "alliance-turtle" }
  ],
  "traceIntervalTicks": 10,
  "hashIntervalTicks": 100
}
```

**Ausgabe je Lauf:**

| Datei | Inhalt |
|---|---|
| `result.json` | Outcome, Siegerslot, Entscheidungstick, Endzustands-Hash, `SimDefinitions.ComputeDefinitionsHash64()`, Fingerprint |
| `trace.ndjson` | eine Zeile je Metriktick, ausschließlich Ganzzahlen |
| `hashchain.json` | `kernel.CalculateStateHash()` alle *n* Ticks |
| `match.replay` | `ReplayRecorder.Finalize(...)` — im echten Spiel abspielbar |

**Harte Regel:** Kein Float verlässt die Simulation. Positionen als
Q16.16-Rohwerte, alles andere ganzzahlig. Sonst ist der Vergleich zweier Läufe
Glückssache statt Rechnung.

### 4.3 Metrikkatalog

Je Metriktick und Slot, alles direkt aus dem committed State ableitbar:

| Gruppe | Metriken | Quelle |
|---|---|---|
| Wirtschaft | `credits`, `powerProvided`, `powerRequired`, `isLowPower` | `PlayerEconomyState` |
| Ernte | `harvesters`, `idleHarvesters`, `cargoInTransit`, `fieldReserveAE` | `UnitState.HarvestFieldId/CargoAE`, `TryGetField` |
| Bau | `sitesOpen`, `buildingsByRole[9]` | `TryGetSite`, `HasFinishedBuilding` |
| Produktion | `queuedByRole`, `queueStallTicks` | `TryGetProducer`/`TryGetQueueEntry` |
| Armee | `armySize`, `armyHealthSum`, `armyMaxHealthSum`, `losses` | Entity-Scan |
| Gefecht | `damageDealt`, `damageTaken`, `kills` | Differenz je Trace-Intervall |
| Sicht | `visibleEnemyUnits`, `visibleEnemyBuildings` | `FogOfWarSystem.GetVisibleEntities` |
| KI | `activeGoal`, `goalUtility`, `goalSwitches`, `intentsSubmitted`, `intentsRejected` | Goal-System, `AiPeerCommandTransport.LastResult` |

`intentsRejected` ist die unterschätzte Zahl: Sie zeigt, wo die KI gegen
Executor-Regeln anrennt — heute schweigend, weil `Submit()` den Verdikt
absichtlich nicht auswertet.

### 4.4 Wie „1:1" bewiesen wird

Vier Ebenen, aufsteigend in Beweiskraft:

1. **Gleiche Quellen.** Dieselben `.cs`-Dateien. Divergenz ist nicht
   unwahrscheinlich, sondern unmöglich.
2. **Gleiche Verdrahtung.** Ein Test hält Systemliste und Reihenfolge gegen
   `MatchRunner` fest.
3. **Gleiche Hash-Kette.** Zwei Läufe mit gleichem Spec liefern identische
   Ketten — der Selbsttest, den `Determinism10000Scenario` vormacht.
4. **Replay-Konformanz.** Der aufgezeichnete Command-Strom wird im echten
   Unity-Spiel abgespielt. Gleicher Endzustands-Hash = nachgewiesen, nicht
   behauptet. `MatchFingerprint` verweigert den Start bei jeder Abweichung in
   Schema, Inhalt oder Konfiguration.

Ebene 4 ist zugleich die Brücke zurück ins Spiel: Was das Labor findet, wird als
Replay in Unity gegengeprüft, bevor es als „gesehen" gilt.

---

## 5. Das Goal-System — die eigentliche KI-Arbeit

### 5.1 Warum die heutige KI nicht reagieren *kann*

> *„the system is STATELESS (every decision is a pure function of the tick and
> the committed state — no timers, no memory)"* — `SkirmishAiSystem.cs:40`

Für G1 war das richtig: kein Snapshot-Block nötig, Restore reproduziert
dieselben Intents. Es ist aber genau der Grund, warum die KI beim Angriff
weiter Harvester in die Warteschlange stellt. Ein Ziel, das über mehrere
Entscheidungsticks verfolgt wird, *ist* Gedächtnis.

### 5.2 Der Trick: die Welt ist das Gedächtnis

Bevor ein Snapshot-Block beantragt wird — und das wäre eine Inhaberentscheidung,
`Simulation/Snapshots/` steht auf „niemand ohne D-ID" — lohnt die Prüfung, wie
weit man ohne kommt. Die Antwort ist: erstaunlich weit, weil der committed State
die Vergangenheit implizit trägt.

**Die stehenden Befehle der eigenen Einheiten *sind* der Zustand des
Goal-Systems.** `UnitState.TargetGridPos`, `GoalGridPos`, `AttackTarget`,
`HarvestFieldId`, `IsReturningCargo` speichern, was die KI zuletzt gewollt hat —
und zwar an einer Stelle, die ohnehin serialisiert wird (Entity-Store-Block).
Die heutige KI benutzt das bereits, nur zur Doppel-Befehl-Unterdrückung
(`AlreadyHeadingTo`). Dasselbe Signal trägt: *„meine Armee steht schon auf
Heimatkurs → ich bin bereits im Verteidigungsziel."*

Damit ist Hysterese ohne Sidecar möglich: Zielwechsel bekommt eine höhere
Schwelle, wenn die Einheiten schon das andere Ziel ausführen.

Aus dem committed State ableitbare Lagesignale:

| Signal | Ableitung | API |
|---|---|---|
| werde angegriffen (Basis) | Summe der Waffenschäden sichtbarer Feinde in Radius *r* um HQ | `GetVisibleEntities` + `WeaponProfiles.Get` |
| werde angegriffen (Feld) | dito um das eigene Aetherium-Feld | + `TryGetField` |
| verliere gerade | `Σ(MaxHealth − CurrentHealth)` über eigene Einheiten | Entity-Scan |
| Geld fehlt | `AetheriumCredits` unter Schwelle **und** Bauwunsch offen | `PlayerEconomyState` |
| Ernte steht | Harvester mit `HarvestFieldId == 0 && !IsReturningCargo` | `UnitState` |
| Strom fehlt | `PowerProvided − PowerRequired < Reserve` | `PlayerEconomyState` |
| Einheit muss weg | `CurrentHealth * 100 / MaxHealth < Schwelle` | `UnitState` |
| aktuelles Ziel | stehende Befehle der eigenen Einheiten | `TargetGridPos`, `AttackTarget` |

**Die Grenze davon**, ehrlich benannt — das sind die späteren Argumente für
einen Sidecar: keine Timer (Mindeststandzeit in Ticks), kein
Aufklärungsgedächtnis („dort war vor 300 Ticks eine Basis"), keine
Squad-Identität über das hinaus, was Befehle kodieren, keine
Ableitungen über Zeitreihen (Schadensrate statt Schadenssumme).

### 5.3 Entscheidungspipeline

```
Ein Entity-Scan  →  Lagebewertung  →  Zielauswahl      →  Zielausführung  →  Intents
(aufsteigend)       (Integer)         (Utility+Hyst.)     (je Gruppe)        (Command-Pfad)
```

Der Scan existiert bereits in `Decide()`. Er wird um die Lagekennzahlen
erweitert, nicht dupliziert.

**Zielauswahl** — jedes Ziel bekommt einen ganzzahligen Nutzwert 0..1000,
höchster gewinnt, Gleichstand nach fester Zielreihenfolge:

```
U(DefendBase)  = min(1000, threatAtBase  * W_defBase  / 10)
U(DefendField) = min(1000, threatAtField * W_defField / 10)
U(Farm)        = credits >= C_target ? 0
                 : (C_target - credits) * W_farm / C_target
U(PowerUp)     = powerMargin < reserve ? W_power : 0
U(ArmyUp)      = max(0, (armyTarget - army)) * W_army
U(Push)        = (army >= pushThreshold && threatAtBase == 0)
                 ? W_push + min(200, army * 10) : 0
```

`threatAtBase` ist die Summe von `WeaponProfiles.Get(faction, role).AttackDamage`
über sichtbare Feinde in Reichweite — ganzzahlig, und ein unbewaffneter
Harvester am Zaun erzeugt korrekt 0 Bedrohung.

Hysterese: Ein Wechsel weg vom aktuell aus den Befehlen abgeleiteten Ziel
verlangt Vorsprung `Δ`, nicht nur `>`.

**Zielkatalog** — deckt R6 und R7 ab:

| Ziel | Auslöser | Aktion (alles über `CommandIntent`) |
|---|---|---|
| `Bootstrap` | kein Refinery | Refinery setzen, Builder nachziehen |
| `Expand` | Wirtschaft läuft, Platz frei | Barracks / Power / VehicleFactory |
| `Farm` | Credits unter Schwelle oder Bauwunsch unbezahlbar | Harvester bauen, Untätige aufs Feld, Rückweg freimachen |
| `PowerUp` | Stromreserve unter Profilwert | Power vorziehen |
| `TechUp` | T2 verlangt, ResearchLab fehlt | ResearchLab setzen |
| `ArmyUp` | Armee unter Soll, Geld da | Infanterie/Fahrzeuge queuen |
| `Scout` | keine Feindsichtung | billige Einheit Richtung Feindbasis |
| `DefendBase` | sichtbarer Feind nahe HQ/Produktion | Armee heim, Ziele nach Score |
| `DefendField` | sichtbarer Feind nahe eigenem Feld (13B · B4) | Teilarmee ans Feld, Harvester ausweichen |
| `Push` | Armee über Schwelle, Basis sicher | Vormarsch auf Feindbasis |
| `Retreat` | *pro Einheit*, unter Lebensschwelle | Move-Intent Richtung eigener Basis |

`Retreat` ist bewusst kein globales Ziel, sondern ein Filter über Einheiten —
sonst zöge sich die ganze Armee wegen eines angeschlagenen Spähers zurück.

**Zielpriorisierung als Score** — ersetzt das heutige „HQ vor Gebäude vor
Einheit" aus `FindPreferredVisibleEnemy`. Rein ganzzahlig:

```
score = W_dmg    * DamageMatrix.Resolve(my.AttackDamage, my.DamageType, target.ArmorClass)
      + W_threat * targetProfile.AttackDamage
      + W_finish * (100 - target.CurrentHealth * 100 / target.MaxHealth)
      - W_dist   * chebyshevCells
Gleichstand → niedrigere rohe Entity-Id (deterministisch)
```

`DamageMatrix` liefert **Integer-Prozent** (100 == 1.00) — der Multiplikator
passt ohne Umrechnung ins Scoring. Damit läuft die Armee nicht mehr am Panzer
vorbei aufs Lagerhaus, und eine Kinetik-Waffe (50 % gegen Medium, 30 % gegen
Gebäude) wählt anders als eine Explosivwaffe (100 % / 75 %).

### 5.4 Vorab zu klären: GB-002 gegen D-087

Die Klassendoku von `SkirmishAiSystem` beruft sich auf GB-002 („kein
Attack-Move, kein Auto-Acquire, Befehle sind zwingend"). `CombatSystem.cs:20`
beschreibt dagegen unter **D-087 bereits Auto-Acquisition**: *„every armed
entity without a valid attack order picks the nearest hostile, visible,
in-range target"*.

**Entscheidung fürs Labor: der Code gilt.** D-087 ist implementiert und neuer;
die KI-Klassendoku ist veraltet. Das Goal-System wird gegen das gebaut, was
`CombatSystem` tatsächlich tut — mit einer Konsequenz, die man kennen muss:
*explizite* Angriffsbefehle werden nie überschrieben („explicit orders are never
retargeted"), das Score-Targeting hat also Vorrang vor Auto-Acquire und
übernimmt damit auch die Verantwortung, nicht schlechter zu zielen als die
Automatik.

Die veraltete Doku-Passage ist ein eigener, winziger PR wert (nur ein
Kommentarblock in `AI/` — eigener Scope). Kein Blocker.

### 5.5 `AI.Data/` — die eine Stelle zum Tunen

`Assets/_Project/Scripts/AI.Data/` enthält heute **nur ein asmdef**. Es gehört
uns exklusiv und ist genau dafür gedacht.

Heute stecken die Werte verstreut im Code: `AiFactionProfile`-Defaults
(`TargetPowerMargin = 30`, `TargetArmySize = 15`, `AttackSquadThreshold = 8`,
`TargetHarvesterCount = 2`) und `const`-Felder im System
(`DecisionTickInterval = 20`, `PlacementSearchRadius = 8`,
`InfantryQueueBatch = 2`, `HarvesterQueueBatch = 2`).

Zielformat:

```json
{
  "profileId": "legion-aggressive",
  "schemaVersion": 1,
  "cadence":   { "decisionTickInterval": 20 },
  "economy":   { "creditTarget": 2000, "targetHarvesters": 4, "powerReserve": 20 },
  "army":      { "targetSize": 20, "pushThreshold": 10, "queueBatch": 2 },
  "goalWeights":   { "defBase": 40, "defField": 25, "farm": 30, "power": 500,
                     "army": 20, "push": 300, "switchHysteresis": 80 },
  "targetWeights": { "dmg": 10, "threat": 6, "finish": 3, "dist": 4 },
  "retreat":   { "enterHealthPercent": 25, "exitHealthPercent": 60 }
}
```

Zwei bindende Regeln:

- **Nur ganze Zahlen.** Ein Float in einer Profildatei ist ein Float in der
  Simulation — verboten, `NoFloatInSimulationTests` prüft mit.
- **Ausgelieferte Profile behalten exakt die heutigen Werte.** Solange die
  Standardprofile numerisch unverändert bleiben, ändert die Umstellung von
  `const` auf Daten **kein Verhalten** und die Baselines bleiben grün. Das
  macht die Datenumstellung zu einem eigenen, risikoarmen, gut prüfbaren PR —
  getrennt von jeder Verhaltensänderung.

Abweichende Profile existieren zunächst nur im Labor. Damit stellt sich die
Frage, ob eine Profil-ID fingerprintrelevanter Inhalt ist (analog zur Fraktion
je Slot), erst dann, wenn ein abweichendes Profil tatsächlich ausgeliefert
werden soll — und dann als saubere Einzelfrage statt als Vorbedingung.

**Damit ist R8 gelöst:** Verhalten steht einmal in C#, Zahlen stehen einmal in
`AI.Data/`, das Labor variiert die Zahlen. Keine zweite Stelle.

---

## 6. Zweistufigkeit: erst ohne Zustand, dann fragen

**Stufe 1 — reaktiv ohne Gedächtnis.** Alles aus §5.2/§5.3, soweit ohne Timer
erreichbar: Angriffserkennung, Feldverteidigung, Rückzug, Score-Targeting,
`Farm`. Kein `IStatefulSimSystem`, kein Snapshot-Block, keine D-ID, keine
Golden-Bytes-Berührung.

**Stufe 2 — `AiSidecar`.** Erst was Stufe 1 nachweislich nicht kann,
rechtfertigt die Anfrage. Die gute Nachricht: `AIArchitecture.md` §4
**spezifiziert den Sidecar bereits** (Schema-/Profil-ID, eigener PRNG-State,
Plan-/Task-/Squad-IDs, Timer in Ticks, letzter konsumierter View-Tick, offene
Intents), und `MatchFingerprint` führt schon ein Feld `SidecarSchemaVersion`
(`V1 = 1`). Der Platz ist reserviert, nur unbelegt. Die Anfrage wäre also das
Einlösen eines vorgesehenen Vertrags, keine Architekturänderung.

Was die Anfrage mitbringen muss, damit sie entscheidbar ist: Blockformat,
Blocknummer aus `SnapshotBlockIds`, Umgang mit `SidecarSchemaVersion`,
Restore-Nachweis („Save und Restore erzeugen dieselben späteren Intents") und
die Metamorphic-Tests aus `AIArchitecture.md` §6 (Änderungen an verborgenen
Gegnerdaten dürfen die Intents nicht bewegen). Genau das liefert das Labor als
Messmaterial.

---

## 7. Die Verhaltenslücke A–Z

Der Command-Vertrag kennt 13 Befehlsarten im Simulationsstrom. **Die KI benutzt
fünf.**

| Befehl | KI nutzt | Fehlt für |
|---|---|---|
| `Move` (1) | ✅ | |
| `AttackTarget` (3) | ✅ | |
| `Harvest` (4) | ✅ | |
| `PlaceBuilding` (6) | ✅ | |
| `QueueUnit` (10) | ✅ | |
| `Stop` (2) | ❌ | Abbrechen eines verlorenen Gefechts, Rückzug sauber beenden |
| `ReturnCargo` (5) | ❌ | Harvester bei Gefahr mit Teilladung heimschicken |
| `CancelConstruction` (7) | ❌ | Baustelle aufgeben, wenn Lage kippt (75 % Rückerstattung) |
| `Repair` (8) | ❌ | Builder repariert Gebäude — 10 HP/Tick |
| `Sell` (9) | ❌ | Gebäude verwerten (50 %), Notliquidität |
| `CancelProduction` (11) | ❌ | falsche Einheiten aus der Queue nehmen |
| `SetRallyPoint` (12) | ❌ | Nachschub sammelt sich, statt einzeln zu sterben |
| `InstallDefenseModule` (13) | ❌ | **MG-/Raketen-Verteidigungsmodule** — aus `AIArchitecture.md` §2 gefordert |

Für R6 („Verhalten von A bis Z") sind vor allem `Repair`, `SetRallyPoint`,
`ReturnCargo` und `InstallDefenseModule` relevant — Verteidigung ohne
Verteidigungsmodule ist nur die halbe Antwort. Jedes davon ist ein eigenes,
kleines, für sich prüfbares Verhalten.

**Bekannte Einschränkung, dokumentiert:** `SetRallyPoint` lehnt das Refinery
laut Klassendoku heute ab (die Producer-Liste in `ProductionSystem` ist älter
als der D-077-Umzug des Harvester-Produzenten). Ein sim-seitiger Fehler außerhalb
unseres Scopes; die KI mikromanagt deshalb die Harvester wie ein Mensch. Nicht
umgehen, nur wissen.

---

## 8. KI gegen KI und 2 gegen 2

### KI gegen KI: im Labor ja

`SkirmishAiTests` fährt heute KI auf Slot 1 gegen einen passiven Slot 0. Zwei
KI-Instanzen mit je eigener Session/Ingress zu verdrahten ist eine überschaubare
Erweiterung.

Im Spiel ist es gesperrt, an einer Stelle die uns nicht gehört:

```csharp
// Gameplay/Match/MatchConfig.cs — Netzstrang
if (AiSlots.Length > 1)
    throw new ArgumentException("The MS-1 runner supports at most one AI slot.");
```

Dazu bindend in `quality/content/mvp-v1.json`:
`"mode": { "id": "solo-human-vs-ai", "humanSlotCount": 1, "aiSlotCount": 1 }`.

**Das betrifft das Labor nicht.** Der Harness baut den Host direkt aus Kernel
und Systemen — genau wie `SimRunner` und `Determinism10000Scenario` es tun —
und geht nicht durch `MatchConfig`. Es entsteht kein Spielmodus, nur ein
Testaufbau. Als lokales Werkzeug (§0) ist das unsere Entscheidung.

### 2 gegen 2: strukturell offen, inhaltlich blockiert

**Da:** 8 reservierte Slots (`CommandLimits`), 8 Team-Masken (`FogOfWarSystem`),
8 Slots im `VictorySystem`, und `MatchFingerprint` führt Belegung *und* Fraktion
für alle acht.

**Fehlt:** ein Team-Begriff. Feindschaft ist heute schlicht „anderer Slot":

```csharp
// Combat/CombatSystem.cs:192
if (!candidate.IsActive || candidate.PlayerId == attacker.PlayerId) continue;
```

Und Fog of War setzt `team == PlayerSlot` (D-058). Ein Verbündeter wäre heute
ein Ziel, und zwei Verbündete teilten keine Sicht.

| Nötig für 2v2 | Datei | Eigentümer |
|---|---|---|
| Freund/Feind-Prüfung | `Simulation/Combat/` | **uns** ✅ |
| geteilte Team-Sicht | `Simulation/Vision/` | nicht zugeteilt ❌ |
| Niederlage je Seite statt je Slot | `Simulation/Victory/` | nicht zugeteilt ❌ |
| Slot-/Modusvertrag | `MatchConfig`, `mvp-v1.json` | Netzstrang / Governance ❌ |

**Im Labor** ist eine 4-Slot-Partie *ohne* Bündnisse (jeder gegen jeden) sofort
machbar und liefert schon viel — Wirtschaftsdruck, Mehrfrontenlage,
Zielprioritäten unter Ablenkung. Echte Teams sind ein Vorschlag, keine
einseitige Umsetzung. Der Harness wird ab Etappe 1 N-Slot-fähig gebaut, damit
später nichts umgebaut werden muss.

---

## 9. Scope-Landkarte

Nur für das, was tatsächlich in einen PR gehen soll — Laborcode bleibt lokal
(§0) und taucht hier nicht auf.

| Vorhaben | Pfad | Status |
|---|---|---|
| Goal-System, Angriffserkennung, Rückzug, Score-Targeting | `Scripts/AI/` | ✅ **uns** |
| Profile und Gewichte als Daten | `Scripts/AI.Data/` | ✅ **uns** (heute leer) |
| Freund/Feind für Teams | `Scripts/Simulation/Combat/` | ✅ **uns** |
| Abstandhalten, kein gegenseitiges Blockieren | `Scripts/Simulation/Movement/` | ✅ **uns** |
| Legion-Waffenidentität | `Scripts/Simulation/Factions/` | ✅ **uns** |
| Tests zu neuen Entscheidungen | `tools/Nova.SimRunner.Tests/` | ✅ **uns** (außer den 4 Baselines) |
| Geteilte Team-Sicht | `Simulation/Vision/` | ❌ nicht zugeteilt |
| Team-Niederlage | `Simulation/Victory/` | ❌ nicht zugeteilt |
| Mehr als ein KI-Slot im Spiel | `Gameplay/Match/MatchConfig` | ❌ Netzstrang |
| KI-Snapshot-Block | `Simulation/Snapshots/SnapshotBlockIds` | ❌ **nur mit D-ID** |
| Fingerprint / Schemaversion | `Simulation/Replays/MatchFingerprint` | ❌ **nur mit D-ID** |

---

## 10. Determinismus-Leitplanken

Unter `Scripts/Simulation/` und `Scripts/AI*`, ohne Ausnahme:

- kein `float`/`double` — Festkomma (`SimFixed`), `NoFloatInSimulationTests` prüft
- kein `System.Random` — nur der Sim-PRNG
- keine Wanduhr, kein `DateTime.Now`, kein `Time.deltaTime` — Zeit ist in Ticks
- keine Abhängigkeit von `Dictionary`/`HashSet`-Iterationsreihenfolge —
  aufsteigende Index-Scans, wie `Decide()` es heute schon tut
- Ziel-Scoring bleibt ganzzahlig, auch die Gewichte in `AI.Data/`
- Gleichstände brauchen ein deterministisches Kriterium (niedrigste Entity-Id)

**Folge für jeden Verhaltens-PR:** Die vier Baseline-Dateien werden rot. Das ist
ihr Zweck. Verhaltens-PR **ohne** Baseline-Änderung (er ist dann rot, korrekt
so), neue Baseline in einem **eigenen PR** mit altem Wert, neuem Wert und
Begründung. Ausnahmslos.

Hier hilft das Labor unmittelbar: Es zeigt über die Hash-Kette, **ab welchem
Tick** zwei Stände auseinanderlaufen — statt nur, *dass* eine Baseline rot ist.

---

## 11. Etappen

Jede Etappe ist für sich abschließbar. Kein Enddatum.

### E0 — Voraussetzung: .NET-SDK

**Auf diesem Rechner ist kein .NET-SDK installiert.** `dotnet` ist nicht im
PATH, `Project_Nova/.dotnet/` existiert nicht (CLAUDE.md §5 nennt es als
Fallback). `global.json` pinnt `8.0.318` mit `rollForward: disable` — exakt
diese Version.

*Fertig, wenn:* `dotnet test tools/Nova.SimRunner.Tests/Nova.SimRunner.Tests.csproj -c Release`
grün durchläuft.

### E1 — Harness, KI gegen KI headless *(lokal)*

`MultiSlotAiHost` aus dem `AiHost`-Muster; 2..8 Slots, je KI-Slot eigene
Session/Ingress/Transport. Zwei KI-Slots gegeneinander bis Entscheidung oder
Tickbudget. Test, der die Systemreihenfolge gegen `MatchRunner` festnagelt.

*Fertig, wenn:* Eine vollständige KI-gegen-KI-Partie liefert Outcome und
Endzustands-Hash, und zwei Läufe mit gleichem Seed liefern identische Hashes.

### E2 — Lauftreiber, Metriken, Parallelität *(lokal)*

`MatchSpec` einlesen, `Parallel.For` über die Spezifikationen, je Lauf
`result.json` / `trace.ndjson` / `hashchain.json` / `match.replay` schreiben.
Metrikkatalog aus §4.3.

*Fertig, wenn:* Ein Kommando fährt *n* Matches parallel und legt *n*
Artefaktsätze ab. **Durchsatz gemessen und notiert** — bisher ungemessen, weil
E0 fehlt. Grobe Erwartung aus dem 2242-Tick-Referenzmatch und 24 Kernen: einige
hundert Matches pro Minute. Das ist eine Schätzung, keine Messung, und wird als
solche gekennzeichnet, bis sie eine ist.

### E3 — Sweep und Auswertung *(lokal)*

Matrix aus Seeds × Profilen × Fraktionen; Aggregation über Läufe (Siegrate,
Median-Entscheidungstick, Wirtschaftskurven, `intentsRejected`); Vergleich gegen
eine eingefrorene Referenzmenge.

*Fertig, wenn:* „Fahre 200 Seeds × 5 Profile" ist ein Befehl, und danach steht
eine auswertbare Tabelle da.

### E4 — Profile zu Daten *(PR, verhaltensneutral)*

`AI.Data/`-Profilformat aus §5.5, `AiFactionProfile` liest daraus, `const`-Werte
wandern hinüber. **Ausgelieferte Werte numerisch identisch** → Baselines bleiben
grün, und genau das ist die Prüfung, dass der Umbau sauber war.

*Fertig, wenn:* Baselines grün, Profile in Daten, ein Test hält die
Standardwerte gegen die alten Konstanten.

### E5 — Reaktive KI, Stufe 1 *(PR, verhaltensändernd)*

Lagebewertung, Utility-Zielauswahl, `DefendBase`, `DefendField`, `Retreat`,
Score-Targeting, `Farm`. Neue Tests in `SkirmishAiTests`. Vorher: Doku-Fix zu
GB-002/D-087 (§5.4).

**Folge:** Baselines werden rot → getrennte PRs, Baseline-PR mit altem und
neuem Wert.

*Fertig, wenn:* Die Definition of Done aus Issue `04` erfüllt ist — Verteidiger
kehren zurück, beschädigte Einheiten ziehen sich zurück, die Armee schießt aufs
gefährlichste erreichbare Ziel — **plus Spielbericht aus einer echten Partie**,
inklusive eines Falls, in dem die Reaktion falsch war, mit Einschätzung warum
das akzeptabel ist.

### E6 — Fehlende Befehlsarten *(PR, je Verhalten einer)*

`Repair`, `ReturnCargo`, `SetRallyPoint`, `InstallDefenseModule`, `Stop`,
`CancelConstruction`, `Sell`, `CancelProduction` — nach Nutzen sortiert, jeweils
klein und einzeln. Das Labor liefert je Verhalten den Vorher/Nachher-Vergleich.

### E7 — Sidecar-Vorschlag *(kein Code)*

Aus den E3-Auswertungen belegen, wo Zustandslosigkeit konkret schadet, und
daraus die D-ID-Anfrage nach §6 bauen. Vorschlag mit Belegen, keine Umsetzung.

### E8 — Goal-System mit Zustand *(nur nach D-ID)*

`SkirmishAiSystem` wird `IStatefulSimSystem` mit eigenem Block. Echte Hysterese
in Ticks, Squads, Aufklärungsgedächtnis. Metamorphic-Tests nach
`AIArchitecture.md` §6.

### E9 — Mehr Slots, Teams *(Vorschlag, blockiert)*

4-Slot-Freiforall im Labor sofort; echte Teams als ausgearbeiteter Vorschlag.
Umsetzung nur nach ausdrücklicher Zuweisung.

---

## 12. Entscheidungen, die wir selbst treffen

Bewusst hier festgehalten, damit sie nicht jedes Mal neu verhandelt werden:

| # | Entscheidung | Begründung |
|---|---|---|
| 1 | **Kein Sim-Nachbau in einer zweiten Sprache** | Zerstört die strukturelle 1:1-Eigenschaft (§1) |
| 2 | **C# statt Python, mit benanntem Wiedereinstieg** | Kein Gewinn beim Laufenlassen, Gewinn beim Auswerten erst bei Volumen (§2) |
| 3 | **Labor bleibt lokal und ist nicht merge-pflichtig** | Es ist Werkzeug, kein Beitrag; löst die meisten Scope-Fragen auf (§0) |
| 4 | **KI gegen KI im Labor ohne Rückfrage** | Der Harness umgeht `MatchConfig` wie `SimRunner`; es entsteht kein Spielmodus (§8) |
| 5 | **Gegen den Code bauen, nicht gegen die veraltete Doku** | D-087 ist implementiert; GB-002-Passage in `AI/` ist stale und wird korrigiert (§5.4) |
| 6 | **Stateless zuerst, Sidecar erst mit Belegen** | Issue `04` verlangt genau das; vermeidet eine unbegründete D-ID-Anfrage (§6) |
| 7 | **Datenumstellung verhaltensneutral, getrennt von Verhalten** | Grüne Baselines beweisen, dass der Umbau nichts verschoben hat (§5.5, E4) |
| 8 | **Labor-Ergebnisse sind Diagnose, nie Nachweis** | Deckungsgleich mit der bestehenden `output/`-Praxis (D-061/D-064) |

## 13. Was eine Inhaberentscheidung bleibt

Genau zwei Dinge — beide bewusst ans Ende gestellt, beide als vorbereiteter
Vorschlag statt als Vorbedingung:

1. **`AiSidecar` / KI-Snapshot-Block.** Braucht eine D-ID.
   `Simulation/Snapshots/` steht auf „niemand ohne D-ID". Vorschlag entsteht in
   E7 mit Messmaterial aus dem Labor. Bis dahin blockiert es nichts.
2. **Teams und mehr als zwei Slots im Spiel.** Berührt Vision, Victory,
   MatchConfig und den Modusvertrag in `mvp-v1.json`. Vorschlag in E9;
   das Labor kommt bis dahin mit Freiforall aus.

Alles andere, was zwischenzeitlich nach Rückfrage aussah — Ablageort des
Werkzeugs, Python im Repo, KI-vs-KI-Erlaubnis, Profil-Identität im Fingerprint —
ist durch das Betriebsmodell aus §0 und die Entscheidungen aus §12 erledigt.

---

## 14. Risiken

| Risiko | Wirkung | Gegenmaßnahme |
|---|---|---|
| Laborcode rutscht in einen PR | Fremder Scope, PR wird zurückgegeben | Getrennte Branches, PR-Tests ohne Laborabhängigkeit (§0) |
| Labor findet Verbesserung, Spiel widerlegt sie | Verlorene Arbeit, falsches Vertrauen | Replay-Gegenprobe in Unity (§4.4) vor jedem Verhaltens-PR |
| Überanpassung an Laborseeds | KI gewinnt Benchmarks, verliert Partien | Feste Referenzmenge getrennt von der Tuningmenge halten |
| Baseline-Rot wird zur Gewohnheit | Der eine Fehler, gegen den die Regel gebaut ist | Verhaltens-PR und Baseline-PR strikt getrennt, immer (§10) |
| Sidecar-Bedarf wird unterschätzt | Stufe 1 zappelt, wirkt schlechter als vorher | `goalSwitches` als Metrik von Anfang an mitschreiben (§4.3) |
| Tick-Reihenfolge im Harness driftet | Labor misst etwas anderes als das Spiel | Reihenfolge-Test gegen `MatchRunner` in E1 |

---

## 15. Was dieser Plan nicht tut

- **Keine zweite Simulation.** Weder Python noch sonst etwas. Die Simulation
  existiert genau einmal.
- **Keine stillschweigende Baseline-Anpassung.** Verhaltens-PR und Baseline-PR
  sind immer getrennt.
- **Kein Anfassen fremden Terrains.** Vision, Victory, Economy, Construction,
  MatchConfig, Snapshots, Replays bleiben unberührt. Wo dort etwas nötig würde,
  entsteht ein Vorschlag, keine Änderung.
- **Keine Fertigmeldung ohne gespielte Runde.** Ein grüner Laborlauf ist
  Diagnose. Was im Spiel nicht gesehen wurde, steht genau so im PR-Text.
