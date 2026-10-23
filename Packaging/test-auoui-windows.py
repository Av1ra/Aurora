"""Exercise only test-owned offscreen native windows, never desktop input/focus."""
import json,pathlib,subprocess,sys,tempfile
app=pathlib.Path(sys.argv[1]).resolve()
with tempfile.TemporaryDirectory(prefix='Aurora-AuoUI-') as temp:
 report=pathlib.Path(temp)/'result.json'
 result=subprocess.run([str(app),'--editor-auoui-check',str(report)],timeout=40,creationflags=0x08000000)
 assert result.returncode==0, f'AuoUI check exited {result.returncode}'
 assert report.exists(), 'AuoUI check did not report a result'
 data=json.loads(report.read_text(encoding='utf-8-sig'))
 assert data.get('Success'),data.get('Error',data)
 metrics=data['Metrics']
 assert metrics['Reopened'] and metrics['ToolWindowReused'] and not metrics['WindowActivated'],metrics
 assert metrics['TallFormScrolling'] and metrics['PrimaryValidationCancelledThenAccepted'],metrics
 assert all(s['ScrollableHeight']==0 for s in metrics['Sizes']),metrics
 print('PASS real AuoUI templates, separate window reopen/cleanup, scrolling and native primary validation without activation')
