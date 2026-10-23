"""Measure real parameter form construction/Measure, without activating a window."""
import json,pathlib,subprocess,sys,tempfile
app=pathlib.Path(sys.argv[1]).resolve()
media=pathlib.Path(sys.argv[2]).resolve()
with tempfile.TemporaryDirectory(prefix='Aurora-parameters-') as temp:
    report=pathlib.Path(temp)/'parameters.json'
    result=subprocess.run([str(app),'--editor-parameters-check',str(media),str(report)],timeout=60,creationflags=0x08000000)
    assert result.returncode==0,result.returncode
    data=json.loads(report.read_text(encoding='utf-8-sig'))
    assert data.get('Success'),data
    metrics=data['Metrics']
    assert not metrics['WindowActivated'] and len(metrics['Samples'])==3
    assert all(s['Controls']>=20 for s in metrics['Samples'])
    print(json.dumps(data,ensure_ascii=False))
