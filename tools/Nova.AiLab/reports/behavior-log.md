# Verhaltensjournal der KI

Jede Änderung am KI-Verhalten bekommt hier **einen Eintrag, bevor die nächste
beginnt.** Der Zweck ist nicht Buchhaltung, sondern zweierlei:

1. **Nachvollziehbarkeit.** Zu jeder Änderung stehen die genauen Werte, die
   gemessenen Folgen — *beide* Richtungen — und der Laufbericht, aus dem die
   Zahlen stammen.
2. **Keine doppelte Arbeit.** Was ausprobiert und verworfen wurde, steht unter
   „Widerlegt". Wer eine Idee hat, sucht sie zuerst hier. Eine Sackgasse, die
   niemand aufgeschrieben hat, wird zuverlässig ein zweites Mal gelaufen.

> [!IMPORTANT]
> **„Besser" und „schlechter" sind menschliche Urteile, keine Rechnung.** Das
> Labor vergibt keine Note (Entscheidung 11) — es legt Zahlen nebeneinander.
> Die Einordnung in diesem Journal trifft ein Mensch und trägt deshalb immer
> die Rohwerte mit, damit sie überprüfbar bleibt statt geglaubt zu werden.
>
> **Und nichts hier ist im Spiel gesehen worden.** Jeder Eintrag sagt in der
> Kopfzeile, ob er nur gemessen oder tatsächlich gespielt wurde.

## Vorlage

```markdown
## V00N · JJJJ-MM-TT · Kurzname
**Lauf:** `runs/<id>.md` · **Commit:** `xxxxxxx` · **Status:** im Labor gemessen / im Spiel gesehen

### Was genau geändert wurde
### Besser        (Kennzahl, vorher → nachher, Quelle)
### Schlechter    (dito — leer lassen ist verdächtig, nicht sauber)
### Unverändert   (was ausdrücklich NICHT verschoben wurde)
### Widerlegt     (Annahmen, die der Lauf gekippt hat — nicht nochmal prüfen)
### Offen
```

---

## V002 · 2026-08-08 · `DefendBase` — gebaut, gemessen, **verworfen**

**Status:** im Labor gemessen, **Code zurückgenommen** · **Basis:** V001 (`3f596d6`)

Kein Commit im Code. Dieser Eintrag existiert, damit die Regel nicht in drei
Monaten ein zweites Mal gebaut wird.

### Was gebaut wurde

Ein Feld `DefenseRadiusCells` (16) im Profil und ein Zweig in Schritt (6) von
`SkirmishAiSystem`: Ist ein **bewaffneter** Feind innerhalb des Radius um das
eigene HQ sichtbar, wählt die Armee ihr Ziel aus diesem Bereich und marschiert
dorthin — **ohne** Angriffsschwelle, weil Verteidigen nichts ist, wofür ein Slot
zu klein sein darf. Ein unbewaffneter Harvester am Zaun löste bewusst nichts
aus. Determinismus geprüft, 95 Kettenglieder identisch, Exit 0.

### Schlechter — auf jeder gemessenen Achse

| | V001 ohne | V002 mit |
|---|---:|---:|
| Entscheidungstick | 8.715 | 9.470 |
| Verluste | 70 / 97 | 75 / 107 |
| Intents je 1000 Ticks | 39,4 / 41,7 | 44,3 / **51,4** |
| `late-push` | 8.228 Ticks, 72 verloren | **13.953 Ticks, 146 verloren** |

Siegquoten aller sechs Kandidaten: **unverändert**. Die Verteidigung kostet
also und liefert nichts zurück, was diese Szenarien messen können.

### Widerlegt

> **Am Radius liegt es nicht.** Gemessen: 8 → Tick 9.564, 16 → 9.470,
> 24 → **byte-identisch zu 16** (die Kämpfe finden ohnehin innerhalb von 16
> Zellen statt). Alle drei liegen über den 8.715 ohne Verteidigung. Wer die
> Regel wieder aufgreift, braucht einen anderen Ansatz, keine andere Zahl.

> **Das Pendeln ist die wahrscheinlichste Ursache.** +23 % Intent-Rauschen bei
> Slot 1 heisst: es werden mehr Befehle neu erteilt, nicht mehr Ziele getroffen.
> Die Armee wird zwischen Front und Basis hin- und hergezogen. Der Plan sieht
> dagegen `switchHysteresis` vor (§4.6) — ein Zielwechsel verlangt Vorsprung,
> statt bei jedem Sichtkontakt umzuschalten. Ohne Hysterese ist ein zweiter
> Anlauf sinnlos.

