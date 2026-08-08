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
   von `upstream/main` abgezweigt. Kein Cherry-Pick aus `lab/`.
2. **PR-Tests hängen nicht vom Labor ab.** Sie folgen dem Muster in
   `SkirmishAiTests.cs` — in sich geschlossen. Sonst ist der PR ohne das lokale
   Werkzeug nicht baubar.
3. **Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Das Repo behandelt
   seine `output/`-Artefakte schon so (D-061/D-064). Was im Spiel nicht gesehen
   wurde, steht genau so im PR-Text.

---

## 1. Befund: die Simulation hängt nicht an Unity

| Beleg | Fundstelle |
|---|---|
| Kernel engine-frei | `Simulation/SimulationKernel.cs:13` |
| KI engine-frei | `AI/SkirmishAiSystem.cs:88` |
| Headless-Läufer, net8.0 | `tools/Nova.SimRunner/Program.cs` — Host in ~40 Zeilen |
| Vollständige KI-Partie headless | `tools/Nova.SimRunner.Tests/SkirmishAiTests.cs` — Entscheidung bei Tick 2242 |
| 10.000-Tick-Determinismuslauf | `tools/Nova.SimRunner/Determinism10000Scenario.cs` |

Entscheidend ist das *Wie*: Die Headless-Lane kompiliert per
`<Compile Include="..\..\Assets\_Project\Scripts\...">` **dieselben
Quelldateien**, die Unity lädt — der *shared-sources contract* (G0-B,
`SimulationCore.md` §9).

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
| **R2** | Parallel, vielfach, schnell | ⚠️ möglich, keine Ansteuerung |
| **R3** | Bit-identisch zum Spiel | ✅ strukturell garantiert |
| **R4** | KI gegen KI | ⚠️ im Labor baubar, im Spiel gesperrt |
| **R5** | 2 gegen 2 | ❌ kein Team-Begriff |
| **R6** | Verhalten A–Z | ⚠️ 5 von 13 Befehlsarten genutzt (§6) |
| **R7** | Reagierendes Goal-System | ❌ KI ist bewusst zustandslos |
| **R8** | Eine Stelle zum Ändern | ❌ Werte stecken im Code |
| **R9** | 2D-Sichtfenster | ❌ nichts vorhanden (§3.4) |

R6–R9 sind die Arbeit.

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
*„byte-exact wiring mirror of MatchRunner.InitializeMatch"*.

Zu verallgemeinern: Slots von 2 auf N (≤ 8), je KI-Slot eigene `MatchSession` +
`CommandIngress` + `AiPeerCommandTransport` in die eine Host-Ingress. Offen
dafür: `CommandLimits.ReservedPlayerSlots = 8`, `FogOfWarSystem.MaxTeams = 8`,
`VictorySystem.MaxSlots = 8`.

**Registrierungsreihenfolge unverändert** — sie ist Vertrag
(`13-15_Parallelbetrieb.md`, Punkt 3): Economy → Construction → Production →
Pathfinding → Movement → FogOfWar → Combat → [KI-Slots] → Victory. Ein Test
nagelt sie gegen `MatchRunner` fest.

**Isolation:** Jedes Match baut Kernel, `EntityManager` und alle Systeme frisch.
Geteilt wird nur Unveränderliches (`SimDefinitions`, `WeaponProfiles`,
`DamageMatrix` sind `static readonly`, damit thread-sicher) — N Matches auf N
Kernen, ohne Sperren.

### 3.2 Ein- und Ausgabe

