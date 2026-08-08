#!/usr/bin/env python3
"""Baut die Gesamtauswertung eines Laborlaufs: out/lab/dashboard.html.

Liest die Artefakte, die `match`, `duel`, `movement` und `compare` geschrieben
haben, verdichtet sie zu einem kompakten JSON-Block und bettet den in
`dashboard.tpl.html` ein. Ergebnis ist eine selbststaendige Seite ohne Build,
ohne Server und ohne Netzzugriff.

    python3 tools/Nova.AiLab/report/build_dashboard.py out/lab

WERKZEUG, KEIN BEITRAG. Die Seite ist Diagnose; was nicht im laufenden Spiel
gesehen wurde, steht als ungesehen im PR-Text. Der Leser wird oben auf der
Seite genau daran erinnert.

Es wird bewusst nichts gerechnet, was die Laufarten nicht schon gemessen haben:
umsortieren, summieren, ausduennen — keine abgeleiteten Kennzahlen und vor allem
keine Gesamtnote (Plan 3.6: das Labor rankt nicht, es legt nebeneinander).
"""

import json
import os
import sys

# Metriken je Slot, die das Dashboard als Kurve zeichnet. Alles Ganzzahlen —
# aus der Simulation kommt kein Float, und hier entsteht auch keiner.
TRACE_KEYS = [
    'credits', 'powerProvided', 'powerRequired', 'harvesters', 'armySize',
    'armyHealthSum', 'unitsLost', 'visibleEnemyUnits', 'visibleEnemyBuildings',
    'intentsSubmitted', 'intentsRejected', 'queuedUnits', 'sitesOpen',
]


def read_ndjson(path):
    with open(path, encoding='utf-8') as handle:
        return [json.loads(line) for line in handle if line.strip()]


def short(faction, role):
    """Fraktion + Rolle als ein Bezeichner. Jede Einheiten-Zahl ist
    fraktionsgebunden (Plan 3.9) — eine Zeile ohne Fraktion mittelt zwei
    verschiedene Waffen."""
    return faction[:3] + '.' + role


def collect_match(root):
    result = json.load(open(os.path.join(root, 'match', 'result.json'), encoding='utf-8'))
    samples = read_ndjson(os.path.join(root, 'match', 'trace.ndjson'))
    slots = []
    for index in range(result['slotCount']):
        series = {key: [s['slots'][index][key] for s in samples] for key in TRACE_KEYS}
        series['buildings'] = [sum(s['slots'][index]['buildingsByRole']) for s in samples]
        slots.append(series)
    return {'result': result, 'trace': {'ticks': [s['tick'] for s in samples], 'slots': slots}}


def collect_duels(root):
    duels = read_ndjson(os.path.join(root, 'duel', 'duels.ndjson'))

    units, cells = [], {}
    for duel in duels:
        if duel['siege']:
            continue
        attacker = short(duel['factionA'], duel['roleA'])
        defender = short(duel['factionB'], duel['roleB'])
        for name in (attacker, defender):
            if name not in units:
                units.append(name)
        cell = cells.setdefault((attacker, defender),
                                {'n': 0, 'w': 0, 'l': 0, 'u': 0, 'nc': 0, 'wob': 0, 'ranges': {}})
        cell['n'] += 1
        cell['w'] += duel['winner'] == 0
        cell['l'] += duel['winner'] == 1
        cell['u'] += duel['winner'] < 0
        cell['nc'] += duel['noContact']
        cell['wob'] += duel['parityWobbles']
        cell['ranges'][duel['range']] = {
            'winner': duel['winner'], 'tick': duel['decidedTick'],
            'survA': duel['survivorsA'], 'survB': duel['survivorsB'],
            'noContact': duel['noContact'],
        }
    units.sort()

    siege = [{
        'a': short(d['factionA'], d['roleA']), 'b': short(d['factionB'], d['roleB']),
        'range': d['range'], 'decided': d['decided'],
        'tick': d['decidedTick'] if d['decided'] else None,
        'countA': d['countA'], 'spentA': d['spentA'], 'survA': d['survivorsA'],
        'winner': d['winner'],
    } for d in duels if d['siege']]

    table = {
        'units': units,
        'cells': [dict(a=a, b=b, **v) for (a, b), v in cells.items()],
        'budget': duels[0]['budgetAE'],
        'counts': {
            'total': len(duels),
            'decided': sum(1 for d in duels if d['decided']),
            'timeout': sum(1 for d in duels if not d['decided']),
            'noContact': sum(1 for d in duels if d['noContact']),
            # Wackelnde Paritaet wird je Paarung gezaehlt, nicht je Duell:
            # sonst zaehlt dieselbe schiefe Paarung dreimal (ein Abstand je Lauf).
            'wobble': len({(d['factionA'], d['roleA'], d['factionB'], d['roleB'])
                           for d in duels if d['parityWobbles']}),
        },
    }
    return table, siege


def build(root, out_path=None):
    duel, siege = collect_duels(root)
    data = {
        'match': collect_match(root),
        'compare': json.load(open(os.path.join(root, 'compare', 'resultset.json'), encoding='utf-8')),
        'movement': read_ndjson(os.path.join(root, 'movement', 'movement.ndjson')),
        'duel': duel,
        'siege': siege,
    }

    template_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'dashboard.tpl.html')
    with open(template_path, encoding='utf-8') as handle:
        template = handle.read()
    if '__DATA__' not in template:
        raise SystemExit(f'{template_path}: Platzhalter __DATA__ fehlt')

    # `</script>` im Datenblock wuerde den umgebenden Script-Tag schliessen.
    payload = json.dumps(data, separators=(',', ':')).replace('</script>', r'<\/script>')
    page = template.replace('__DATA__', payload)

    out_path = out_path or os.path.join(root, 'dashboard.html')
    with open(out_path, 'w', encoding='utf-8') as handle:
        handle.write(page)
    return out_path, len(page)


if __name__ == '__main__':
    directory = sys.argv[1] if len(sys.argv) > 1 else 'out/lab'
    written, size = build(directory)
    print(f'{written} — {size // 1024} KiB')
