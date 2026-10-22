# Editor открывал Launcher вместо собственного окна

Владелец сообщил проблему после revision 06. Журнал LocalAppData/Aurora/editor-startup-error.txt показал ArgumentException в WinUI Setter..ctor из ConfigureOpaqueSurfaces. Причина: new Setter(MenuFlyoutPresenter.SystemBackdropProperty, null) — WinRT конструктор Setter отклоняет null value. Общий catch OnLaunched скрывал отказ Editor, открывая MainWindow лаунчера.

Удалён недопустимый Setter. Непрозрачный SolidColorBrush сохраняется в стиле; SystemBackdrop сбрасывается напрямую у созданного presenter при открытии popup. Ошибка запуска Editor теперь отображается как ошибка Editor, а не молча подменяется Launcher; отсутствующий manifest тоже явно отклоняется.

Добавлена Packaging/test-editor-startup.py и режим --editor-startup-check. Проверка создаёт настоящее подписанное окно Editor без Activate, проверяет наличие верхней панели с пятью меню и создание непрозрачного MenuFlyoutPresenter со сбросом SystemBackdrop. Закрывает только своё скрытое окно и завершает отдельный тестовый процесс. Пользовательские процессы не трогает. Build вызывает проверку перед ZIP после подписи/копирования модуля. Это проверка конструирования и visual content tree, не живой показ/клики/приёмка viewport.


Проверка создания Editor прошла при production packaging и повторно из установленной app/Aurora.Launcher.exe. Установлен source c45db8149f0b91f82688cdb7c0897bee0de75ce7, build-c111dafe03994c7d9de2d17e0adcb5c6; 572 совпавших файла. После закрытия окон владельцем оставались четыре старых процесса без MainWindowHandle вследствие прежнего сбоя конструктора; после проверки точного пути и нулевого handle завершены, затем установлен комплект. Новый скрытый тест не оставил процессов.
