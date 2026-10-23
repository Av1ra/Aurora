namespace Aurora.Editor;
enum EditorToolKind{None,Mesh,Animation,Lights,Cargo,Smoke,Mechanism,Camera}
sealed record EditorToolContext(EditorToolKind Kind=EditorToolKind.None,bool MeshAxis=false,bool AnimationAxis=false,int Cargo=-1,int Smoke=-1,int Lamp=-1,TransportPoint? Point=null);
sealed partial class EditorWindow
{
 EditorToolContext toolContext=new();
 void ToolMode(EditorToolKind kind,bool enabled){if(enabled)toolContext=toolContext with{Kind=kind};else if(toolContext.Kind==kind)toolContext=toolContext with{Kind=EditorToolKind.None};}
 bool meshEditing{get=>toolContext.Kind==EditorToolKind.Mesh;set=>ToolMode(EditorToolKind.Mesh,value);}
 bool animationEditing{get=>toolContext.Kind==EditorToolKind.Animation;set=>ToolMode(EditorToolKind.Animation,value);}
 bool lightEditing{get=>toolContext.Kind==EditorToolKind.Lights;set=>ToolMode(EditorToolKind.Lights,value);}
 bool cameraEditing{get=>toolContext.Kind==EditorToolKind.Camera;set=>ToolMode(EditorToolKind.Camera,value);}
 bool meshPivotEditing{get=>meshEditing&&toolContext.MeshAxis;set=>toolContext=toolContext with{MeshAxis=value};}
 bool animationAxisEditing{get=>animationEditing&&toolContext.AnimationAxis;set=>toolContext=toolContext with{AnimationAxis=value};}
 int activeCargo{get=>toolContext.Kind==EditorToolKind.Cargo?toolContext.Cargo:-1;set{toolContext=toolContext with{Cargo=value};if(value>=0)ToolMode(EditorToolKind.Cargo,true);else ToolMode(EditorToolKind.Cargo,false);}}
 int activeSmoke{get=>toolContext.Kind==EditorToolKind.Smoke?toolContext.Smoke:-1;set{toolContext=toolContext with{Smoke=value};if(value>=0)ToolMode(EditorToolKind.Smoke,true);else ToolMode(EditorToolKind.Smoke,false);}}
 int activeLamp{get=>lightEditing?toolContext.Lamp:-1;set{toolContext=toolContext with{Lamp=value};if(value>=0)ToolMode(EditorToolKind.Lights,true);}}
 TransportPoint? transportPoint{get=>toolContext.Kind is EditorToolKind.Mechanism or EditorToolKind.Lights?toolContext.Point:null;set{toolContext=toolContext with{Point=value};if(value!=null&&!lightEditing)ToolMode(EditorToolKind.Mechanism,true);else if(value==null)ToolMode(EditorToolKind.Mechanism,false);}}
}
