"""Summarize actual Unity NUnit results without inferring results from log text."""
from pathlib import Path
import xml.etree.ElementTree as ET
import json
root=Path('D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020')
data={}
for p in root.glob('*.xml'):
 try:r=ET.parse(p).getroot()
 except ET.ParseError:continue
 cases=[dict(c.attrib, failure=c.findtext('failure/message'), stack=c.findtext('failure/stack-trace')) for c in r.iter('test-case')]
 data[p.stem]={'summary':dict(r.attrib),'cases':cases}
(root/'test_results.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
for name,row in data.items():
 s=row['summary'];print(name,{k:s.get(k) for k in ('result','total','passed','failed','skipped','duration')})
 for c in row['cases']:
  if c['result']=='Failed':print(c['fullname'],c['failure'])
