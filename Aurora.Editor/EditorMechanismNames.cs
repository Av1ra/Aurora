using Aurora.Core;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 readonly Microsoft.UI.Xaml.Controls.InfoBar editorNotice=new(){IsOpen=false,IsClosable=true,VerticalAlignment=Microsoft.UI.Xaml.VerticalAlignment.Top,Margin=new Microsoft.UI.Xaml.Thickness(12)};
 static bool IsLightParameter(string key)=>key.StartsWith("LIGHT_",StringComparison.Ordinal)||key is "SIREN_POINT";
 static string MechanismDescription(string key)=>key switch{
  "DUMPER_DECK_PIVOT"=>"Поднимаемый кузов основной машины. Выберите его меш и разместите ось во viewport.",
  "DUMPER_JOINTDECK_PIVOT"=>"Поднимаемый кузов прицепа. Настройка выполняется в координатах прицепа.",
  "MIXER_DRUM" or "MIXER_JOINTDRUM"=>"Барабан бетономешалки. Направление вращения задаётся осью между точками A и B.",
  "CRANE_STATIC"=>"Неподвижная модель механизма. У кабины это кузов ниже подвеса.",
  "CRANE_DYNAMIC" or "CRANE_DYNAMIC2"=>"Отдельная подвижная модель механизма. У кабины — тележка у подвеса.",
  "JOINT"=>"Присоединённая модель, например прицеп или задняя секция сочленённого автобуса.",
  "ALPHA"=>"Дополнительная модель ALPHA. Её игровое назначение зависит от типа транспорта.",
  "PROPELER_STATIC"=>"Модель винта в неподвижном состоянии.",
  "PROPELER_DYNAMIC"=>"Модель винта для высоких оборотов.",
  "PROPELER_LOW"=>"Модель и материал винта для низких оборотов.",
  "PROPELER_HELICOPTER"=>"Набор моделей и материал одного винта вертолёта.",
  _=>TransportParameters.All.FirstOrDefault(p=>p.Key==key)?.Name??key
 };
}
