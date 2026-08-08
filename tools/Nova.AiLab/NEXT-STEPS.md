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

## 2 · Der Angriff läuft immer dieselbe Linie

**Was der Spieler sieht.** Nach zwei Partien weiss man, wo die KI langkommt, und
stellt sich hin. Sie ändert daran nichts, nie.

**Warum.** `GetEnemyStartAreaCell` liefert **das entfernteste Aetherium-Feld** —
einen einzigen festen Punkt. Kein Umweg, kein zweites Ziel, keine Flanke. Die
Annahme dahinter („beim entferntesten Feld steht die Feindbasis") ist zudem
schon bei vier Slots falsch: Die 4-Slot-Partie endet deshalb im
Zeitlimit-Unentschieden.

**Was zu bauen ist — in dieser Reihenfolge:**

1. **Erst ein zweites lohnendes Ziel**, nicht gleich Flankenrouten. Harvester
   und Refinery des Gegners sind weich, wichtig und stehen abseits. Das
   Score-Targeting aus V001 kann das schon bewerten — es sieht diese Ziele nur
   nie, weil die Armee an ihnen vorbeiläuft.
2. **Dann die Annäherung.** `Simulation/Pathfinding/` gehört uns seit v1.1.0
   (Flow-Field und `CostField` inbegriffen, unter der `IsWalkable`-Auflage) —
   eine Route, die nicht die Luftlinie ist, ist damit im eigenen Scope machbar.

**Wenn es wirkt:** Der Spieler kann sich nicht mehr an eine Stelle stellen. Das
merkt man sofort und in keiner Kennzahl.

**Scope:** `AI/` uns, `Pathfinding/` uns (13–15). Die `IsWalkable`-Semantik
selbst wird **nicht** angefasst.

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

## 6 · Die KI kann ihre Artillerie nicht benutzen

**Was der Spieler sieht.** Artillerie mit 20 Zellen Reichweite läuft auf
Tuchfühlung heran und stirbt an Infanterie.

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
entscheidet eine gespielte Partie — und die steht bis heute aus, weil der
Linux-Build Bringschuld des Netzstrangs ist. Bis dahin gilt jede Zeile hier als
ungespielt, und genau so gehört sie in einen PR-Text.

Drei Dinge, die man beim Spielen anschauen sollte, sobald es geht:

1. **Kommt die Armee als Welle oder als Kette?** (Punkt 1)
2. **Drehen angeschlagene Einheiten ab?** (Punkt 3)
3. **Läuft der Angriff zweimal hintereinander denselben Weg?** (Punkt 2)

Alle drei sieht man in einer Partie, ohne eine Zahl zu lesen. Das ist der
Massstab, den diese Liste meint.
