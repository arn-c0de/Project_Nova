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
| Siegrate | 50% | 100% |
| Partien (S/N/U) | 1/1/0 | 2/0/0 |
| Entscheidungstick (Mittel) | 5931 | 9753 |
| Credits am Ende (Mittel) | 17100 | 27635 |
| Armeegröße am Ende (Mittel) | 10 | 18 |
| Verlorene Einheiten (Mittel) | 35 | 89 |
| Intents gesendet | 308 | 951 |
| Intents abgelehnt | 0 | 0 |

### Spielgefühl

| Kennzahl | ms1-canonical | late-push |
|---|---:|---:|
| Austauschverhältnis (Feindverluste je 100 eigene) | 123 | 155 |
| Gefechtsintervalle (mit Verlusten) | 10 | 27 |
| Grösster Verlustsprung in einem Intervall | 9 | 5 |
| Reaktionslatenz (Ticks Schaden → neuer Marschbefehl) | 116 | 102 |
| Unbeantworteter Schaden (Ereignisse) | 35 | 88 |
| Aktionen pro Minute | 15 | 27 |
| Verschiedene Partieausgänge über die Menge | 1 | 2 |

`-1` heisst "in dieser Menge nicht messbar" (keine eigenen Verluste bzw. keine einzige Reaktion), nicht `0`.
Das Austauschverhältnis ist **nur einseitig** aussagekräftig — jeder Kandidat hier spielt gegen die Referenz, genau diese Anordnung.

Bedingungen des Laufs — ohne sie ist keine Zahl oben reproduzierbar:

- Spec-Version 1, Profil-Schema 1
- Tickbudget 27000, 2 Slots, jeder Kandidat in **beiden** Fraktionsrollen
- `ComputeDefinitionsHash64()` = 0x6326FA3E56CFF5A3
- Commit 3f7f5811d00b858a1e0e56b16c80804ed39b62e8
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
