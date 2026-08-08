# Nächste Schritte am KI-Verhalten — sortiert danach, was ein Spieler merkt

**Stand:** KI-Verhalten `r2.A037B84D` · Commit `732ea98` ·
Messgrundlage: [`reports/latest.md`](reports/latest.md) ·
Historie: [`reports/behavior-log.md`](reports/behavior-log.md)

Diese Liste ist **nicht** nach Aufwand oder nach Laborkennzahl sortiert, sondern
danach, was in einer Partie *Mensch gegen KI* auffällt. Eine Verbesserung, die
nur eine Zahl in `latest.md` verschiebt, gehört ans Ende; eine, die man in der
ersten Minute sieht, an den Anfang.

> [!IMPORTANT]
> Jeder Punkt hier ist **verhaltensändernd**. Das heisst jedes Mal: Referenz
> vorher sichern, Determinismus zuerst prüfen (Exit 2 = sofort stoppen),
> `AiBehaviorId.Revision` bumpen, Journaleintrag mit **besser und schlechter**
> schreiben. Der Ablauf steht in [`AGENTS.md`](AGENTS.md) §3.

---

## 0 · Eine Ursache, eine Bauform — vor allen sechs Punkten

Die sechs Punkte unten sind sechs Beobachtungen, aber nicht sechs Ursachen.
Schritt (6) in `SkirmishAiSystem.cs` hat genau **eine** Form: *ein* Ziel und
*eine* Marschzelle für die **ganze** Armee, das Ganze hinter einem Gate auf
`AttackSquadThreshold`. Daraus folgen fünf der sechs Symptome fast mechanisch.

| Symptom | Folgt aus |
|---|---|
| 1 · Armee tröpfelt | jede neue Einheit bekommt sofort denselben globalen Marschbefehl |
| 2 · immer dieselbe Linie | *eine* Zielzelle aus `GetEnemyStartAreaCell` |
| 3 · kein Rückzug | ein globales Ziel kann „diese eine Einheit dreht ab" nicht ausdrücken |
| 4 · kein Zielen unter sechs Einheiten | der ganze Block hängt am Schwellen-Gate |
| — · `DefendBase` (V002) | globaler Zielwechsel je Kadenz → Pendeln, +23 % Intent-Rauschen |

Wer die Punkte einzeln in diese Form hineinbaut, baut jedes Mal einen
Sonderfall an ein Konstrukt, das den Fall nicht ausdrücken kann. Deshalb steht
vor Punkt 1 eine **Formänderung ohne Verhaltensänderung**.

### Die Bauform: Absicht je Einheit statt Befehl für die Armee

Schritt (6) wird zu einer reinen Funktion in drei Stufen — weiterhin
zustandslos, weiterhin **in `SkirmishAiSystem`**: kein neues System, keine
Änderung der Tick-Reihenfolge (`AGENTS.md` §4), `MatchRunner` bleibt unberührt.

```
ArmyPosture posture = ResolvePosture(...);          // abgeleitet, nicht gespeichert
foreach (Einheit u)  UnitIntent i = ResolveUnitIntent(u, posture);
GroupAndSubmit(intents);                            // gleiche Befehle → ein Intent
```

`UnitIntent` ist ein kleiner Struct (`Kind: Attack | MoveTo | Hold`,
`targetRaw`, `cellX/cellY`). Das Gruppieren ist keine Kosmetik, sondern die
Absicherung gegen den Fehlermodus, an dem V002 gescheitert ist: **die Intents
je 1.000 Ticks dürfen nicht steigen.** Diese Zahl ist bei jedem der Schritte
unten die erste, die man anschaut — sie hat `DefendBase` gekippt, bevor die
Siegquoten überhaupt etwas sagten.

### Hysterese ohne Sidecar

Ein Sidecar-Block wäre Inhaberentscheidung und ist damit gesperrt. Er wird
nicht gebraucht: **der stehende Befehl der Einheit ist das Gedächtnis**, und er
wird ohnehin serialisiert (`TargetGridPos`, `AttackTarget`, `IsMoving`,
`CurrentHealth`, `HarvestFieldId`, `IsReturningCargo`).

