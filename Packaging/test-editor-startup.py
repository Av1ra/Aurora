"""Construct the real signed WinUI editor without activating or interacting with a window."""
import json,pathlib,subprocess,sys,tempfile
app=pathlib.Path(sys.argv[1]).resolve()
with tempfile.TemporaryDirectory(prefix='Aurora-editor-startup-') as temp:
 report=pathlib.Path(temp)/'startup.json'
 result=subprocess.run([str(app),'--editor-startup-check',str(report)],timeout=30,creationflags=0x08000000)
 assert result.returncode==0, f'Editor startup check exited {result.returncode}'
 assert report.exists(), 'Editor startup check did not report a result'
 data=json.loads(report.read_text(encoding='utf-8-sig'))
 assert data.get('Success'),data.get('Error',data)
 assert data.get('Window')=='Aurora.Editor.EditorWindow' and data.get('Toolbar'),data
 print('PASS real signed WinUI Editor construction and attached toolbar without window activation')