### Einschränkung, die zum Befund gehört

**Gemessen wurde symmetrisches KI-gegen-KI.** Beide Slots verteidigen, also
bleiben beide zu Hause und die Partien ziehen sich. Gegen einen Menschen, der
die Basis wirklich überfällt, könnte dieselbe Regel wertvoll sein — das kann
dieses Labor nicht zeigen. „Kostet in KI-gegen-KI" ist nicht „nutzlos".

### Nachweis des Rückbaus

Nach dem Zurücknehmen liefert die Referenzpartie wieder Tick **8.715** und
Endzustand **`0x5D8FB2D45FFD16B6`**, und alle Artefakte sind byte-identisch zu
V001 bis auf `elapsedMilliseconds` — Wanduhr, keine Simulationsausgabe.

---

## V001 · 2026-08-08 · Score-Targeting statt Reihenfolge

**Lauf:** [`runs/20260808-2035-ab6cb9a1.md`](runs/20260808-2035-ab6cb9a1.md) ·
**Vorher-Lauf:** [`runs/20260808-1945-3b3f27d7.md`](runs/20260808-1945-3b3f27d7.md) ·
**Status:** im Labor gemessen, **im laufenden Spiel ungesehen** (Linux-Build steht aus)

### Was genau geändert wurde

| Ort | Änderung |
|---|---|
| `AI.Data/AiProfile.cs` | vier neue Ganzzahlfelder: `TargetDamageWeight`, `TargetThreatWeight`, `TargetFinishWeight`, `TargetDistanceWeight` — ohne Konstruktor-Vorgabewerte, damit kein Wert stillschweigend driften kann |
| `AI.Data/AiProfiles.cs` | `ms1-canonical` und `legacy-defaults` bekommen `dmg 10, threat 6, finish 3, dist 4` (Planskizze §4.6). **Die ersten Zahlen hier, die keine Kopie einer ausgelieferten Konstante sind** — Zielbewertung gab es vorher nicht |
| `AI/SkirmishAiSystem.cs` | `FindPreferredVisibleEnemy` → `FindBestVisibleEnemyByScore` plus `ScoreTarget` |
| `AI/AiFactionProfile.cs` | Alt-Konstruktor reicht die vier Gewichte aus dem ausgelieferten Profil durch |

Die Formel, ausschliesslich ganzzahlig:

```
score = W_dmg    · Ø DamageMatrix.Resolve(Waffe_i, Rüstungsklasse Ziel)
      + W_threat ·   Waffenschaden des Ziels
      + W_finish ·   fehlende Lebenspunkte in Prozent
      - W_dist   · Ø Chebyshev-Abstand Armee → Ziel
Gleichstand → niedrigere rohe Entity-Id
```

Drei Festlegungen, die nicht aus der Planskizze kommen:

- **Der Score gilt der Armee, nicht der Einzeleinheit**, weil dieser Aufruf
  *ein* gemeinsames Ziel liefert. Für die heute homogene Armee ist die
  Reihenfolge identisch zur Pro-Angreifer-Formel: das Mittel über n gleiche
  Angreifer *ist* der Einzelwert. Gemischte Armeen mitteln.
- **Das feindliche HQ bleibt ein Kurzschluss** und ist bewusst kein Gewicht:
  sein Verlust entscheidet die Partie (D-077). Eine Siegbedingung ist keine
  Vorliebe, die ein Gewicht überstimmen darf.
- **Eigene Einheiten werden hier gefiltert** — und nur hier. `ValidateDomain`
  hat für `AttackTarget` keinen Case, die Feuerphase prüft Reichweite und
  Sicht, aber nie den Besitzer. Ein expliziter Befehl auf eine eigene Einheit
  würde feuern.

### Besser

Referenzpartie `ms1-canonical` gegen sich selbst, Seed `0xA17E57DE57`:

| Kennzahl | vorher | nachher | |
|---|---:|---:|---|
| Entscheidungstick | 12.975 | **8.715** | −33 % |
| Verluste Slot 0 | 113 | **70** | −38 % |
| Verluste Slot 1 | 137 | **97** | −29 % |
| Eingereichte Intents | 443 / 578 | 343 / 363 | weniger Befehlsrauschen |

