# Editor открывал Launcher вместо собственного окна

Владелец сообщил проблему после revision 06. Журнал LocalAppData/Aurora/editor-startup-error.txt показал ArgumentException в WinUI Setter..ctor из ConfigureOpaqueSurfaces. Причина: new Setter(MenuFlyoutPresenter.SystemBackdropProperty, null) — WinRT конструктор Setter отклоняет null value. Общий catch OnLaunched скрывал отказ Editor, открывая MainWindow лаунчера.

Удалён недопустимый Setter. Непрозрачный SolidColorBrush сохраняется в стиле; SystemBackdrop сбрасывается напрямую у созданного presenter при открытии popup. Ошибка запуска Editor теперь отображается как ошибка Editor, а не молча подменяется Launcher; отсутствующий manifest тоже явно отклоняется.

Добавлена Packaging/test-editor-startup.py и режим --editor-startup-check. Проверка создаёт настоящее подписанное окно Editor без Activate, проверяет наличие верхней панели с пятью меню и создание непрозрачного MenuFlyoutPresenter со сбросом SystemBackdrop. Закрывает только своё скрытое окно и завершает отдельный тестовый процесс. Пользовательские процессы не трогает. Build вызывает проверку перед ZIP после подписи/копирования модуля. Это проверка конструирования и visual content tree, не живой показ/клики/приёмка viewport.
