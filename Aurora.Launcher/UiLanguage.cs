using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Aurora.Launcher;
static class UiLanguage
{
    static readonly string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PBR.dll","ui-language.txt");
    public static bool English {get;private set;}=Read();
    static bool Read(){try{return File.ReadAllText(path).Trim()=="en";}catch{return false;}}
    static readonly Dictionary<string,string> words=new()
    {
      ["Установлено"]="Installed",["Доступно"]="Available",["Не установлен"]="Not installed",["Установить модуль"]="Install module",["Обновить модуль"]="Update module",["Установка модуля"]="Module installation",["Обновление модуля"]="Module update",["Позже"]="Later",["Закройте игру перед установкой."]="Close the game before installing.",["Изменение угла обзора"]="Field of view",["Включить"]="Enable",["Найден в Workshop"]="Found in Workshop",["Не найден в Workshop"]="Not found in Workshop",["Отсутствуют файлы"]="Missing files",["Идентификаторы Workshop"]="Registered Workshop mods",["Игра и модули"]="Game and modules",["Приложение"]="Application",["Альфа"]="Alpha",["Консоль"]="Console",["Команды пока недоступны."]="Commands are not available yet.",["Экспериментальные функции"]="Experimental features",["Папка установки модулей"]="Module installation folder",["Папка лаунчера"]="Launcher folder",["Модели, материалы PBR и стекло. Устанавливается отдельно."]="Models, PBR materials and glass. Installed separately.",["Скачать редактор"]="Download editor",["Открыть редактор"]="Open editor",["Скачивание редактора…"]="Downloading editor…",
      ["Игра найдена"]="Game found",["Выберите папку игры в настройках"]="Choose your game folder in Settings",["Настройки игры и модулей"]="Game and module settings",["Папка игры"]="Game folder",["Папка модов"]="Mods folder",["Модули"]="Modules",["Совместимые модули не найдены."]="No compatible modules found.",["Поиск модулей…"]="Finding modules…",["Открыть в игре: ~"]="Open in game: ~",["Установлена актуальная версия 0.2"]="Current version 0.2 installed",["Устаревшая версия 0.2"]="Outdated version 0.2",["Сохранить папку"]="Save folder",["Папка сохранена."]="Folder saved.",["Угол обзора"]="Field of view",["Главная"]="Home",["Настройки"]="Settings",["Запустить игру"]="Launch game",["Мастер моделей"]="Model editor",["Папка SovietRepublic"]="SovietRepublic folder",["Выбрать папку…"]="Browse…",["Найти модули"]="Find modules",["Проверить обновления модулей"]="Check module updates",["Консоль — альфа"]="Console — alpha",["Открыть консоль"]="Open console",["Проверить обновления"]="Check for updates",["Установить обновление"]="Install update",["Установить / обновить модуль"]="Install / update module",["Изменения применяются при следующем запуске игры."]="Changes apply on the next game launch.",["Моды не найдены. Откройте настройки и выполните поиск."]="No mods found. Open Settings and search for mods.",["Выберите мод в меню слева."]="Select a mod in the left menu.",
      ["Бета-функции"]="Beta features",["Экспериментальные эффекты. Раскройте раздел, чтобы настроить их отдельно."]="Experimental effects. Expand to configure them individually.",["Инструкция"]="Guide",["Не удалось открыть инструкцию: "]="Could not open the guide: ",["Пример"]="Example",["Мелкая трава"]="Small grass",["Вариант травы из полной версии. Применяется при запуске игры."]="Grass from the full version. Applied when the game starts.",["Цветовые текстуры земли и дорог остаются исходными."]="Original terrain and road colour textures are retained",
      ["Русский"]="Русский",["English"]="English",["Графика"]="Graphics",["Эффекты"]="Effects",
      ["PBR — настройки запуска"]="PBR — launch settings",["PBR — эффекты в игре"]="PBR — in-game effects",
      ["Настройте эффекты перед запуском Soviet Republic."]="Configure effects before starting Soviet Republic.",
      ["Включить PBR"]="Enable PBR",["Общий переключатель эффектов мода"]="Master switch for mod effects",
      ["PBR автомобилей"]="Vehicle PBR",["Материалы, normal-карты, AO и отражения"]="Materials, normal maps, AO and reflections",
      ["PBR зданий и объектов"]="Building and object PBR",["Материалы и normal-карты поддерживаемых объектов"]="Materials and normal maps for supported objects",
      ["Бета"]="Beta",["PBR дорог"]="Road PBR",["Normal-карты и PBR дорожных материалов"]="Normal maps and PBR road materials",
      ["Дополнительные тени"]="Additional shadows",["Самотень автомобилей. Контактные тени требуют PBR дорог. Штатные тени сохраняются."]="Vehicle self-shadowing. Contact shadows require road PBR. Native shadows remain enabled.",
      ["Стекло"]="Glass",["Прозрачность и отражения стёкол. Требует PBR автомобилей."]="Glass transparency and reflections. Requires vehicle PBR.",
      ["Полный комплект текстур земли, дорог и растительности."]="Full terrain, road and vegetation texture set.",
      ["Дополнительно"]="Advanced",["Диагностика PBR"]="PBR diagnostics",["Расширенный сбор данных для проверки ошибок"]="Collect additional data for troubleshooting",
      ["Отмена"]="Cancel",["Сохранить"]="Save",["Сохранить и запустить"]="Save and launch",["Вкл."]="On",["Выкл."]="Off",
      ["Зеркало"]="Mirror",["Интенсивность PBR"]="PBR amount",["Металличность"]="Metallic",["Шероховатость"]="Roughness",["Затенение AO"]="AO",["Отражения"]="Reflections",["Свет окружения"]="Environment light",
      ["Полный PBR"]="Full PBR",["Карта металличности"]="Metallic map",["Карта шероховатости"]="Roughness map",["Карта AO"]="AO map",["Карта нормалей"]="Normal map",["Базовый цвет"]="Base color",
      ["Закрыть"]="Close",["F9 — показать / скрыть"]="F9 — show / hide",
      ["Не удалось применить настройку. Проверьте, что игра отвечает."]="Could not apply the setting. Check that the game is responding.",
      ["Общий PBR выключен в лаунчере. Включите его перед следующим запуском игры."]="PBR is disabled in the launcher. Enable it before the next game launch.",
      ["Не удалось сохранить настройки: "]="Could not save settings: "
    };
    public static string Text(string value){if(English)return words.GetValueOrDefault(value,value);foreach(var pair in words)if(pair.Value==value)return pair.Key;return value;}
    public static void SetEnglish(bool english){English=english;try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,English?"en":"ru");}catch{}}
    public static void Apply(DependencyObject node){ApplyNode(node,new HashSet<DependencyObject>());}
    static void ApplyNode(DependencyObject node,HashSet<DependencyObject> visited){
      if(!visited.Add(node))return;
      // Navigation content is translated by its owner. Writing presenter TextBlocks
      // would replace the template binding and pin the previous language label.
      if(node is NavigationViewItem)return;
      if(node is TextBlock text)text.Text=Text(text.Text);
      if(node is TextBox box&&box.Header is string boxHeader)box.Header=Text(boxHeader);
      if(node is ContentControl content&&content.Content is string value)content.Content=Text(value);
      if(node is Expander expander&&expander.Header is string header)expander.Header=Text(header);
      if(node is ToggleSwitch toggle){if(toggle.Header is string toggleHeader)toggle.Header=Text(toggleHeader);toggle.OnContent=Text("Вкл.");toggle.OffContent=Text("Выкл.");}
      if(node is ComboBox combo)foreach(var item in combo.Items.OfType<ComboBoxItem>())if(item.Content is string label)item.Content=Text(label);
      // Collapsed beta/advanced content still needs the selected language.
      if(node is Panel panel)foreach(var child in panel.Children)ApplyNode(child,visited);
      if(node is Border border&&border.Child is DependencyObject borderChild)ApplyNode(borderChild,visited);
      if(node is ContentControl logical&&logical.Content is DependencyObject contentChild)ApplyNode(contentChild,visited);
      for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)ApplyNode(VisualTreeHelper.GetChild(node,i),visited);
    }
    public static void Bind(ComboBox selector,FrameworkElement root,Action changed){
      selector.SelectedIndex=English?1:0;
      selector.SelectionChanged+=(_,_)=>{English=selector.SelectedIndex==1;try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,English?"en":"ru");}catch{}Apply(root);changed();};
      root.Loaded+=(_,_)=>{Apply(root);changed();};
    }
}