Beide Seiten verlieren weniger, obwohl beide dasselbe neue Zielverhalten
fahren — die Armee schiesst auf das, was sie tatsächlich beschädigen kann,
statt auf das erste sichtbare Gebäude. `late-push` entscheidet 4.370 Ticks
früher bei 43 % weniger Verlusten (126 → 72) und hält seine 100 %.

### Schlechter

**Die Änderung ist nicht für jedes Profil eine Verbesserung** — und genau
deshalb steht sie hier:

| Kandidat | Siegquote | Entscheidungstick | Verluste |
|---|---|---|---|
| `early-push` | 50 % → 0 % | 16.299 → 15.401 | 156 → 150 |
| `greedy-economy` | 50 % → 50 % | 8.635 → **11.470** | 86 → **114** |
| `fast-cadence` | 50 % → 50 % | 11.454 → **12.948** | 110 → **132** |

**Nachgesehen statt vermutet — und die erste Zeile trägt nicht.** „50 % → 0 %"
klingt nach Einbruch und ist eine einzige gekippte Partie. `early-push` spielt
zwei Partien: eine verliert es vorher wie nachher nach rund 7.200 Ticks. Die
andere ist ein Zermürbungskrieg über **23.500 von 27.000 Ticks** — 87 % des
Budgets, über 470 tote Einheiten zusammen — und *die* kippte von Slot 0 auf
Slot 1. In der aufgezeichneten Partie verliert `early-push` dabei sogar
**weniger** Einheiten als der Gegner (211 gegen 264).

Das ist keine Eigenschaft der Score-Formel, sondern die Auflösungsgrenze des
Messaufbaus: **bei zwei Partien je Kandidat ist ein Siegquotensprung von 50
Punkten genau eine Partie.** Die Seed-Achse ist leer, es gibt also keine
Streuung, aus der sich das herausmitteln liesse. `greedy-economy` und
`fast-cadence` sind aus demselben Grund Einzelmessungen — ihre Tick- und
Verlustzahlen sind echt, aber sie sind je *eine* Beobachtung, keine Tendenz.

Ausserdem sinkt in der Referenzpartie der Endkassenstand (33.460 / 38.810 →
21.790 / 25.020) und die Armee am Ende ist kleiner (7/7 → 7/4). Beides ist
Folge der kürzeren Partie und nicht für sich schlecht — aufgeschrieben, damit
niemand es später als eigenständigen Effekt liest.

### Unverändert

- `intentsRejected` bleibt **0 von 706**. Kein einziger Befehl läuft gegen eine
  Executor-Regel.
- **Determinismus**: zwei Läufe desselben Specs stimmen auf allen 88
  Kettengliedern überein, Exit-Code 0.
- Duell-Arena und Bewegungsszenarien: **byte-identisch**. Sie fahren kein
  KI-System, das ist die Gegenprobe, dass nichts ausserhalb der KI verrutscht ist.
- **557/557 SimRunner-Tests und 87/87 Labortests grün.**

### Widerlegt

> **Die vier Determinismus-Baselines werden von einer reinen KI-Änderung
> NICHT rot.** Das war die Erwartung — im Plan (§8, E7) und in `AGENTS.md` —
> und sie stimmt nicht. `SnapshotGoldenBytesTests`, `CommandGoldenBytesTests`,
> `SimRandomGoldenTests` und `Determinism10000Tests` erwähnen `SkirmishAi` mit
> keiner Zeile; ihre Szenarien fahren kein KI-System. Rot wird von einer
> KI-Änderung nur, was die KI auch ausführt.
>
> Die Trennungsregel gilt trotzdem unverändert: `check_baseline_guard.py` führt
> `Scripts/AI/` in seinen Simulationspfaden, ein PR mit Verhalten **und**
> Baseline wird also weiterhin maschinell abgelehnt. Nur die Begründung „er
> wäre ohnehin rot" trägt hier nicht.

> **Eine Siegquote aus `compare` hat drei mögliche Werte: 0 %, 50 %, 100 %.**
> Zwei Partien je Kandidat, keine Seed-Streuung — jeder Sprung ist eine
> gekippte Partie, und eine Partie, die 87 % des Tickbudgets läuft, kippt an
> allem. Wer aus dieser Spalte eine Tendenz liest, liest Rauschen. Brauchbar
> wird sie erst mit einer echten Varianzquelle; bis dahin sind
> Entscheidungstick und Verlustzahlen die belastbareren Spalten, und selbst
> die sind Einzelmessungen.

