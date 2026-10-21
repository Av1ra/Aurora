using Aurora.Core;
static class TransportPackageChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject)
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-transport-package-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string head=Path.Combine(root,"head"),wagon=Path.Combine(root,"wagon"),output=Path.Combine(root,"output");foreach(string folder in new[]{head,wagon,output})Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(root,"paint.dds"),DdsImage.Encode(new(1,1,[20,40,60,255])));
   foreach(string folder in new[]{head,wagon}){File.WriteAllText(Path.Combine(folder,"main.nmf"),"rigid model fixture");File.WriteAllText(Path.Combine(folder,"lod.nmf"),"lod fixture");File.WriteAllText(Path.Combine(folder,"material.mtl"),"$SUBMATERIAL Paint\n$TEXTURE_MTL 0 ../paint.dds\n$TEXTURE_MTL 3 ../paint.dds\n$END\n");}
   // The root represents a portable export, with local texture references.
   File.Copy(Path.Combine(root,"paint.dds"),Path.Combine(head,"paint.dds"));File.WriteAllText(Path.Combine(head,"material.mtl"),"$SUBMATERIAL Paint\n$TEXTURE_MTL 0 paint.dds\n$END\n");
   File.WriteAllText(Path.Combine(head,"script.ini"),"$TYPE VEHICLETYPE_RAIL_LOCOMOTIVE\n$TRAINSET wagon_native\n$TRAINSET_REVERSE head_native\n$TRAINSET wagon_native\n$FAMILY base_native\n$LOD1 lod.nmf\n$END\n");File.WriteAllText(Path.Combine(wagon,"script.ini"),"$TYPE VEHICLETYPE_RAIL_VAGON\n$FAMILY head_native\n$PURCHASE_EXCLUDE\n$LOD1 lod.nmf\n$END\n");var objects=new Dictionary<string,string>{{"head_native",Path.Combine(head,"script.ini")},{"wagon_native",Path.Combine(wagon,"script.ini")}};
   string package=TransportPackage.Write(head,"head_native","123456789","head",objects,output);string primary=File.ReadAllText(Path.Combine(package,"head","script.ini"));string secondary=File.ReadAllText(Path.Combine(package,"object_1","script.ini"));check(primary.Contains("$TRAINSET 123456789/object_1")&&primary.Contains("$TRAINSET_REVERSE 123456789/head")&&secondary.Contains("$FAMILY 123456789/head")&&primary.Contains("$FAMILY base_native"),"transport package resolves cyclic native family/trainset references, repeats and reversed head with Workshop ID/object paths");check(File.ReadAllLines(Path.Combine(package,"workshopscript.ini")).Length==2&&File.ReadAllText(Path.Combine(package,"aurora-transport.json")).Contains("base_native"),"transport package registers every object and records remaining external targets");
   Directory.Delete(head,true);Directory.Delete(wagon,true);File.Delete(Path.Combine(root,"paint.dds"));foreach(string folder in Directory.GetDirectories(package)){string script=File.ReadAllText(Path.Combine(folder,"script.ini"));foreach(var token in VehicleScript.Tokens(script).Where(t=>t.Key=="LOD1"&&t.Index==0))check(File.Exists(Path.Combine(folder,token.Value)),"packaged LOD survives source removal");string material=Path.Combine(folder,"material.mtl");string mapped=VehicleAssets.MapMaterial(File.ReadAllText(material),r=>{check(File.Exists(MaterialFiles.Resolve(material,r)),"packaged material texture survives source removal");return r;});}
   reject(()=>TransportPackage.Write(package,null,"0","../escape",objects,output),"invalid Workshop package identity");
  }finally{Directory.Delete(root,true);}
 }
}
