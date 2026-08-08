# Nova.AiLab — Berichte

> [!IMPORTANT]
> **DIAGNOSE, kein Nachweis.** Nichts in diesem Bericht wurde im laufenden Spiel gesehen.
> Alle Zahlen stammen aus headless-Läufen derselben Quelldateien, die Unity lädt — das
> macht sie vergleichbar, nicht wahr. Es gibt bewusst **keine Rangfolge**: die Werte stehen
> nebeneinander, die Auswahl trifft ein Mensch.

Dieser Ordner ist die **lesbare Fassung** der Laborläufe: [`latest.md`](latest.md) ist immer der zuletzt vermessene Lauf, `runs/` die Historie, `data/` die verdichteten Messwerte, aus denen beides jederzeit neu entsteht. Die interaktive Fassung mit Kurven, Heatmap und Scrubber bleibt [`../out/dashboard.html`](../out/dashboard.html) — sie braucht einen Browser, dieser Ordner nicht.

```bash
# messen, Bericht schreiben, Historie fortschreiben — ein Kommando
python3 tools/Nova.AiLab/report/build_reports.py tools/Nova.AiLab/out

# nur neu rendern, ohne zu messen (nach einer Formatänderung)
python3 tools/Nova.AiLab/report/build_reports.py --regenerate
```

## Zuletzt vermessen — [`20260808-1945-3b3f27d7`](latest.md)

| Was | Wert |
| --- | --: |
| gemessen am | 2026-08-08T19:45:12Z |
| Commit | `3b3f27d7` |
| Definitionstabelle | `0x6326FA3E56CFF5A3` |
| Partie entschieden bei Tick | 12.975 — Slot 0 |
| Duelle entschieden | 395 von 576, 100 ohne Kontakt |
| Überlauf `standoff` | 14 von 14 nutzbaren Zellen |
| Endzustands-Hash | `0x4947D4769384585C` |

## Historie — 1 Lauf

| Lauf | gemessen (UTC) | Commit | Sieger | entsch. Tick | Duelle entsch. | ohne Kontakt | wackelnd | Überlauf standoff | angekommen | Endzustands-Hash |
| --- | --- | --- | --- | --: | --: | --: | --: | --: | --: | --- |
| [`20260808-1945-3b3f27d7`](runs/20260808-1945-3b3f27d7.md) | 2026-08-08 19:45 | `3b3f27d7` | Slot 0 | 12.975 | 395/576 | 100 | 6 | 14/14 | 64/64 | `0x4947D4769384585C` |

---

Ein grüner Laborlauf ist Diagnose, kein Nachweis. Was nicht im laufenden Spiel gesehen wurde, steht als ungesehen im PR-Text — diese Seite ersetzt keine gespielte Beobachtung.
