<!-- Entwurf aus dem KI-Labor. Enthält AUSSCHLIESSLICH Gemessenes.
     Der Abschnitt "Im laufenden Spiel gesehen" ist absichtlich leer und
     muss von Hand gefüllt werden — oder ausdrücklich leer bleiben. -->

## Was & Warum

<!-- 1-3 Sätze, von Hand. Das Labor schreibt hier nichts hinein: warum eine
     Änderung richtig ist, ist eine Begründung und keine Messung. -->

## Gemessen

Kandidat `late-push` — geändert gegenüber `ms1-canonical`: armySize 12→20, squadThreshold 6→12.

| Kennzahl | ms1-canonical | late-push |
|---|---:|---:|
| Siegrate | 50% | 50% |
| Partien (S/N/U) | 1/1/0 | 1/1/0 |
| Entscheidungstick (Mittel) | 6223 | 8734 |
| Credits am Ende (Mittel) | 17730 | 23270 |
| Armeegröße am Ende (Mittel) | 10 | 9 |
| Verlorene Einheiten (Mittel) | 41 | 84 |
| Intents gesendet | 313 | 788 |
| Intents abgelehnt | 0 | 0 |

### Spielgefühl

| Kennzahl | ms1-canonical | late-push |
|---|---:|---:|
| Austauschverhältnis (Feindverluste je 100 eigene) | 105 | 104 |
| Gefechtsintervalle (mit Verlusten) | 11 | 21 |
| Grösster Verlustsprung in einem Intervall | 9 | 9 |
| Reaktionslatenz (Ticks Schaden → neuer Marschbefehl) | 26 | 20 |
| Unbeantworteter Schaden (Ereignisse) | 39 | 81 |
| Aktionen pro Minute | 14 | 26 |
| Verschiedene Partieausgänge über die Menge | 1 | 2 |

`-1` heisst "in dieser Menge nicht messbar" (keine eigenen Verluste bzw. keine einzige Reaktion), nicht `0`.
Das Austauschverhältnis ist **nur einseitig** aussagekräftig — jeder Kandidat hier spielt gegen die Referenz, genau diese Anordnung.

Bedingungen des Laufs — ohne sie ist keine Zahl oben reproduzierbar:

- Spec-Version 1, Profil-Schema 1
- Tickbudget 27000, 2 Slots, jeder Kandidat in **beiden** Fraktionsrollen
- `ComputeDefinitionsHash64()` = 0x6326FA3E56CFF5A3
- Commit 0b0c211c55a16e0fbe20c420337a0b8e5ad2d754
- Seeds: `0x6656D5210FB2CE85`

## Im laufenden Spiel gesehen

<!-- LEER GELASSEN — und zwar absichtlich.

     Das Labor kann diesen Abschnitt nicht füllen. Ein Laborlauf ist Diagnose,
     kein Nachweis: hier gehört hinein, was in einer echten Partie zu sehen war,
     einschließlich eines Falls, in dem das Verhalten falsch war, mit Einschätzung
     warum das akzeptabel ist.

     Wenn nicht gespielt wurde, bleibt genau das hier stehen:
     "Nicht im laufenden Spiel geprüft." Der PR ist dann unfertig und sagt es. -->

## Baselines

Eine Verhaltensänderung macht diese vier Dateien rot. **Das ist ihr Zweck, kein Defekt** —
dieser PR ändert sie nicht:

- `tools/Nova.SimRunner.Tests/SnapshotGoldenBytesTests.cs`
- `tools/Nova.SimRunner.Tests/CommandGoldenBytesTests.cs`
- `tools/Nova.SimRunner.Tests/SimRandomGoldenTests.cs`
- `tools/Nova.SimRunner.Tests/Determinism10000Tests.cs`

Die neue Baseline kommt in einen **eigenen PR** mit altem Wert, neuem Wert und
Begründung. Ein PR, der Verhalten ändert und im selben Zug eine Baseline neu setzt,
wird nicht gemergt.

## Checkliste

- [ ] `dotnet test tools/Nova.SimRunner.Tests` lokal gelaufen — Ergebnis eintragen
- [ ] Zeile unter `[Unreleased]` in CHANGELOG.md
- [ ] Beobachtungsabschnitt oben gefüllt **oder** ausdrücklich als ungespielt markiert
