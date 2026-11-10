#!/usr/bin/env python3
"""Run after run.sh. Copies input DLLs only to a scratch temporary directory."""
import json, os, pathlib, shutil, struct, subprocess, tempfile
root=pathlib.Path(__file__).resolve().parent
results=root/'results'
s=json.loads((results/'summary.json').read_text())
assert all(x['normalizedWholeFileEqual'] and x['methodsEqual'] for x in s)
new=pathlib.Path(s[0]['newPath']).parent
old=pathlib.Path(s[0]['oldPath']).parent
methods=json.loads((results/'Empire.dll.methods.new.json').read_text())
m=next(x for x in methods if x['body'] and len(x['body']['il'])>20)
b=bytearray((new/'Empire.dll').read_bytes())
pe=struct.unpack_from('<I',b,0x3c)[0]
n=struct.unpack_from('<H',b,pe+6)[0]
so=pe+24+struct.unpack_from('<H',b,pe+20)[0]
rva=m['RelativeVirtualAddress']
for i in range(n):
    size,va,rawsize,ptr=struct.unpack_from('<IIII',b,so+40*i+8)
    if va<=rva<va+max(size,rawsize): at=ptr+rva-va
header=1 if b[at]&3==2 else ((struct.unpack_from('<H',b,at)[0]>>12)*4)
cases=[('il-byte',at+header,1),('field-attribute-byte',s[0]['newFirstFieldMetadataOffset'],16)]
reports=[]
for label,offset,xor in cases:
    with tempfile.TemporaryDirectory(prefix='identity-selftest-',dir=root) as tmp:
        for name in ('Empire.dll','Empire.Core.dll'):shutil.copyfile(new/name,pathlib.Path(tmp)/name)
        data=bytearray(b);data[offset]^=xor;(pathlib.Path(tmp)/'Empire.dll').write_bytes(data)
        out=root/'selftest-results'/label
        proc=subprocess.run([str(root/'run.sh'),str(old),tmp,str(out)],cwd=root,capture_output=True,text=True)
        (out/'process.txt').write_text(proc.stdout+proc.stderr)
        actual=json.loads((out/'summary.json').read_text())
        assert proc.returncode==1,(label,proc.returncode,proc.stdout,proc.stderr)
        assert not actual[0]['normalizedWholeFileEqual']
        assert actual[0]['remainingUnexplainedOffsets']==[offset]
        if label=='il-byte':assert not actual[0]['methodsEqual']
        assert actual[1]['normalizedWholeFileEqual']
        reports.append({'case':label,'mutatedOffset':offset,'xor':xor,'exitCode':proc.returncode,'rejected':True,'unexplainedOffsets':actual[0]['remainingUnexplainedOffsets'],'methodsEqual':actual[0]['methodsEqual'],'coreUnchanged':True})
(root/'selftest-results'/'summary.json').write_text(json.dumps({'passed':True,'cases':reports},indent=2))
print(json.dumps(reports,indent=2))
