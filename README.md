# BatteryWatt

Потребление ноутбука в ваттах — прямо в системном трее Windows.

Маленькая утилита, которая показывает, сколько энергии ноутбук тратит **прямо сейчас**.
Увидели, что цифра без причины выросла с 7 до 15 Вт — значит, что-то стартовало и
быстрее съест батарею. Это не «загрузка процессора», а именно мощность: она учитывает
экран, Wi-Fi, накопитель, вентиляторы и всё остальное.

```
  8.2   ← зелёный, всё спокойно
  12    ← жёлтый
  16    ← оранжевый
  21    ← красный, батарея тает на глазах
```

## Возможности

- Цифра ватт в трее, цветовая шкала по порогам 9 / 13 / 18 Вт.
- Подсказка при наведении: ватты, остаток в Wh, оценка времени работы.
- Левый клик — подробности, правый — меню, включая галочку «Запускать при входе».
- При питании от сети показывает `AC`, при зарядке — `+12`.
- **14 КБ**, один exe, без установщика, без служб, без прав администратора.
- Собирается встроенным в Windows компилятором C# — не нужен .NET SDK, Visual Studio или NuGet.
- Windows 10 и Windows 11.

![BatteryWatt в трее](docs/preview.png)

## Установка

Нужны только Windows 10/11 и PowerShell 5.1 (есть по умолчанию).

### Вариант 1 — через git

```powershell
git clone https://github.com/SheensonSV/batterywatt.git "$env:TEMP\batterywatt"
powershell -ExecutionPolicy Bypass -File "$env:TEMP\batterywatt\install.ps1"
```

### Вариант 2 — без git, скачать ZIP

```powershell
$zip = "$env:TEMP\batterywatt.zip"
Invoke-WebRequest https://github.com/SheensonSV/batterywatt/archive/refs/heads/main.zip -OutFile $zip
Expand-Archive $zip "$env:TEMP" -Force
powershell -ExecutionPolicy Bypass -File "$env:TEMP\batterywatt-main\install.ps1"
```

### Вариант 3 — двойной клик (без консоли)

Скачайте ZIP, распакуйте и запустите **`install.cmd`** двойным кликом.
Скрипт сам обойдёт запрет на выполнение скриптов, если он включён политикой.

### Что делает `install.ps1`

1. Компилирует `src\BatteryWatt.cs` в `dist\BatteryWatt.exe`.
2. Копирует exe в `%LOCALAPPDATA%\Programs\BatteryWatt`.
3. Включает автозапуск через `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
4. Запускает приложение.

Права администратора не нужны: всё ставится в профиль пользователя.

Полезные ключи:

```powershell
.\install.ps1 -NoAutostart     # поставить, но не добавлять в автозапуск
.\install.ps1 -NoLaunch        # поставить и не запускать
.\install.ps1 -Uninstall       # полностью удалить
```

## Удаление

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
```

Или двойным кликом по **`uninstall.cmd`**.

Вручную: правый клик по иконке → «Выход», затем удалить
`%LOCALAPPDATA%\Programs\BatteryWatt` и значение `BatteryWatt`
в `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## Как это работает

Данные берутся из WMI-класса `root\wmi:BatteryStatus`:

| Свойство | Что означает |
|---|---|
| `PowerOnline` | питание от сети |
| `DischargeRate` | текущий разряд, мВт |
| `ChargeRate` | текущая зарядка, мВт |
| `RemainingCapacity` | остаток, мВт·ч |

Опрос раз в 3 секунды. Оценка времени работы — `остаток / текущая мощность`.

**Ограничение, о котором стоит знать:** контроллер батареи обновляет значение
примерно раз в 10–30 секунд, поэтому цифра меняется ступенями (7.2 → 8.2 → 9.6),
а не плавно. Так ведут себя все подобные утилиты, включая HWiNFO, — это не баг.

## Сборка вручную

```powershell
.\build.ps1
```

Скрипт находит `csc.exe` в `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\`
и собирает `dist\BatteryWatt.exe`.

Ручная команда, если хочется без скрипта:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
  /nologo /target:winexe /optimize+ /codepage:65001 `
  /out:dist\BatteryWatt.exe `
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll `
  src\BatteryWatt.cs
```

## Ключи командной строки

| Ключ | Действие |
|---|---|
| `--once` | прочитать значения один раз и записать `once.txt` рядом с exe |
| `--autostart on` | включить автозапуск для текущего пути к exe |
| `--autostart off` | выключить автозапуск |

## Известные особенности

- Новый значок в трее Windows 10/11 по умолчанию прячется под стрелкой `^`.
  Перетащите его на панель задач, чтобы он был всегда на виду.
- exe не подписан сертификатом. При скачивании готового релиза SmartScreen
  может спросить подтверждение; при локальной сборке этого не происходит.
- Некоторые корпоративные политики запрещают запись в `HKCU\...\Run` —
  тогда автозапуск включить не получится, приложение всё равно будет работать.

## Лицензия

MIT — см. [LICENSE](LICENSE).
