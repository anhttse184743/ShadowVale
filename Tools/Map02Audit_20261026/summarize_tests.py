import xml.etree.ElementTree as E
from pathlib import Path
import json,hashlib
root=Path(__file__).resolve().parents[2]/'Docs/Reviews/Map02_Audit_20261026'
latest={};runs=[]
for name in ['before-tests','audit-tests','retry-two','retry-performance','retry-preview']:
    path=root/(name+'.xml')
    if not path.exists():continue
    r=E.parse(path).getroot();cases=[]
    for t in r.iter('test-case'):
        row={'name':t.get('fullname'),'result':t.get('result'),'message':t.findtext('failure/message'),'source':name+'.xml'}
        cases.append(row)
        if name!='before-tests':latest[row['name']]=row
    runs.append({'file':name+'.xml','sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'total':int(r.get('total',0)),'passed':int(r.get('passed',0)),'failed':int(r.get('failed',0)),'skipped':int(r.get('skipped',0)),'cases':cases})
result={'runs':runs,'latest_per_distinct_case':list(latest.values()),'distinct_total':len(latest),'distinct_passed':sum(t['result']=='Passed' for t in latest.values()),'distinct_failed':sum(t['result']=='Failed' for t in latest.values()),'note':'Union of latest result per test across targeted reruns; not a single full clean run or a visual/FPS certification.'}
(root/'test-summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['runs','latest_per_distinct_case']},indent=2))
