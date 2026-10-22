# Точная установленная сборка 0.2

Это сохранение уже установленного комплекта, не новый Release. Source `c45db8149f0b91f82688cdb7c0897bee0de75ce7`, build `build-c111dafe03994c7d9de2d17e0adcb5c6`, production signing. Проверено 572 установленных файла. Подробности и границы приёмки — [текущее состояние](../01_CURRENT_STATE.md).

| Файл | Назначение |
| --- | --- |
| Aurora-0.2-win-x64.zip | Один ZIP Launcher + Editor, 99 149 415 байт, Git LFS |
| Aurora-Setup.exe | Соседний installer, 413 696 байт |
| aurora-update.json | Подписанные release metadata исходного комплекта |
| SHA256SUMS.txt | Хеши исходных трёх файлов |

Полные ожидаемые размеры и SHA256 также в [snapshot.json](../snapshot.json). Закрытого signing key здесь нет. Выполнить `git lfs pull`, если ZIP содержит текстовый LFS pointer. Для установки оставить Setup рядом с этим ZIP и закрыть Aurora Launcher/Editor. Не использовать RESAURO.zip вместо payload: RESAURO хранит исходники и документацию.