- Eine Einheit **ist auf Rückzug**, wenn `TargetGridPos` die Sammel- bzw.
  Basiszelle ist **und** `CurrentHealth < exitHealthPercent`. Eintritt bei
  25 %, Austritt erst bei 60 % — echte Hysterese, kein Timer, kein neues Feld.
- Eine Einheit **wartet**, wenn ihre Zelle innerhalb `stagingToleranceCells`
  um den Sammelpunkt liegt.
- Eine **Welle ist unterwegs**, wenn mindestens eine eigene Kampfeinheit näher
  am Ziel steht als der Sammelpunkt. Abgeleitet, nicht gemerkt.

Save/Restore reproduziert das von selbst, weil nichts davon neben der Welt
liegt. Ganzzahlig, aufsteigende Scans, Gleichstand über die niedrigere rohe
Entity-Id — die Regeln aus §4 des Arbeitsvertrags bleiben unangetastet.

### Neue Profilwerte

Alle `int`, alle in `AiProfile`: `waveSize`, `stagingDistanceCells`,
`stagingToleranceCells`, `retreatEnterHealthPercent`,
`retreatExitHealthPercent`, `engageRangeSlackCells`.

Ein angehängtes Feld ändert `AiBehaviorId.ProfileHash` und damit den
angezeigten Bezeichner — das ist der Zweck des Hashes, kein Nebeneffekt.
`AiProfileTests` bekommt seine Zusicherungen im selben PR (keine der vier
Baseline-Dateien). `AiProfile.SchemaVersion` bleibt bei 1: Felder anhängen ist
keine Bedeutungsänderung.

### Drei Fallen, im Code nachgesehen

