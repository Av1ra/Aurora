param([Parameter(Mandatory=$true)][string]$Logo,[Parameter(Mandatory=$true)][string[]]$Executables)
$ErrorActionPreference='Stop'
if(-not ('AuroraBrandResource' -as [type])){
 Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class AuroraBrandResource {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr BeginUpdateResource(string path,bool deleteExisting);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern bool UpdateResource(IntPtr update,IntPtr type,IntPtr name,ushort language,byte[] data,uint length);
 [DllImport("kernel32.dll",SetLastError=true)]public static extern bool EndUpdateResource(IntPtr update,bool discard);
}
'@
}
$png=[IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Logo))
$buffer=[IO.MemoryStream]::new();$writer=[IO.BinaryWriter]::new($buffer)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1)
$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0)
$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$png.Length);$writer.Write([uint16]1)
$group=$buffer.ToArray();$writer.Dispose();$buffer.Dispose()
foreach($file in $Executables){
 $target=[IO.Path]::GetFullPath($file)
 # PyInstaller stores its package after the PE sections. Preserve that overlay
 # if Windows resource updating drops it; its offsets are package-relative.
 $original=[IO.File]::ReadAllBytes($target);$pe=[BitConverter]::ToInt32($original,60);$sections=[BitConverter]::ToUInt16($original,$pe+6);$table=$pe+24+[BitConverter]::ToUInt16($original,$pe+20);$rawEnd=0
 for($i=0;$i -lt $sections;$i++){$at=$table+$i*40;$end=[BitConverter]::ToUInt32($original,$at+20)+[BitConverter]::ToUInt32($original,$at+16);$rawEnd=[Math]::Max($rawEnd,$end)}
 $overlay=if($rawEnd -lt $original.Length){$original[$rawEnd..($original.Length-1)]}else{@()}
 $handle=[AuroraBrandResource]::BeginUpdateResource($target,$false)
 if($handle -eq [IntPtr]::Zero){throw "Cannot update icon: $target"}
 $complete=$false
 try{
  if(-not [AuroraBrandResource]::UpdateResource($handle,[IntPtr]3,[IntPtr]1,0,$png,$png.Length)){throw 'Cannot embed PNG icon frame'}
  if(-not [AuroraBrandResource]::UpdateResource($handle,[IntPtr]14,[IntPtr]1,0,$group,$group.Length)){throw 'Cannot embed icon group'}
  $complete=$true
 }finally{if(-not [AuroraBrandResource]::EndUpdateResource($handle,-not $complete)){throw "Cannot finalize icon: $target"}}
 if($overlay.Length){$updated=[IO.File]::ReadAllBytes($target);$present=$updated.Length -ge $overlay.Length;if($present){for($i=0;$i -lt $overlay.Length;$i++){if($updated[$updated.Length-$overlay.Length+$i] -ne $overlay[$i]){$present=$false;break}}};if(-not $present){$stream=[IO.File]::Open($target,[IO.FileMode]::Append);try{$stream.Write([byte[]]$overlay)}finally{$stream.Dispose()}}}
}
