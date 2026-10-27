from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET
import hashlib,json
root=Path(__file__).resolve().parents[2]
source=Path('C:/Users/Admin/Downloads/ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm.docx')
out=root/'Docs/Reviews/Map02_Audit_20261026';out.mkdir(parents=True,exist_ok=True)
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
with ZipFile(source) as z: body=ET.fromstring(z.read('word/document.xml')).find('w:body',ns)
blocks=[]
for element in body:
 if element.tag.endswith('}p'):
  text=''.join(t.text or '' for t in element.findall('.//w:t',ns));kind='paragraph'
 elif element.tag.endswith('}tbl'):
  text='\n'.join(' | '.join(''.join(t.text or '' for t in cell.findall('.//w:t',ns)) for cell in row.findall('w:tc',ns)) for row in element.findall('w:tr',ns));kind='table'
 else:continue
 if text.strip():blocks.append({'id':len(blocks)+1,'type':kind,'text':text})
(out/'script-blocks.json').write_bytes((json.dumps(blocks,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
(out/'script-numbered.txt').write_bytes(('\n'.join(f"B{b['id']:03d} [{b['type']}] {b['text']}" for b in blocks)+'\n').encode('utf-8'))
(out/'source-receipt.json').write_bytes((json.dumps({'file':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'blocks':len(blocks),'bytes':source.stat().st_size},ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
print('SPEC_EXTRACTED',len(blocks))
