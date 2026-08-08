<!-- Entwurf aus dem KI-Labor. Enthält AUSSCHLIESSLICH Gemessenes.
     Der Abschnitt "Im laufenden Spiel gesehen" ist absichtlich leer und
     muss von Hand gefüllt werden — oder ausdrücklich leer bleiben. -->

## Was & Warum

<!-- 1-3 Sätze, von Hand. Das Labor schreibt hier nichts hinein: warum eine
     Änderung richtig ist, ist eine Begründung und keine Messung. -->

## Gemessen

Kandidat `early-push` — geändert gegenüber `ms1-canonical`: armySize 12→10, squadThreshold 6→3.

| Kennzahl | ms1-canonical | early-push |
|---|---:|---:|
| Siegrate | 50% | 0% |
| Partien (S/N/U) | 1/1/0 | 0/2/0 |
| Entscheidungstick (Mittel) | 8715 | 15401 |
| Credits am Ende (Mittel) | 23405 | 41255 |
| Armeegröße am Ende (Mittel) | 5 | 3 |
| Verlorene Einheiten (Mittel) | 81 | 150 |
| Intents gesendet | 706 | 1683 |
| Intents abgelehnt | 0 | 0 |

Bedingungen des Laufs — ohne sie ist keine Zahl oben reproduzierbar:

- Spec-Version 1, Profil-Schema 1
- Tickbudget 27000, 2 Slots, jeder Kandidat in **beiden** Fraktionsrollen
- `ComputeDefinitionsHash64()` = 0x6326FA3E56CFF5A3
- Commit 206d8bc5eb3d0d03896964c7d778702aa5f1b258
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