```json
{ "specVersion": 1, "seed": "0xA17E57DE57", "tickBudget": 27000,
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
   behauptet (`MatchFingerprint` verweigert den Start bei jeder Abweichung).
   **Das ist die Brücke zurück:** Was das Labor findet, wird so gegengeprüft,
   bevor es als „gesehen" gilt.

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
| **Profil** (ab E5 nur Daten) | alte Profildatei läuft im aktuellen Binary | echtes Kopf-an-Kopf im selben Lauf |
| **Codestand** (Goal-System, E6+) | läuft *nicht* im selben Binary | nur über **eingefrorene Ergebnismengen**: gleiche Seeds, gleiche Spec, gespeicherte Kennzahlen |

Der zweite Fall ist der Normalfall bei Verhaltensarbeit — und er stellt eine
Bedingung: Die **Referenz-Seedmenge und die Spec-Version müssen fixiert sein**.
Ändert sich die Startaufstellung oder die Spec, ist jeder Vergleich mit alten
Ergebnismengen wertlos, und das muss auffallen statt durchzurutschen. Deshalb
trägt jede Ergebnismenge Spec-Version, Seedliste und `ComputeDefinitionsHash64()`
mit; passt eines nicht, verweigert der Bericht den Vergleich.

**Tuningmenge und Referenzmenge bleiben getrennt.** Sonst gewinnt die KI
Benchmarks und verliert Partien.

### 3.8 Befundliste für fremdes Terrain

Das Labor wird Dinge aufdecken, die uns nicht gehören — in Economy,
Pathfinding, Production, Vision. Diese Funde werden gesammelt und weitergegeben,
nicht selbst repariert (Arbeitsvertrag §6: „fragen, nicht entscheiden").

Je Befund im Labor unter `findings/`: Beobachtung, betroffener Pfad und
Eigentümer, **Seed und MatchSpec zur Reproduktion**, das `match.replay`, und die
Fundstelle im Code. Damit ist ein Fund für den Maintainer nachvollziehbar,
statt eine Behauptung zu sein — und das ist der einzige Weg, wie er je behoben
wird.

Ein bereits bekannter Kandidat steht in §5: `SetRallyPoint` lehnt das Refinery
ab, weil die Producer-Liste älter ist als der D-077-Umzug.

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
Die veraltete Doku-Passage ist ein eigener, winziger PR in `AI/`. Kein Blocker.

### 4.6 `AI.Data/` — die eine Stelle zum Tunen

Enthält heute **nur ein asmdef**, gehört uns exklusiv, ist genau dafür gedacht.
Die Werte stecken verstreut in `AiFactionProfile`-Defaults (`TargetPowerMargin
= 30`, `TargetArmySize = 15`, `AttackSquadThreshold = 8`,
`TargetHarvesterCount = 2`) und `const` im System (`DecisionTickInterval = 20`,
`PlacementSearchRadius = 8`, `InfantryQueueBatch = 2`).

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

**Bekannte Einschränkung:** `SetRallyPoint` lehnt das Refinery heute ab — die
Producer-Liste in `ProductionSystem` ist älter als der D-077-Umzug des
Harvester-Produzenten. Sim-seitiger Fehler außerhalb unseres Scopes; die KI
mikromanagt deshalb wie ein Mensch. Nicht umgehen, nur wissen.

---

## 6. KI gegen KI und 2 gegen 2

**KI gegen KI im Labor: ja.** `SkirmishAiTests` fährt heute KI gegen einen
passiven Slot. Zwei KI-Instanzen zu verdrahten ist eine überschaubare
Erweiterung. Im *Spiel* ist es gesperrt (`MatchConfig`: `AiSlots.Length > 1`
wirft; `mvp-v1.json` schreibt `solo-human-vs-ai` bindend fest) — das betrifft
das Labor nicht, weil der Harness den Host direkt baut wie `SimRunner` und nicht
durch `MatchConfig` geht. Es entsteht kein Spielmodus, nur ein Testaufbau.

**2 gegen 2: strukturell offen, inhaltlich blockiert.** Da sind 8 Slots, 8
Team-Masken, 8 Victory-Slots. Es fehlt ein Team-Begriff: Feindschaft ist heute
`candidate.PlayerId == attacker.PlayerId` (`CombatSystem.cs:192`), und FoW setzt
`team == PlayerSlot` (D-058). Ein Verbündeter wäre ein Ziel, zwei Verbündete
teilten keine Sicht.

| Nötig | Datei | Eigentümer |
|---|---|---|
| Freund/Feind | `Simulation/Combat/` | **uns** ✅ |
| geteilte Sicht | `Simulation/Vision/` | nicht zugeteilt ❌ |
| Niederlage je Seite | `Simulation/Victory/` | nicht zugeteilt ❌ |
| Slot-/Modusvertrag | `MatchConfig`, `mvp-v1.json` | Netzstrang / Governance ❌ |

Im Labor ist eine 4-Slot-Partie *ohne* Bündnisse sofort machbar und liefert
schon viel. Echte Teams sind ein Vorschlag, keine einseitige Umsetzung. Der
Harness wird ab E1 N-Slot-fähig gebaut.

---

## 7. Scope-Landkarte (nur für PR-Inhalte)

| Vorhaben | Pfad | Status |
|---|---|---|
| Goal-System, Angriffserkennung, Rückzug, Score-Targeting | `Scripts/AI/` | ✅ **uns** |
| Profile und Gewichte als Daten | `Scripts/AI.Data/` | ✅ **uns** (leer) |
| Freund/Feind für Teams | `Simulation/Combat/` | ✅ **uns** |
| Bewegung am Ziel | `Simulation/Movement/` | ✅ **uns** |
| Legion-Waffenidentität | `Simulation/Factions/` | ✅ **uns** |
| Tests zu neuen Entscheidungen | `tools/Nova.SimRunner.Tests/` | ✅ **uns** (außer den 4 Baselines) |
| Geteilte Team-Sicht | `Simulation/Vision/` | ❌ nicht zugeteilt |
| Team-Niederlage | `Simulation/Victory/` | ❌ nicht zugeteilt |
| Mehr als ein KI-Slot im Spiel | `Gameplay/Match/MatchConfig` | ❌ Netzstrang |
| KI-Snapshot-Block | `Snapshots/SnapshotBlockIds` | ❌ **nur mit D-ID** |
| Fingerprint / Schemaversion | `Replays/MatchFingerprint` | ❌ **nur mit D-ID** |

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

### E0 — .NET-SDK ✅ **erledigt (2026-08-08)**

SDK 8.0.318 unter `Project_Nova/.dotnet/` (`global.json` pinnt hart mit
`rollForward: disable`). Nachweis: **549 Tests, 0 Fehler, 10 s.** Die komplette
Suite inklusive Determinismus-Baselines und End-to-End-KI-Partie braucht zehn
Sekunden — die Durchsatzerwartung aus E2 ist damit eher konservativ.

### E1 — Harness, KI gegen KI *(lokal)*

`tools/Nova.AiLab/` anlegen, `MultiSlotAiHost` aus dem `AiHost`-Muster.
N-Slot-fähig gebaut, **erst mit 2 Slots belegt** — 4 Slots sind danach eine
Konfigurationszeile. Reihenfolge-Test gegen `MatchRunner`.

**Startaufstellung exakt kanonisch** (`MatchBootstrap`): je Slot ein
Aetherium-Feld, fertiges HQ, ein Builder, 3.000 AE — und dieselbe Spawn-
Reihenfolge, Slot 0 vor Slot 1, weil sie Entity-Ids und Snapshots bestimmt.
Kartenvarianz kommt erst nach E6: Die KI setzt heute voraus, dass das
entfernteste Feld die Feindbasis markiert (`GetEnemyStartAreaCell`) — bei freier
Aufstellung bricht diese Annahme, und man tunt gegen einen Fehler statt gegen
das Verhalten.

*Fertig, wenn:* Eine KI-gegen-KI-Partie liefert Outcome und Endzustands-Hash,
und zwei Läufe mit gleichem Seed liefern identische Hashes.

### E2 — Lauftreiber, Metriken, Parallelität *(lokal)*

`MatchSpec` einlesen, `Parallel.For`, Artefakte je Lauf, Metrikkatalog aus §3.3.

*Fertig, wenn:* Ein Kommando fährt *n* Matches parallel. **Durchsatz gemessen
und notiert.**

### E3 — 2D-Sichtfenster, beide Darstellungen *(lokal)*

`ViewRecorder` nach §3.4, Terminalansicht live und HTML-Abspieler zur Nachschau
in einem Zug — beide lesen denselben Frame-Strom, der Mehraufwand gegenüber
einer Darstellung ist gering. Ebenen: Fog of War je Team, verworfene Intents.

Bewusst **vor** dem Sweep: Tausend Läufe auszuwerten hilft wenig, solange man an
einem einzelnen nicht erkennt, was schiefging.

*Fertig, wenn:* Eine laufende Partie ist im Terminal verfolgbar, ein
abgeschlossener Lauf im Browser zurückspulbar — **und ein Test belegt, dass ein
Lauf mit und ohne Sichtfenster dieselbe Hash-Kette liefert.**

### E4 — Vergleichsbericht und Gegnerarchiv *(lokal)*

Matrix aus Seeds × Profilen × Fraktionen. Ergebnis ist der Vergleichsbericht aus
§3.6 — **Kennzahlen nebeneinander, keine Rangliste**: eine Zeile je Kandidat,
Abweichung zur Referenz hervorgehoben, Link zum Sichtfenster-Lauf.

Dazu das Archiv aus §3.7: eingefrorene heutige KI als Maßstab, Momentaufnahmen
eigener Fassungen als Verlaufsvergleich. Ergebnismengen tragen Spec-Version,
Seedliste und `ComputeDefinitionsHash64()`; passt eines nicht, verweigert der
Bericht den Vergleich statt still Unvergleichbares zu mischen.

*Fertig, wenn:* Zwei Kandidaten sind in Minuten gegeneinander beurteilbar —
Bericht lesen, auffälligen Lauf im Sichtfenster nachschauen, entscheiden.

### E5 — Profile zu Daten *(PR, verhaltensneutral)*

`AI.Data/`-Format aus §4.6, `const` wandert hinüber, ausgelieferte Werte
numerisch identisch → Baselines bleiben grün.

### E6 — Reaktive KI, Stufe 1 *(PR, verhaltensändernd)*

`DefendBase`, `DefendField`, `Retreat`, Score-Targeting, `Farm`. Vorher der
Doku-Fix zu GB-002/D-087. Baselines werden rot → getrennte PRs.

*Fertig, wenn:* Definition of Done aus Issue `04` erfüllt — Verteidiger kehren
zurück, beschädigte Einheiten ziehen sich zurück, die Armee schießt aufs
gefährlichste erreichbare Ziel — **plus Spielbericht aus einer echten Partie**,
inklusive eines Falls, in dem die Reaktion falsch war, mit Einschätzung warum
das akzeptabel ist.

### E7 — Fehlende Befehlsarten *(PR, je Verhalten einer)*

Nach Nutzen sortiert aus §5, jeweils klein und einzeln. Das Labor liefert je
Verhalten den Vorher/Nachher-Vergleich.

### E8 — Sidecar-Vorschlag *(kein Code)*

Aus den E4-Auswertungen belegen, wo Zustandslosigkeit schadet; daraus die
D-ID-Anfrage nach §4.7 bauen. Vorschlag mit Belegen, keine Umsetzung.

### E9 — Goal-System mit Zustand *(nur nach D-ID)*

`IStatefulSimSystem` mit eigenem Block, echte Hysterese in Ticks, Squads,
Aufklärungsgedächtnis. Metamorphic-Tests nach `AIArchitecture.md` §6.

### E10 — Mehr Slots, Teams *(Vorschlag, blockiert)*

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
| 9 | Laborergebnisse sind Diagnose, nie Nachweis | Deckungsgleich mit der `output/`-Praxis (D-061/D-064) |
| 10 | Beide Sichtdarstellungen in einem Zug | Gemeinsamer Frame-Strom, Mehraufwand gering (§3.4) |
| 11 | **Keine skalare Gütefunktion, kein Auto-Optimierer** | Eine Zahl belohnt das Falsche; für „sieht im Spiel richtig aus" gibt es keine Kennzahl (§3.6) |
| 12 | Referenz: eingefrorene heutige KI + eigene Momentaufnahmen | Fester Maßstab plus Verlaufsvergleich; Rückschritt fällt auf (§3.7) |
| 13 | Kanonische Startaufstellung, Kartenvarianz erst nach E6 | Sonst tunt man gegen die gebrochene `GetEnemyStartAreaCell`-Annahme (E1) |
| 14 | Fremde Befunde sammeln und melden, nicht reparieren | Arbeitsvertrag §6; mit Seed und Replay reproduzierbar (§3.8) |

## 11. Was Inhaberentscheidung bleibt

Genau zwei — beide als vorbereiteter Vorschlag statt als Vorbedingung, beide
blockieren zwischenzeitlich nichts:

1. **`AiSidecar` / KI-Snapshot-Block** — braucht eine D-ID. Vorschlag in E8 mit
   Messmaterial aus dem Labor.
2. **Teams und mehr als zwei Slots im Spiel** — berührt Vision, Victory,
   MatchConfig und `mvp-v1.json`. Vorschlag in E10; das Labor kommt bis dahin
   mit Freiforall aus.
