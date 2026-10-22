# Форматы, адресация и сохранение

## OBJ -> native geometry

`ObjImporter`: объекты/группы материалов, explicit normals и smoothing/hard seams, отрицательные индексы, ear clipping для поддержанных плоских полигонов, ограничение native 16-bit vertices. Native UV V отличается от Wavefront, конверсия явная. Curve/произвольный non-planar repair/skin из OBJ не обещаны. Файлы OBJ неизменяемы; нормализация единиц делается в памяти/сессии.

`ObjTextureMap`/`MaterialDocuments`: mtllib/usemtl, карты относительно их источников, поддержанные MTL options. Не угадывать, что bump обязательно RGB normal. `ModelFileNames` распределяет уникальные имена export/проектных NMF, чтобы два main.nmf из разных папок не перезаписали друг друга.

## NMF и NAF

`NativeModel` читает поддержанные B3DMH/fromObj streams, материалы, node frames, группы и skin streams; индексные/размерные ограничения проверяются. Поддержка не охватывает каждый штатный формат. Ошибка reader не означает, что оригинальная игровая модель повреждена.

`ModelOrientation` преобразует вершины и frame общим transform, сохраняет origins/оси; global pivot bake импортированного skeleton и split rigged model отклоняются. `ModelEditing` назначает материал на mesh/draw group, переименовывает/разделяет геометрию. Rename обновляет native mechanism/animation references. Split исходного анимируемого mesh переносит track на части до commit и проверяет лимиты.

`AnimationTimeline`: pose/keys/FPS, sampler/interpolation и NAF B3DAM10. `AnimatedNmf`: rigid per-mesh bones, weights, draw bone palettes; bone ID стабилен по имени меша между clips. `AnimationBindings`: реальные script keys и совместимость с типом/ролью. Импорт существующих NAF в редактируемые keys пока отсутствует; исходные mate-файлы сохраняются при переносе.

WORKING exports содержат полную рабочую модель; attached actions экспортируют выбранные компоненты, исключаемые из static main/LOD, чтобы избежать дубликатов. Это контракт файлов и вывод из native assets, не выполненная engine acceptance. Cargo binding использует RESOURCE_VISUALIZATION_BONE и общий стабильный parent во всех нужных действиях.

## Native/PBR material pairing

- `material.mtl` -> `material_pbr.mtl`.
- `material_1.mtl` -> `material_pbr_1.mtl` (и другие номера).
- Другой native basename -> `<basename>_pbr.mtl`.
- Legacy fallback: `.pbr`, `.pbr.mtl`, `pbr.mtl` в описанном порядке поиска. Не делать legacy общую пару приоритетнее точной numbered пары.

`TEXTURE` game-root reference отличается от `TEXTURE_MTL`, который относительно MTL. Не искать наугад вверх по диску и не подменять другой одноимённой картой. Portability требует export используемых зависимостей и переписывания ссылок. DDS исходники по возможности сохраняются; конвертация карт и mipmap policy — отдельная область проверки.

PBR maps: base/normal, ORM или separate metallic/roughness/AO, specular отдельно, emissive/height и дополнительные свойства по MaterialSettings. Наличие поля не равно поддержке конкретным shader adapter; смотреть связанный renderer.

## script.ini

`VehicleScript` разбирает активную область до END, повторения, quoted tokens, составные role arguments и блоки cargo. Неизвестные поля должны сохраняться. Нельзя вручную пересоздать весь INI из нескольких видимых полей и потерять остальное.

- FAMILY — базовая модель и наследование части параметров, например годов доступности.
- PURCHASE_EXCLUDE — скрыть объект из списка покупки, подходит служебным секциям/прицепам.
- JOINT — присоединённый NMF со смещением/поправкой, не mesh внутри главного NMF.
- HORSE — размещение/ссылка упряжи; SINGLE_HORSE_POWER и HORSE_MATERIAL — отдельные native directives Early Start, не универсальная мощность двигателя машины.
- MOVEMENT_HILL_ACCELERATION реально присутствует в native scripts; не путать с ранее сомнительными/obsolete consumption/service-life полями.
- Native names/countries/currencies/roles должны показываться понятными названиями; классовые поля фильтруются по выбранному транспорту.

Экспорт включает dependencies (модели, материалы, текстуры, sounds INI references и mates), LOD, named variants и связанные workspace objects. `TransportPackage` выдаёт папки объекта и Workshop script, переписывает `aurora:<object>` в пути комплекта. Исходная игра не служит местом staging.

## .aur

`AuroraProjectArchive`: magic AURORA + schema, ZIP payload, AES-GCM с embedded application key, случайным nonce/tag, atomic `.writing-*` -> final. Ограничения: 256 MiB, 2048 entries, portable relative paths, отсутствие traversal/дубликатов/опасных Windows имён.

Открытие без пароля выбрано владельцем. Это защита формата приложения, **не секретность от владельца исходников и не publisher signature**. Не обещать «прочтёт только Editor» как криптографическую гарантию.

`document.json` содержит модели, variants/settings, выбранные позиции, script, transform/pivot, lights/cargo/cameras, clips и axis overrides. `TransportWorkspace` добавляет `transport.json`, до 64 objects, content-addressed `data/<sha256>` и active object. Старый single-object project поддерживается. Сохранение .aur и native export — разные операции; промежуточный проект сохраняется даже без окончательных игровых bindings.
