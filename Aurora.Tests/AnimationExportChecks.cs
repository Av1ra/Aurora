using Aurora.Core;
using Aurora.Editor;
using System.Text;
using System.Text.Json;
static class AnimationExportChecks
{
 public static async Task Run(Action<bool,string> check,Action<Action,string> reject)
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-animation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var model=ObjImporter.Parse("o body\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl paint\nf 1 2 3\n").Model;
   var clip=new AnimationClip("Work",25,100,[new("body",[0,0,0],[new(0,AnimationPose.Identity),new(100,new([0,1,0],[90,0,0],[1,1,1]))])],"WORKING_ANIM");
   byte[] pixel=DdsImage.Encode(new(1,1,[180,180,180,255]));
   var doc=new EditorProject(1,[new("main.nmf","models/main.nmf",[])],[new("material","native/material.mtl",new(){{"paint",new(Base:"assets/paint.dds")}})],0,0,null,null,"$TYPE VEHICLETYPE_ROAD\n$SKILL_BULLDOZER 1\n$END\n",[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],Animations:[clip],MechanismRotations:new(){{"MIXER_DRUM/0",[0,45,0]}});
   var entries=new Dictionary<string,byte[]>{{"document.json",JsonSerializer.SerializeToUtf8Bytes(doc)},{"models/main.nmf",model.Data},{"native/material.mtl",Encoding.UTF8.GetBytes(NativeMaterialText.Create(["paint"],"../assets/paint.dds","../assets/paint.dds","../assets/paint.dds"))},{"assets/paint.dds",pixel},{"models/main.naf",AnimatedNmf.Naf(clip)}};
   string project=Path.Combine(root,"test.aur");AuroraProjectArchive.Write(project,entries);var restored=ProjectData.Load(AuroraProjectArchive.Read(project),p=>Path.Combine(root,"restored",p));
   check(restored.Document.Animations![0].Tracks[0].Sample(50).Rotation[0]==45&&restored.Document.MechanismRotations!["MIXER_DRUM/0"][1]==45,"encrypted project restores keyframes, action binding and editable mechanism axis orientation");
   var maps=MaterialDocuments.Native(restored.Variants[0].NativePath);check(VehicleAssets.AnimationData(restored.Models.Keys.First())!.SequenceEqual(AnimatedNmf.Naf(clip)),"implicit main-model NAF survives project extraction and canonical filename changes");string destination=Path.Combine(root,"export");Directory.CreateDirectory(destination);
   await ModelExport.Export(restored.Models,restored.Glass,maps,restored.Variants[0].NativePath,null,null,destination,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))),VehicleScript.Set(restored.Vehicle!,"WORKING_ANIM",0,"missing.nmf 0 0"),useSourceScript:false,animations:restored.Document.Animations);
   string output=Directory.GetDirectories(destination).Single();string script=File.ReadAllText(Path.Combine(output,"script.ini"));string reference=VehicleScript.Tokens(script).First(t=>t.Key=="WORKING_ANIM").Value;
   check(File.Exists(Path.Combine(output,reference))&&File.Exists(Path.Combine(output,Path.ChangeExtension(reference,".naf")))&&BitConverter.ToInt32(File.ReadAllBytes(Path.Combine(output,Path.ChangeExtension(reference,".naf"))),16)==25,"work action export writes a paired animated NMF/NAF with selected FPS and replaces stale action references");
   var packed=new Dictionary<string,byte[]>();string packedScript=VehicleAssets.Pack(script,output,packed);string unpacked=Path.Combine(root,"unpacked");Directory.CreateDirectory(unpacked);foreach(var entry in packed){string file=Path.Combine(unpacked,entry.Key);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllBytes(file,entry.Value);}string portable=Path.Combine(root,"portable");Directory.CreateDirectory(portable);string portableScript=VehicleAssets.Export(packedScript,unpacked,portable);string portableModel=VehicleScript.Tokens(portableScript).First(t=>t.Key=="WORKING_ANIM").Value;
   check(File.Exists(Path.Combine(portable,portableModel))&&File.ReadAllBytes(Path.Combine(portable,Path.ChangeExtension(portableModel,".naf"))).SequenceEqual(AnimatedNmf.Naf(clip)),"project dependencies and transport export retain the exact NAF next to its renamed NMF");
   check(!AnimationBindings.AppliesToScript("GARBAGE_LOADING_ANIM","$TYPE VEHICLETYPE_ROAD\n$RESOURCE_TRANSPORT_TYPE RESOURCE_TRANSPORT_PASSENGERS\n$END\n")&&AnimationBindings.AppliesToScript("GARBAGE_LOADING_ANIM","$TYPE VEHICLETYPE_ROAD\n$SKILL_WASTE_CONTAINER_BIG 1\n$END\n"),"garbage animation choices follow the configured transport role instead of appearing on every road vehicle");
   var parts=ObjImporter.Parse("o body\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl paint\nf 1 2 3\no wheels\nv 0 0 1\nv 1 0 1\nv 0 1 1\nf 4 5 6\n").Model;
   var gear=clip with{Name="Gear",Action="LANDING_GEAR_ANIM",Tracks=[clip.Tracks[0] with{Mesh="wheels",CargoParent="wheels"}]};
   string plane=VehicleScript.SetCargo("$TYPE VEHICLETYPE_AIRPLANE\n$END\n",new(new(0,0,0),new(0,1,0),new(1,1,1),new(0,2,0),new(1,2,1)));string bound=AnimationBindings.Write(plane,parts,[gear],portable);
   var attachment=new NativeModel(File.ReadAllBytes(Path.Combine(portable,"animation_landing_gear_anim.nmf")));var rest=AnimationBindings.StaticModel(parts,[gear]);check(attachment.Nodes.Select(n=>n.Name).SequenceEqual(new[]{"wheels"})&&rest.Nodes.Select(n=>n.Name).SequenceEqual(new[]{"body"}),"attached actions export only moving components and leave the static vehicle without duplicate geometry");
   check(VehicleScript.Fields(bound).Single(f=>f.Key=="RESOURCE_VISUALIZATION_BONE").Value==AnimatedNmf.Bone("wheels")&&!VehicleScript.Fields(bound).Any(f=>f.Key=="RESOURCE_VISUALIZATION"),"cargo follows the exported stable bone through the native cargo binding");
   var reordered=gear with{Tracks=[new("body",[0,0,0],[new(0,AnimationPose.Identity)]),gear.Tracks[0]]};byte[] reorderedNaf=AnimatedNmf.Naf(reordered);check(Encoding.UTF8.GetString(reorderedNaf,32+64+(reordered.LastFrame+1)*64,64).TrimEnd('\0')==AnimatedNmf.Bone("wheels"),"bone identifiers remain stable when a different action uses a different track order");
   reject(()=>AnimationBindings.StaticModel(parts,[reordered]),"attached animation cannot silently remove the entire static vehicle");
   reject(()=>AnimationBindings.Write("$TYPE VEHICLETYPE_SHIP\n$END\n",model,[clip],portable),"road work animation cannot be assigned to an incompatible transport type");
   reject(()=>AnimationBindings.Write(doc.Vehicle!,model,[clip,clip with{Name="duplicate"}],portable),"two clips cannot silently overwrite the same game action");
  }finally{Directory.Delete(root,true);}
 }
}
