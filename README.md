# Centerly

Скачать: [последний релиз](https://github.com/ducker-dev/centerly/releases/latest), файл `Centerly-win-Setup.exe`. Подпись кода: [Code signing policy](#code-signing-policy).

Утилита для Windows 10/11, которая центрирует окна:

- **по сочетанию клавиш** — активное окно (по умолчанию `Ctrl + Alt + C`);
- **по сочетанию клавиш на следующий или предыдущий монитор**: окно переносится на соседний монитор и центрируется там, развёрнутое окно остаётся развёрнутым (сочетания задаются в настройках);
- **автоматически при открытии**: окна приложений из вашего списка или всех приложений, кроме исключений, включая приложения из Microsoft Store.
  У каждого режима свой список: в «Только из списка» это приложения для центрирования, в «Все, кроме исключений» — исключения. При переключении режима второй список сохраняется.

Сочетание «Вернуть окно на место» отменяет последнее перемещение активного окна, в том числе автоматическое. Повторное нажатие возвращает окно обратно.

Подробности поведения:

- Центрирует по видимой рамке окна: невидимые границы ресайза Windows 10/11 не сдвигают его.
- Учитывает панель задач, работает на каждом мониторе отдельно, в том числе с разным масштабом (DPI).
- Не трогает развёрнутые, свёрнутые и полноэкранные окна.
- Автоматически центрируются только окна, открытые после запуска Centerly. Окна, которые уже существовали (свёрнутые в трей, на другом виртуальном рабочем столе, открытые во время паузы), не перемещаются, когда снова появляются.
- В режиме «Все, кроме исключений» окна программ, запущенных от администратора, пропускаются без уведомления. Для приложений из списка уведомление показывается.
- Если приложение после открытия само возвращает окно на сохранённое место, Centerly центрирует его повторно (не больше трёх раз). Как только вы начинаете перетаскивать окно, повторные попытки прекращаются.
- Приложение в список можно добавить тремя способами: выбрать из запущенных, «прицелом» (перетащить кнопку на окно) или указать `.exe`.
- Автозапуск: обычный или с правами администратора (через Планировщик задач, без запроса UAC при каждом входе).
- Интерфейс на русском и английском, светлая и тёмная тема по системе.
- Автообновление из GitHub Releases (Velopack).

## Сборка и запуск

Нужен [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build Centerly.slnx
dotnet test --solution Centerly.slnx
dotnet run --project src/Centerly.App
```

## Релиз

Локально (результат в `artifacts/releases/win-x64`: `Centerly-win-Setup.exe`, портативный zip и пакеты обновлений):

```powershell
./tools/publish.ps1 -Version 0.2.0 -RepositoryUrl https://github.com/ducker-dev/centerly
```

Через GitHub Actions: отправьте тег `v0.2.0`. Workflow `release.yml` прогонит тесты, соберёт пакеты (включая дельта-обновление от предыдущей версии) и опубликует релиз. Установленные копии найдут обновление сами.

Без `-RepositoryUrl` сборка получается без проверки обновлений. Сборка self-contained, поэтому .NET у пользователя не нужен. Установщик кладёт приложение в `%LocalAppData%\Centerly` и создаёт ярлык в меню «Пуск». Права администратора для установки не нужны.

Код пока не подписан: при первом запуске установщика Windows SmartScreen покажет предупреждение. Для открытого проекта можно бесплатно получить подпись через [SignPath Foundation](https://signpath.org/), платный вариант — Azure Trusted Signing.

## Структура

| Путь | Что внутри |
|---|---|
| `src/Centerly.Core` | Win32-обёртки (CsWin32), геометрия центрирования, хоткеи, автоцентрирование, настройки |
| `src/Centerly.App` | WPF-приложение (WPF-UI): окно настроек, трей, автозапуск, обновления |
| `tests/Centerly.Tests` | Юнит-тесты (xUnit v3) |
| `tools/strings.py` | Все строки интерфейса (RU/EN). После правки запустите `python tools/strings.py` |
| `tools/publish.ps1` | Сборка релиза и упаковка Velopack |
| `tools/make-icon.ps1` | Генерация `app.ico` |

## Где лежат данные

- Настройки: `%AppData%\Centerly\settings.json` (сохраняются при удалении приложения)
- Логи: `%LocalAppData%\Centerly\logs`, хранятся 7 дней

Аргументы командной строки:

- `--background` — запуск сразу в трей (его использует автозапуск).

## Ограничения

- Windows не позволяет обычному процессу перемещать окна программ, запущенных от имени администратора. Для таких окон Centerly нужно запустить от имени администратора: кнопкой в настройках или автозапуском «с правами администратора».
- Окна, открытые до запуска Centerly, автоматически не центрируются. Для них есть сочетание клавиш.

## Ручная проверка перед релизом

- [ ] `Ctrl+Alt+C` центрирует обычное окно, окно Store-приложения и консоль (Windows Terminal).
- [ ] Развёрнутое и полноэкранное окно (видео, игра) не трогается.
- [ ] Перенос на следующий и предыдущий монитор работает в обе стороны, в том числе между мониторами с разным масштабом.
- [ ] Развёрнутое окно переносится на другой монитор развёрнутым, после «Восстановить» оказывается по центру нового монитора.
- [ ] «Вернуть окно на место» после сочетания, после автоцентрирования и после переноса развёрнутого окна; повторное нажатие возвращает обратно.
- [ ] Занятое сочетание (например, уже используемое другой программой) показывает предупреждение, старое сочетание продолжает работать.
- [ ] Автоцентрирование: Блокнот, Калькулятор, приложение, которое запоминает позицию (закрыть в углу и открыть снова).
- [ ] «Только первое окно» и «Также диалоги» ведут себя как описано.
- [ ] Режим «Все, кроме исключений»: новое окно любого приложения центрируется, исключённое приложение не трогается, Диспетчер задач без прав администратора пропускается молча.
- [ ] Приложение, свёрнутое в трей до запуска Centerly, при открытии из трея остаётся на своём месте.
- [ ] «Прицел»: подсветка следует за курсором по обоим мониторам, отпускание над окном добавляет приложение.
- [ ] Автозапуск обычный и с правами администратора: после перезагрузки приложение в трее, хоткеи работают.
- [ ] Окно программы, запущенной от администратора: без прав — уведомление, с правами — центрируется.
- [ ] Смена языка и «Перезапустить».
- [ ] Установка, обновление на следующую версию и удаление через «Параметры → Приложения».
- [ ] Windows 10.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

Starting with the first signed release, the Windows installer (`Centerly-win-Setup.exe`) and the application executables published on [GitHub Releases](https://github.com/ducker-dev/centerly/releases) are signed. They are built only by the [Release workflow](.github/workflows/release.yml) on GitHub Actions from this repository's source code; a release is triggered by pushing a version tag. Every signing request is approved manually.

### Team roles

| Role | Members |
|---|---|
| Authors (committers): may change the source code without additional review | [ducker-dev](https://github.com/ducker-dev) |
| Reviewers: review changes from contributors, such as pull requests | [ducker-dev](https://github.com/ducker-dev) |
| Approvers: approve each signing request | [ducker-dev](https://github.com/ducker-dev) |

### Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it, with this exception:

- **Update checks.** When "Check for updates automatically" is enabled (the default) or the user clicks "Check now", the program requests the list of releases from GitHub (`github.com`) and downloads an update only when the user chooses to install it. No personal data, settings or usage statistics are sent; GitHub receives the usual request data such as the IP address. See the [GitHub General Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement). Automatic checks can be turned off on the "General" tab.

The program has no telemetry. Settings and logs are stored only on the local computer (see "Где лежат данные" above).

### Verifying a download

Open the file's Properties → Digital Signatures, or run in PowerShell:

```powershell
Get-AuthenticodeSignature .\Centerly-win-Setup.exe
```

If you believe a signed file is malicious or has been tampered with, do not run it and [open an issue](https://github.com/ducker-dev/centerly/issues) with the file's SHA256 hash and where you downloaded it.

## Лицензия

[MIT](LICENSE). Автор: [@ducker_dev](https://t.me/ducker_dev).
