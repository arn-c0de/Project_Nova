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

## M001 · 2026-08-09 · **Methodenbefund** — V002 und V003 wurden im Selbstspiel beurteilt

**Status:** kein Lauf, eine Feststellung über den Messaufbau ·
**Betrifft:** [V002](#v002--2026-08-08--defendbase--gebaut-gemessen-verworfen),
[V003](#v003--2026-08-08--zielen-unter-der-angriffsschwelle--vier-varianten-verworfen-die-form-bleibt)

Beide Rückweisungen stützen sich auf Messungen an `ms1-canonical` **gegen sich
selbst**. Eine Verhaltensregel steckt aber im Binary, nicht im Profil — also
bekommen in dieser Partie **beide** KIs sie gleichzeitig.

> Zwei Armeen, die beide besser zielen oder beide heimlaufen, liefern eine
> längere und blutigere Partie. Genau das wurde gemessen. **„Später
> entschieden, mehr Verluste" heisst im Selbstspiel nicht „schlechtere KI" —
> es kann „zwei stärkere Armeen" heissen, und der heutige Aufbau kann die
> beiden Fälle nicht unterscheiden.**

`compare` hilft nicht von allein: Kandidaten unterscheiden sich dort
ausschliesslich in Zahlen, eine Coderegel erreicht ebenfalls beide Seiten.

**Abhilfe, und sie war einen Profilwert entfernt.** Jedes neue Verhalten
bekommt eine **Aus-Stellung** (`waveSize: 1`, `retreatEnterHealthPercent: 0`,
`defenseRadiusCells: 0`). Dann spielt dasselbe Binary „mit" gegen „ohne",
einseitig, in einem `compare`-Lauf. V002 hatte das Feld bereits und hat es
nicht so benutzt — es verglich zwei komplette Messmengen vor/nach dem Umbau.

**Was das für die beiden Einträge heisst:** Ihre Zahlen stimmen, ihre
Schlussfolgerung ist **offen**. Weder „DefendBase kostet" noch „Zielen unter
der Schwelle kostet" ist damit widerlegt — aber auch nicht mehr belegt. Wer
einen der beiden wieder aufgreift, misst ihn einseitig, und zwar bevor er
irgendetwas umbaut.

Ausführlich in [`../NEXT-STEPS.md`](../NEXT-STEPS.md) §7, samt der vier
Kennzahlen, die Spielgefühl überhaupt abbilden können.

---

## B001 · 2026-08-08 · **Gespielte Beobachtung** — Mensch gegen KI

**Quelle:** Partie am Rechner, kein Laborlauf · **KI-Verhalten:** `r2.A037B84D`

> [!IMPORTANT]
> Dies ist die **einzige** Eintragsart in diesem Journal, die kein Laborlauf
> ist — und die einzige, die im Sinne des Repos etwas *beweist*. Alles andere
> hier ist Diagnose. Beobachtungen bekommen deshalb ein eigenes Kürzel `B`
> statt `V`: Sie messen nichts nach, sie stellen fest.

Vier Beobachtungen, wörtlich und dann eingeordnet:

**1. „Manchmal kommen sie in kleiner Gruppe, oft danach einzeln."**
Bestätigt NEXT-STEPS §1 aus der Spielersicht. Die erste Welle ist die Armee,
die bei Erreichen der Schwelle stand; alles danach ist Nachschub, der einzeln
losläuft. Im Labor sah man davon nur eine gleichmässig steigende Verlustkurve.

**2. „Rennen immer auf Headquarter."**
Das ist kein Zufall und kein Balancing-Problem, das ist eine Zeile: Ist das
feindliche HQ sichtbar, bricht `FindBestVisibleEnemyByScore` sofort ab und
liefert es zurück — und die Marschzelle wird die HQ-Zelle. Ist gar nichts
sichtbar, marschiert die Armee zum entferntesten Aetherium-Feld, und das liegt
neben der Basis. **Beide Wege zeigen auf denselben Punkt.**

> **Diese Beobachtung widerlegt eine Entscheidung aus V001.** Dort steht:
> „Das feindliche HQ bleibt ein Kurzschluss und ist bewusst kein Gewicht: sein
> Verlust entscheidet die Partie (D-077). Eine Siegbedingung ist keine
> Vorliebe." Das ist als Regel richtig und als *Verhalten* falsch. Es macht die
> KI vollständig vorhersagbar und schickt sie durch jede Verteidigung, die
> zwischen ihr und dem HQ steht. Der Kurzschluss gehört durch ein hohes
> **Gewicht** ersetzt — hoch genug, dass ein freiliegendes HQ gewinnt, nicht so
> hoch, dass ein verteidigtes alles andere überstimmt.

**3. „Halten keinen Abstand zu meinen Fernkampfangreifern."**
Die Laborzahl dazu ist der Standoff-Überlauf, aber sie misst die *eigene*
Reichweite. Der Spieler beschreibt die andere Hälfte: Die KI erkennt die
Reichweite des **Gegners** überhaupt nicht und läuft in sie hinein. Im Labor
bisher nicht gemessen — es gibt kein Szenario dafür.

**4. „Laufen einfach straight line, anstatt diese zu umlaufen, solange
Leben sparen sich mehr rentiert als der Umweg, den sie nehmen müssten."**
Das ist die präziseste Formulierung des Problems in diesem ganzen Journal, und
sie enthält bereits die Regel: **Umweg nehmen, solange der Umweg billiger ist
als die Verluste auf der geraden Linie.** Beides ist ganzzahlig rechenbar —
Zellen gegen erwarteten Schaden — und braucht keinen Zufall und kein Gedächtnis.

### Was daraus folgt

- NEXT-STEPS §2 wird um den HQ-Kurzschluss erweitert; er ist die Ursache, nicht
  nur `GetEnemyStartAreaCell`.
- Die Kostenregel aus Beobachtung 4 wird die Formulierung, gegen die gebaut wird.
- Ein Laborszenario für Beobachtung 3 fehlt: eine Gruppe läuft gegen einen
  **stehenden Fernkämpfer** und die Frage ist, wie viel Schaden sie auf dem Weg
  frisst. Das ist ein `movement`-Szenario, kein Duell.

---

## V003 · 2026-08-08 · Zielen unter der Angriffsschwelle — vier Varianten, **verworfen**; die Form bleibt

**Lauf:** [`runs/20260808-2153-206d8bc5.md`](runs/20260808-2153-206d8bc5.md) ·
**Variantenlauf (a):** [`runs/20260808-2146-206d8bc5.md`](runs/20260808-2146-206d8bc5.md) ·
**Status:** im Labor gemessen, **Verhaltensteil zurückgenommen**, Formänderung bleibt ·
**Basis:** V001/V002-Stand (`0x5D8FB2D45FFD16B6`, Tick 8.715)

Dieser Eintrag hat zwei Hälften, die man auseinanderhalten muss: eine
**Formänderung, die bleibt und nichts am Verhalten ändert**, und eine
**Verhaltensregel, die gebaut, viermal gemessen und wieder ausgebaut wurde.**

### Was bleibt: Schritt (6) in drei Stufen

`SkirmishAiSystem` erteilt keinen Armeebefehl mehr, sondern löst auf:

```
ArmyPosture posture = ResolveArmyPosture(...);     // was tut die Armee
foreach (Einheit)  UnitAssignment a = ResolveUnitAssignment(...);   // was tut diese Einheit
SubmitAssignments(...);                            // gleiche Befehle → ein Intent
```

Die Regeln sind dabei **unverändert** geblieben; der Nachweis ist keine
Testzusage, sondern eine Zahl: Entscheidungstick **8.715**, Endzustand
**`0x5D8FB2D45FFD16B6`**, Hash-Kette und `result.json` byte-identisch zur
Referenz bis auf `elapsedMilliseconds`. `AiBehaviorId.Revision` bleibt deshalb
bei **2** — die Regel dort sagt „bumpen, wenn sich Verhalten ändert", und hier
ändert sich keins.

Der Zweck: „diese eine Einheit dreht ab", „Nachschub wartet am Sammelpunkt"
und „zielen, bevor die Schwelle steht" sind in einem Armeebefehl **nicht
formulierbar**. Alle drei stehen in NEXT-STEPS.

### Was gebaut und wieder ausgebaut wurde

Die Angriffsschwelle sollte nur noch den *Vormarsch* regeln, nicht das
*Zielen* (NEXT-STEPS §4). Vier Fassungen, alle deterministisch (Exit 0), alle
auf der Referenzpartie `ms1-canonical` gegen sich selbst, Seed `0xA17E57DE57`:

| Fassung | Entscheidungstick | Verluste 0/1 | Intents 0/1 |
|---|---:|---:|---:|
| **r2 — kein Zielen unter der Schwelle** | **8.715** | **70 / 97** | **343 / 363** |
| (a) je Einheit ihr bestes Ziel in eigener Reichweite | 10.847 | 90 / 122 | 451 / 468 |
| (a′) wie (a), ohne HQ-Kurzschluss | 10.855 | 90 / 126 | 455 / 474 |
| (b) **ein gemeinsames** Ziel, nur wer es erreicht | 9.664 | 77 / 107 | 397 / 382 |
| (c) nur korrigieren: unbewaffnetes Ziel → bewaffnetes | 10.831 | 89 / 124 | 421 / 446 |

### Schlechter

Alle vier kosten auf allen drei gemessenen Achsen. Die beste Fassung (b) liegt
immer noch 11 % später und bei 10 % mehr Verlusten je Slot als gar kein Zielen.

### Warum — der Befund, der den Aufwand wert war

**Ein expliziter Angriffsbefehl ist eine Einbahnstrasse.** Im Code nachgesehen,
nicht vermutet: `UnitState.AttackTarget` wird an genau drei Stellen geschrieben
— vom Befehl `AttackTarget`, von der D-087-Auto-Acquisition **in ein leeres
Feld**, und beim Tod des Ziels. `UnitState.Stop()` lässt es unangetastet,
obwohl der Kommentar an der Anwendungsstelle „Stop cancels every standing
order" behauptet.

Daraus folgt: Wer einer **stehenden** Einheit ein Ziel gibt, nimmt sie
dauerhaft aus der Automatik. Läuft das Ziel aus der Reichweite, hält die
Einheit den Befehl (`CombatSystem` Phase 3 hält, statt zu verwerfen) und
**feuert nicht mehr** — die Automatik kann nicht einspringen, weil das Feld
belegt ist. Die Automatik ist reaktiv und wird nie schal; ein Befehl wird es
mit jedem Tick. Solange die Einheit auf ihr Ziel **zuläuft**, ist der Befehl
besser; sobald sie steht, ist er schlechter.

Genau deshalb bleibt das Zielen oberhalb der Schwelle unverändert richtig: dort
marschiert die Armee auf ihr Ziel zu.

### Widerlegt

> **Der HQ-Kurzschluss ist nicht die Ursache.** (a) 10.847 gegen (a′) 10.855 —
> Rauschen. Wer die Regel wieder aufgreift, braucht einen anderen Ansatz.

> **Feuerverteilung erklärt den grössten Teil.** Ein gemeinsames Ziel (b) holt
> gegenüber „jede Einheit ihr eigenes" (a) 1.183 Ticks und 13 / 15 Verluste
> zurück. Konzentration bleibt richtig, auch stehend.

> **„Möglichst wenig eingreifen" ist hier falsch.** Fassung (c) greift am
> seltensten ein und liegt trotzdem schlechter als (b) — weil jedes einzelne
> Eingreifen eine dauerhafte Sperre erzeugt, unabhängig davon, wie selten es
> passiert. Die Zahl der Sperren zählt, nicht die Zahl der Befehle.

### Unverändert

- **Determinismus:** jede Fassung zweimal gefahren, Exit 0.
- `intentsRejected` bleibt **0** in allen Fassungen.
- Duell-Arena und Bewegungsszenarien byte-identisch (sie fahren kein KI-System).
- **87/87 Labortests, 559/559 SimRunner-Tests grün** — im Endzustand wie in
  jeder Zwischenfassung.

### Einschränkung, die zum Befund gehört

Gemessen wurde **symmetrisch KI gegen KI**. Beide Slots zielen schlechter oder
besser gleichzeitig; was eine bessere Zielwahl gegen einen Menschen wert ist,
kann dieses Labor nicht zeigen — dieselbe Einschränkung wie bei V002.

### Offen — und es ist eine Anfrage, keine Umsetzung

Damit „zielen, ohne zu marschieren" überhaupt gewinnen kann, braucht es einen
Weg, ein Ziel wieder **freizugeben**. Der naheliegende Kandidat ist `Stop`,
das laut eigenem Kommentar bereits jeden stehenden Befehl löscht, `AttackTarget`
aber auslässt. Das liegt in `Simulation/State/` — **fremdes Terrain, gesperrt.**
Befund abgelegt unter [`findings/F001-stop-loescht-attacktarget-nicht.md`](../findings/F001-stop-loescht-attacktarget-nicht.md);
der Weg nach draussen ist Mail oder Issue, kein PR.

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
