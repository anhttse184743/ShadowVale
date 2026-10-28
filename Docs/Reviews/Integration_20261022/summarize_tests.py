"""Summarize the actual merged Unity run without counting reruns twice."""
from pathlib import Path
import json, xml.etree.ElementTree as ET
import sys
sys.stdout.reconfigure(encoding='utf-8')
work=Path(__file__).resolve().parent
cases={}; runs=[]
for name in ('merged-tests.xml','merge-rerun.xml','guide-rerun.xml','guide-final.xml'):
    if not (work/name).exists(): continue
    root=ET.parse(work/name).getroot()
    runs.append({'file':name,**{k:root.get(k) for k in ('total','passed','failed','skipped','result','duration')}})
    for case in root.iter('test-case'):
        cases[case.get('fullname')]={'name':case.get('fullname'),'result':case.get('result'),'run':name,
            'message':case.findtext('failure/message')}
assert cases, 'No completed Unity test result found.'
report={'latestDistinctTests':len(cases),'passed':sum(c['result']=='Passed' for c in cases.values()),
        'failed':sum(c['result']=='Failed' for c in cases.values()),'runs':runs,'cases':[cases[n] for n in sorted(cases)]}
(work/'test-summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='cases'},ensure_ascii=False,indent=2))
for case in cases.values():
    if case['result']!='Passed': print(json.dumps(case,ensure_ascii=False))
