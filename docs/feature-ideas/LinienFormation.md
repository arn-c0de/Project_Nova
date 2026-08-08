# Linien-Formation für Bewegungsbefehle

**Version:** 0.1.0 | **Status:** Idee – nicht beschlossen, keine D-ID vergeben | **Verantwortungsbereich:** offen | **Sprint:** offen

## Zweck

Beschreibt einen Bewegungsbefehl, bei dem der Spieler durch Ziehen mit
gedrückter Maustaste eine Linie aufspannt und die ausgewählten Einheiten sich
entlang dieser Linie aufstellen. Der kurze Klick behält exakt das heutige
Verhalten: Lauf zum Punkt, Aufstellung im Haufen.

Das Dokument hält den Umsetzungsweg fest, den eine Code-Analyse am 2026-08-08
ergeben hat. Es ist eine Idee zur Diskussion, keine verbindliche Spezifikation.

## Abhängigkeiten

- [../tech/Commands.md](../tech/Commands.md) – Wire-Format und Validierungsregeln
- [../tech/SimulationCore.md](../tech/SimulationCore.md) – Determinismus, `SimFixed`, Tick
- [../tech/Pathfinding.md](../tech/Pathfinding.md) – Flow-Field und Movement
- [../tech/InputSystem.md](../tech/InputSystem.md) – Client-Intents

## 1. Ausgangslage

Der Bewegungsbefehl verteilt heute bereits Formations-Slots. Die dafür nötige
Trennung steckt schon in `UnitState`:

| Feld | Bedeutung |
| --- | --- |
| `TargetGridPos` | gemeinsames Flow-Field-Ziel der Gruppe |
| `GoalGridPos` | persönliche Ankunftszelle der Einheit – der Slot |

`UnitState.SetTarget(GridPos2D flowTarget, GridPos2D goalCell)` ist die
Überladung für Gruppenbefehle.

Die Slot-Vergabe liegt in `UnitCommandStateView.ApplyMove`. Sie verteilt
expandierende Chebyshev-Ringe um die Zielzelle, in aufsteigender `(y, x)`-
Reihenfolge, mit `FormationMaxRing = 16`. Das erzeugt die heutige
Haufen-Aufstellung.

Bereits vorhanden und wiederverwendbar:

- Insertion-Sort der Entity-IDs, gebunden durch `MaxEntityIdsPerCommand`
- Kollaps doppelter IDs – eine Einheit belegt genau eine Zelle
- Stamp-Array `_formationCellStamps` gegen Doppelbelegung, allokationsfrei
- Rückfall auf das reine Kommandoziel, wenn mehr Einheiten als belegbare
  Zellen existieren

Für die Linie wird ausschließlich der Slot-Generator getauscht. Der Rahmen
bleibt unverändert.

## 2. Wire-Format

`CommandKind` ist ein Wire-Enum. Umnummerieren oder Umsortieren bestehender
Werte ist ein Wire-Bruch und verboten. Der neue Wert wird angehängt:

```
MoveFormation = 18
```

Die Gültigkeitsprüfung in `CommandKind` prüft heute
`kind >= Move && kind <= LoadRequest` und muss die neue Obergrenze aufnehmen.

`MoveFormationPayload` entsteht analog zu `MovePayload`, trägt aber zwei
Punkte statt einem:

| Feld | Typ |
| --- | --- |
| `EntityIds` | `uint[]`, sortiert |
| `StartX`, `StartY` | `SimFixed` |
| `EndX`, `EndY` | `SimFixed` |

In `CommandPayloadValidation` sind `KindCarriesEntityList` und `TryParseRefs`
um die neue Art zu ergänzen.

## 3. Slot-Verteilung

Für `N` Einheiten auf dem Segment `A → B`:

```
slot_i = A + (B − A) · i / max(1, N − 1)
```

Verbindlich in `SimFixed`-Arithmetik. **Kein `float`, kein `Vector3`** – die
Simulation läuft deterministisch im Lockstep, Gleitkomma in der Slot-Berechnung
führt zu Desync zwischen den Clients.

Ist die Zielzelle blockiert, greift eine Suche im Umkreis des Slots über das
vorhandene Stamp-Array.

Als Flow-Ziel dient die Linienmitte, als `GoalGridPos` der jeweilige Slot. Die
Gruppe teilt sich damit ein Flow-Field und fächert erst am Ziel auf – dasselbe
Muster, das die bestehende Ring-Logik nutzt.

### Slot-Zuordnung