> **`SkirmishAiTests` fängt eine Zielverhaltensänderung nicht.** Der
> End-to-End-Test prüft Ausgang, Sieger und „mindestens 6 Infanteristen", nicht
> den Entscheidungstick. Er blieb grün, während sich die Partie um 4.260 Ticks
> verschob. **Geschlossen** durch
> `SkirmishAi_ShootsTheDangerousTarget_NotTheFirstOneInTheVisibleList`: ein
> Harvester (zuerst gespawnt, also niedrigerer Entity-Index) und ein
> BattleTank erscheinen gleichzeitig neben der Armee — die alte Regel hätte
> den Harvester gewählt. Der Test ist nachweislich tragend: mit
> `targetThreatWeight: 0` fällt er.

> **Unterhalb der Angriffsschwelle gibt es überhaupt kein KI-Zielverhalten.**
> Beim Schreiben des Tests gemessen: mit fünf Einheiten (Schwelle 6) reicht die
> KI **keinen einzigen** `AttackTarget`-Befehl ein. Was die Einheiten tragen,
> ist ausschliesslich D-087-Auto-Acquisition, und die nimmt das *nächste*
> sichtbare Ziel, nicht das gefährlichste: **vier von fünf schiessen auf den
> Harvester, während der Panzer eine Zelle daneben steht.** In dem Moment, in
> dem die sechste Einheit fertig wird, springen alle fünf auf den Panzer.
> Der Score regiert also erst ab der Schwelle — darunter regiert die
> Entfernung. Das ist kein Fehler dieser Änderung, sondern eine Lücke, die
> vorher niemand sehen konnte.

### Offen

- ~~Warum kippt `early-push`?~~ **Nachgesehen, siehe „Schlechter":** eine
  gekippte Partie an der Auflösungsgrenze, keine Eigenschaft der Formel. Was
  offen *bleibt*: warum diese Paarung überhaupt 23.500 Ticks braucht. Zwei
  Armeen, die sich gegenseitig endlos nachbauen und aufreiben, ohne dass eine
  Seite die Basis der anderen erreicht — das ist ein Verhaltensbefund für
  sich, unabhängig vom Zielverhalten.
- **Ziel je Einheit** statt ein Armeeziel. Heute ohne Unterschied (homogene
  Armee), sobald die KI Fahrzeuge baut nicht mehr.
- **Die Lücke unterhalb der Schwelle.** Zwischen dem ersten Soldaten und dem
  sechsten schiesst die Armee nach Entfernung statt nach Gefahr. Ob das
  überhaupt stört, ist offen — verteidigen muss sie in dieser Phase ohnehin,
  und `DefendBase` aus E7 wird genau dort ansetzen.
- **Kein Spielbericht.** E7 ist erst mit einer echten Partie fertig, inklusive
  eines Falls, in dem die Reaktion falsch war. Das hängt am Linux-Build, der
  Bringschuld des Netzstrangs ist.
- Die vier Gewichte sind ungetunt aus der Planskizze übernommen. `compare` kann
  sie jetzt variieren — `LabProfiles.Derive` kennt sie.

---

## V000 · 2026-08-08 · Ausgangslage

**Lauf:** [`runs/20260808-1945-3b3f27d7.md`](runs/20260808-1945-3b3f27d7.md) ·
**Status:** Referenz, kein Eingriff

Das Verhalten, gegen das V001 gemessen wurde: `ms1-canonical` mit den acht
Werten, die das Spiel ausliefert, Zielwahl nach „HQ, sonst erstes sichtbares
Gebäude, sonst erste sichtbare Einheit". Entscheidung bei Tick 12.975,
Endzustand `0x4947D4769384585C`, Verluste 113 / 137.

Die Etappen E0–E6 stehen im Plan
([`docs/feature-ideas/AiSimulationEnvironment.md`](../../../docs/feature-ideas/AiSimulationEnvironment.md)
§9) und werden hier nicht wiederholt: sie haben das Labor gebaut, nicht das
Verhalten geändert. E6 hat die Zahlen aus dem Code in Daten verschoben —
nachweislich verhaltensneutral, denn die Baselines blieben grün.