> **Ein expliziter Angriffsbefehl ausserhalb der Reichweite macht die Einheit
> passiv.** `CombatSystem` Phase 2 überspringt jede Einheit mit gültigem
> `AttackTarget`, Phase 3 *hält* einen Befehl, dessen Ziel ausser Reichweite
> oder unsichtbar ist. Wer also Punkt 4 baut („zielen ab der ersten Einheit"),
> muss explizite Ziele **auf Waffenreichweite + `engageRangeSlackCells`
> begrenzen** — sonst steht die Einheit da und feuert nicht, wo die
> Auto-Acquisition geschossen hätte. Das wäre ein Rückschritt, kein Fortschritt.

> **Der Rally-Punkt ist lesbar.** `ProductionSystem.TryGetProducer(raw, out
> entryCount, out rallyXRaw, out rallyYRaw)` gibt ihn heraus — die
> Doppelbefehl-Unterdrückung für Punkt 5 ist also zustandslos machbar. Beim
> allerersten Mal existiert die Producer-Zeile eventuell noch nicht;
> `SetRallyPoint` legt sie an, danach ist der Wert lesbar. Es entsteht kein
> dauerhaftes Rauschen. `ValidateSetRallyPoint` verlangt eigenes,
> **fertiggestelltes** Produktionsgebäude und ein Ziel auf der Karte.

> **Eigene Einheiten filtert nur die Zielwahl.** `ValidateDomain` hat für
> `AttackTarget` keinen Case, die Feuerphase prüft Reichweite und Sicht, nie
> den Besitzer. Der Filter in `FindBestVisibleEnemyByScore` ist die einzige
> Stelle, die das verhindert — er muss in jede neue Zielwahl mitwandern.

### Was zuerst passiert: PR 0, verhaltensneutral

Schritt (6) bekommt die neue Form **mit exakt den heutigen Regeln**. Der
Nachweis, dass die Umstellung sauber war, ist kein Test, sondern eine Zahl:
Entscheidungstick **8.715** und Endzustand **`0x5D8FB2D45FFD16B6`** bleiben
gleich, die Artefakte sind byte-identisch bis auf `elapsedMilliseconds`. Kein
`Revision`-Bump, kein Journaleintrag nötig — und danach ist jeder Schritt
unten klein genug, um einzeln gemessen zu werden. Genau das hat V002
überhaupt erst auswertbar gemacht.

---

## 1 · Die Armee tröpfelt einzeln in den Tod

**Was der Spieler sieht.** Kein Angriff, sondern ein Förderband: Soldat läuft
los, stirbt, nächster Soldat läuft los, stirbt. Man kann sich mit drei Einheiten
an den Weg stellen und die halbe Partie lang einen nach dem anderen abräumen.

**Warum.** Schritt (6) schickt in *jeder* Entscheidung **alle** Kampfeinheiten
zum selben Zielpunkt. Eine gerade fertiggestellte Einheit steht an der Kaserne,
bekommt 20 Ticks später denselben Marschbefehl und läuft allein über die halbe
Karte hinterher. Es gibt keinen Sammelpunkt und keine Wellen — ein Griff nach
`Regroup`, `Staging` oder `Rally` findet im ganzen System **nichts**.

**Messbar im Labor.** Verluste 70/97 bei nur 8.715 Ticks. Die Verlustkurve im
Bericht steigt in gleichmässigen kleinen Stufen statt in wenigen Sprüngen —
genau die Signatur des Nachtröpfelns statt einer Schlacht.

**Was zu bauen ist.** Ein Sammelpunkt zwischen eigener Basis und Ziel; neue
Einheiten laufen dorthin und **warten**, bis die Wellengrösse steht. Erst dann
marschiert die Welle geschlossen. Zwei neue Profilwerte (`waveSize`,
`stagingDistanceCells`), die Logik in `SkirmishAiSystem`.

**Wenn es wirkt:** dieselbe Zahl toter Einheiten, aber in weniger, grösseren
Sprüngen — und weniger Verluste je zerstörtem Gegner. Am Sichtfenster
nachprüfbar, nicht nur an der Tabelle.

**Scope:** `AI/`, `AI.Data/` — beides uns. Keine Rückfrage nötig.

---

## 2 · Sie rennen immer aufs Headquarter, immer die gerade Linie

> **Gespielt beobachtet** ([Journal B001](reports/behavior-log.md)): *„Rennen
> immer auf Headquarter … laufen einfach straight line, anstatt diese zu
> umlaufen, solange Leben sparen sich mehr rentiert als der Umweg, den sie
> nehmen müssten."*

**Was der Spieler sieht.** Nach zwei Partien weiss man, wohin die KI läuft und
auf welcher Linie. Man stellt sich hin und räumt sie ab.

**Warum — zwei Ursachen, die zufällig dasselbe Ziel ergeben.**

1. **Der HQ-Kurzschluss.** Ist das feindliche HQ sichtbar, bricht die Zielwahl
   sofort ab und liefert es zurück; die Marschzelle wird die HQ-Zelle. Das war
   eine bewusste Entscheidung aus V001 — „eine Siegbedingung ist keine
   Vorliebe" — und sie ist als Regel richtig und als Verhalten falsch.
2. **`GetEnemyStartAreaCell`** liefert das **entfernteste Aetherium-Feld**,
   wenn gar nichts sichtbar ist. Das liegt neben der Basis. Beide Wege zeigen
   also auf denselben Punkt.

Dazu: Die Annahme „beim entferntesten Feld steht die Feindbasis" ist bei vier
Slots ohnehin falsch — die 4-Slot-Partie endet deshalb im
Zeitlimit-Unentschieden.

**Was zu bauen ist — in dieser Reihenfolge:**

1. **Den Kurzschluss durch ein Gewicht ersetzen.** Hoch genug, dass ein
   freiliegendes HQ gewinnt; nicht so hoch, dass ein *verteidigtes* alles andere
   überstimmt. Ein Profilwert, `targetHqWeight`, und der `return` fällt weg.
2. **Ein zweites lohnendes Ziel zulassen.** Harvester und Refinery sind weich,
   wichtig und stehen abseits. Das Score-Targeting kann sie längst bewerten —
   es kommt nur nie dazu, weil Punkt 1 vorher abbricht.
3. **Die Annäherung nach Kosten wählen.** Die Regel steht schon in der
   Beobachtung und ist ganzzahlig rechenbar:

   > **Umweg nehmen, solange der Umweg billiger ist als die Verluste auf der
   > geraden Linie.**

   Konkret: erwarteter Schaden entlang der Luftlinie — Summe des Waffenschadens
   sichtbarer Feinde, in deren Reichweite die Linie verläuft, mal der Ticks, die
   man darin steht — gegen die Mehrkosten des Umwegs in Zellen. Kein Zufall,
   kein Gedächtnis, nur committed State. `Simulation/Pathfinding/` gehört uns
   seit v1.1.0, Flow-Field und `CostField` inbegriffen.

**Wenn es wirkt:** Der Spieler kann sich nicht mehr an eine Stelle stellen. Das
merkt man sofort und in keiner Kennzahl.

**Scope:** `AI/` uns, `Pathfinding/` uns (13–15). Die `IsWalkable`-Semantik
selbst wird **nicht** angefasst — das ist die Auflage aus v1.1.0.

---

## 3 · Wer die KI angreift, merkt nichts davon

**Was der Spieler sieht.** Man schiesst ihre Harvester ab, und sie erntet
weiter. Man beschiesst ihre Basis, und die Armee marschiert unbeirrt weiter
vorwärts. Angeschlagene Einheiten kämpfen bis zum letzten Lebenspunkt.

**Warum.** Es gibt keinen Rückzug und keine Verteidigung. `Retreat` existiert
nirgends im System, und `DefendBase` wurde gebaut, gemessen und **verworfen**
(→ Journal V002): Es kostete auf jeder Achse, weil die Armee zwischen Front und
Basis pendelte.

**Was zu bauen ist:**

- **`Retreat` zuerst, und zwar als Filter über Einheiten, nicht als Ziel.** Eine
  Einheit unter der Lebensschwelle bekommt einen Move-Intent Richtung Basis. Als
  globales Ziel würde sich die ganze Armee wegen eines angeschlagenen Spähers
  zurückziehen. Zwei Profilwerte mit Hysterese (`enterHealthPercent: 25`,
  `exitHealthPercent: 60`), damit dieselbe Einheit nicht im Wechseltakt kehrt
  macht.
- **`DefendBase` erst danach, und nur mit Hysterese.** V002 hat gezeigt: ohne
  Vorsprungsregel beim Zielwechsel ist ein zweiter Anlauf sinnlos. Am Radius
  liegt es nicht — 8, 16 und 24 wurden gemessen.

**Wenn es wirkt:** Angeschlagene Einheiten drehen ab, statt zu verpuffen. Das
ist die sichtbarste einzelne Verhaltensänderung dieser ganzen Liste.

**Scope:** `AI/`, `AI.Data/` — uns.

---

## 4 · Unter sechs Einheiten zielt die KI überhaupt nicht

**Was der Spieler sieht.** Früh im Spiel schiessen ihre Soldaten auf den
nächstbesten Gegenstand — auch auf einen unbewaffneten Harvester, während ein
Panzer daneben steht.

**Warum.** Gemessen beim Schreiben des Zielverhaltenstests: Unterhalb von
`AttackSquadThreshold` reicht die KI **keinen einzigen** `AttackTarget` ein. Was
die Einheiten tragen, ist reine D-087-Auto-Acquisition, und die nimmt das
*nächste* Ziel, nicht das gefährlichste. Vier von fünf schossen auf den
Harvester; in dem Tick, in dem die sechste Einheit fertig wurde, sprangen alle
fünf auf den Panzer.

**Was zu bauen ist.** Das Score-Targeting aus der Angriffsentscheidung
herauslösen: Ziele werden bewertet, sobald überhaupt Kampfeinheiten leben. Die
Schwelle regelt dann nur noch den *Vormarsch*, nicht das *Zielen*. Kleiner
Eingriff, weil die Bewertung schon existiert.

**Scope:** `AI/` — uns.

---

## 5 · Nachschub sammelt sich nicht

**Was der Spieler sieht.** Einheiten verlassen die Kaserne einzeln in
Richtung Front — dasselbe Bild wie Punkt 1, nur an der Quelle.

**Warum.** Die KI benutzt `SetRallyPoint` nicht. Der Grund dafür stand bis
`768da5c` als Kommentar im Code und war **falsch**: `ValidateSetRallyPoint`
prüft über `IsProducerRole` aus der Definitionstabelle, und beide Fraktionen
tragen am Harvester `producerRole: Refinery` seit D-077. Der Befehl ist sofort
nutzbar.

**Was zu bauen ist.** Rally-Punkt der Kaserne auf den Sammelpunkt aus Punkt 1
setzen. Das ist derselbe Punkt — die beiden Änderungen gehören zusammen und
sollten in dieser Reihenfolge kommen: erst der Sammelpunkt, dann der Rally-Punkt
darauf. Nebeneffekt: Das Harvester-Micromanagement kann entfallen.

**Scope:** `AI/` — uns.

---

## 6 · Abstand — die KI kennt weder ihre eigene Reichweite noch deine

> **Gespielt beobachtet** ([Journal B001](reports/behavior-log.md)): *„Halten
> keinen Abstand zu meinen Fernkampfangreifern."*

Das sind **zwei** Fehler, die gern für einen gehalten werden:

| | eigene Reichweite | Reichweite des Gegners |
|---|---|---|
| Was passiert | Artillerie läuft bis auf Abstand 0 heran | Die Armee läuft ohne zu zögern in die Reichweite gegnerischer Fernkämpfer |
| Gemessen | ja — nutzbarer Überlauf **7** von 7 | **nein**, es gibt kein Szenario dafür |
| Gehört zu | Issue 03, `Movement/` | Punkt 2, die Annäherung nach Kosten |

Das Labor kann die zweite Spalte heute nicht messen. **Das fehlende Szenario:**
eine Gruppe läuft auf einen *stehenden Fernkämpfer* zu, gemessen wird der
Schaden, den sie auf dem Weg frisst, gegen den Schaden bei einem Umweg. Das ist
ein `movement`-Szenario, kein Duell — und es ist die Vorarbeit, ohne die Punkt 2
nur behauptet statt belegt werden kann.

**Was der Spieler bei der eigenen Reichweite sieht.** Artillerie mit 20 Zellen
Reichweite läuft auf Tuchfühlung heran und stirbt an Infanterie.

**Warum.** Gemessen: nomineller Überlauf 20 (Allianz) und 18 (Legion), nutzbarer
Überlauf **7** — die Einheiten rücken über die Entfernung hinaus vor, auf der sie
zum ersten Mal getroffen haben. Dazu: In 100 von 576 Duellen fällt kein einziger
Schuss, weil die Waffenreichweite über der Sichtweite liegt (20 gegen 10).

**Was zu bauen ist.** Zwei getrennte Dinge, die gern verwechselt werden:

- **Abstandhalten** (Issue 03) in `Simulation/Movement/` — der rote Balken im
  Standoff-Diagramm, und nur der.
- **Aufklärung**, damit die Reichweite überhaupt nutzbar wird. Ein Kontrolllauf
  hat gezeigt: eine Gruppe, die auf voller Reichweite stehenbleibt, richtet über
  2.000 Ticks **null** Schaden an. „Auf Reichweite stehenbleiben" allein ist also
  keine Verbesserung, sondern eine wirkungslose Waffe.

**Scope:** `Movement/`, `Combat/`, `AI/` — uns. Neue Legion-Waffenwerte wären
`Simulation/Definitions/` und damit **Absprache**, nicht Umsetzung.

---

## Reihenfolge der PRs — je einer, je eine Verhaltensänderung

Die Reihenfolge oben sagt, was ein Spieler zuerst merkt. Diese hier sagt, in
welcher Folge man es baut, ohne zweimal dasselbe anzufassen: Zielen vor
Marschieren, Sammelpunkt vor Rally-Punkt, Rückzug erst, wenn es einen Ort
gibt, an den man sich zurückzieht.

| # | Was | Punkt | Ort | Messgrösse, an der es hängt |
|---:|---|---|---|---|
| 0 | Form: Absicht je Einheit, **verhaltensneutral** | §0 | `AI/` | Tick 8.715 und `0x5D8FB2D45FFD16B6` **unverändert** |
| 1 | Zielen unabhängig von der Schwelle, begrenzt auf Waffenreichweite | 4 | `AI/`, `AI.Data/` | Verluste in den ersten 2.000 Ticks, `armyHealthSum` |
| 2 | Sammelpunkt und Wellengrösse | 1 | `AI/`, `AI.Data/` | Verlustkurve in Sprüngen statt Stufen; Verluste je zerstörtem Gegner |
| 3 | Rally-Punkt der Kaserne auf den Sammelpunkt | 5 | `AI/` | Intents je 1.000 Ticks (sollen **sinken**), Zeit bis zum Wellenstart |
| 4 | `Retreat` als Einheitenfilter mit Hysterese | 3 | `AI/`, `AI.Data/` | `unitsLost` gegen `healthLost` |
| 5 | Zweites lohnendes Ziel (Harvester, Refinery) | 2 | `AI/` | Entscheidungstick, Ziele je Partie; 4-Slot-Lauf endet nicht mehr im Zeitlimit |
| 6 | Annäherung über eine Route statt der Luftlinie | 2 | `AI/`, `Pathfinding/` | zweimal dieselbe Partie, zwei verschiedene Wege |
| 7 | Abstandhalten **plus** Aufklärung | 6 | `Movement/`, `AI/` | `usableRangeOvershootCells`, kontaktlose Duelle (heute 100 von 576) |

Zwei Reihenfolgeregeln, die nicht verhandelbar sind: **3 nach 2**, weil der
Rally-Punkt derselbe Punkt ist wie der Sammelpunkt, und **7 als Paar**, weil
„auf Reichweite stehenbleiben" ohne Aufklärung im Kontrolllauf über 2.000
Ticks null Schaden angerichtet hat.

Bei jedem PR ab 1: Referenz sichern, Determinismus zuerst (Exit 2 = Ende),
Hash-Kette gegen die Referenz diffen (der **erste abweichende Tick** ist die
wertvollste Zahl), beide Suiten fahren, `AiBehaviorId.Revision` bumpen,
Journaleintrag mit Abschnitt „Schlechter". Der Abschnitt „Im laufenden Spiel
gesehen" bleibt leer, solange der Linux-Build aussteht.

---

## Nicht anfangen — begründet

| Vorhaben | Warum nicht |
|---|---|
| **Verteidigungsmodule bauen** (`InstallDefenseModule`) | `ValidateDomain` lehnt diesen Befehl **unbedingt** ab: G2/G4-Inhalt laut `mvp-v1.json`. Eine KI, die ihn benutzt, produziert nur `intentsRejected`. Der Plan führt ihn als Position 1 der fehlenden Befehle — der Code widerspricht. |
| **`DefendBase` erneut, mit anderem Radius** | Gemessen: 8 → 9.564, 16 → 9.470, 24 → byte-identisch zu 16. Am Radius liegt es nicht (Journal V002). |
| **Legion-Waffenwerte ändern** (Issue 01) | `Simulation/Definitions/` ist geteilte Vertragsfläche. Das Labor *misst* Issue 01, die Umsetzung braucht Absprache. |
| **Kartenvarianz** | Erst nach dem Goal-System. Vorher tunt man gegen die gebrochene `GetEnemyStartAreaCell`-Annahme statt gegen Verhalten. |
| **Ein automatischer Optimierer** | Nicht vertagt, sondern nicht vorgesehen (Entscheidung 11). Es gibt keine skalare Gütefunktion, und für „sieht im Spiel richtig aus" gibt es keine Kennzahl. |

---

## Wie man merkt, dass es wirkt

Die Laborzahlen sind **Diagnose**. Ob eine Änderung im Spiel etwas taugt,
entscheidet eine gespielte Partie — und die gibt es inzwischen: die vier
Beobachtungen in [Journal B001](reports/behavior-log.md) stammen aus einer
Partie am Rechner, nicht aus einem Lauf. Sie sind der Grund, warum die Punkte 1,
2 und 6 oben so stehen, wie sie stehen.

**Das ändert nichts an der PR-Regel.** Beobachtet wurde der *Ist-Zustand*
`r2.A037B84D`. Ob eine der Änderungen von dieser Liste im Spiel etwas taugt,
ist damit weiterhin ungesehen, und genau so gehört es in einen PR-Text. Was
fehlt, ist eine gespielte Partie **nach** der Änderung — dafür braucht es den
Linux-Build, der Bringschuld des Netzstrangs ist.

Drei Fragen, an denen sich die nächste gespielte Partie messen lässt:

1. **Kommt die Armee als Welle oder als Kette?** (Punkt 1)
2. **Läuft der Angriff zweimal hintereinander denselben Weg aufs HQ?** (Punkt 2)
3. **Drehen angeschlagene Einheiten ab?** (Punkt 3)

Alle drei sieht man, ohne eine Zahl zu lesen. Das ist der Massstab, den diese
Liste meint — und die Sorte Satz, die als einzige in den PR-Text gehört.