Die Vergabe erfolgt nicht stur nach Entity-Index, sondern nach Projektion der
aktuellen Einheitenposition auf die Linie. Sonst laufen die Einheiten über
Kreuz.

Determinismus bleibt gewahrt, solange die Projektion in `SimFixed` gerechnet
wird und Gleichstände über den Entity-Index gebrochen werden.

## 4. Eingabe

Die Stelle ist `RtsDeviceInput`, wo RMB-Down heute unmittelbar den Move
absetzt. Das Projekt nutzt dort die Legacy-`Input`-API, nicht das Input System.

Neuer Ablauf:

1. **RMB-Down** – Startpunkt und Zeitstempel merken, nichts senden.
2. **Gehalten** – überschreitet die Bewegung den Pixel-Schwellwert oder die
   Haltedauer die Zeitschwelle, wechselt die Anzeige in den Formationsmodus
   und zeigt die Vorschaulinie. Solange die Taste hält, wird nur die Vorschau
   aktualisiert.
3. **RMB-Up** – unterhalb beider Schwellen das bisherige `Move`, darüber
   `MoveFormation` mit Start- und Endpunkt.

`_dragThresholdPixels` dient als Vorbild für den Pixel-Schwellwert.

Damit ist auch das freie Ausrichten der Linie abgedeckt: Länge und Winkel
ergeben sich aus der laufenden Mausposition, gesendet wird erst beim
Loslassen.

## 5. Vorschau

Die Vorschaulinie samt Slot-Punkten ist reine Presentation über einen
`LineRenderer`. Sie erzeugt keinen Command und berührt die Simulation nicht.

## 6. Testbarkeit

Die Slot-Verteilung ist ohne Unity prüfbar und gehört in die EditMode-Tests:

- gleichmäßige Verteilung bei `N = 1, 2, 3, viele`
- `A == B` – degeneriertes Segment
- blockierte Zellen entlang der Linie
- mehr Einheiten als belegbare Zellen
- Kreuzungsfreiheit der Zuordnung
- identisches Ergebnis bei identischer Eingabe, unabhängig von der
  Reihenfolge der Entity-IDs im Payload

## 7. Abgrenzung

Nicht Teil dieser Idee:

- gespeicherte Formationen oder Formationstypen jenseits der Linie
- Beibehalten der Formation während der Fahrt
- Ausrichtung der Einheiten in Blickrichtung
- Formationen für Gebäude oder unbewegliche Einheiten

## 8. Berührte Dateien

| Datei | Sprint-12-Überschneidung |
| --- | --- |
| `Simulation/State/UnitCommandStateView.cs` | keine |
| `Simulation/CommandsV1/CommandPayloads.cs` | keine |
| `Simulation/CommandsV1/CommandKind.cs` | keine |
| `Simulation/CommandsV1/CommandPayloadValidation.cs` | keine |
| `Presentation/UI/RtsDeviceInput.cs` | +24 −3 |
| `Simulation/CommandsV1/CommandIngress.cs` | +7 −0 |

Stand `upstream/codex/feat/sprint-12-network-combat` (81cb20c) gegen
`upstream/main` (dedd3a2). Die Simulationsseite ist unberührt, nur die zwei
Presentation-Dateien überschneiden sich geringfügig. `upstream/main` ist damit
die sinnvolle Basis; ein Rebase nach dem Merge von PR #28 bleibt klein.

## 9. Offene Fragen

- Zeitschwelle und Pixelschwelle für die Modusumschaltung – Zahlenwerte müssen
  am Spiel erprobt werden.
- Verhalten bei Mischauswahl aus mobilen und unbeweglichen Einheiten.
- Soll `MoveFormation` in der Replay-Ansicht eigens dargestellt werden?
- Obergrenze der Liniendistanz, damit eine Formation nicht über die halbe
  Karte reicht.

## 10. Nächste Schritte

1. Entscheidung, ob die Idee verfolgt wird – bei Ja eine D-ID im
   [DecisionLog](../production/DecisionLog.md) vergeben.
2. Simulationsseite zuerst: Payload, `ApplyMoveFormation` und die
   EditMode-Tests aus Abschnitt 6. Damit steht das Determinismus-Fundament
   prüfbar, bevor Eingabe und Vorschau dazukommen.
3. Eingabe und Vorschaulinie nachziehen, Schwellwerte am Spiel erproben.
4. [../tech/Commands.md](../tech/Commands.md) um den neuen Wire-Typ ergänzen.
