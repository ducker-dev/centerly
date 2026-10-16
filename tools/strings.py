"""Source of truth for UI strings: regenerates src/Centerly.App/Resources/Strings*.resx and Strings.Designer.cs.

Usage: python tools/strings.py
"""
from pathlib import Path
from xml.sax.saxutils import escape

STRINGS = {
    # key: (English, Russian)
    "MainTitle": ("Window centering", "Центрирование окон"),
    "EnabledTitle": ("Enabled", "Включено"),
    "EnabledDescription": ("When off, hotkeys and automatic centering stop working",
                           "Если выключить, сочетания клавиш и автоцентрирование перестанут работать"),

    "SectionHotkeys": ("Hotkeys", "Сочетания клавиш"),
    "HotkeyCenterTitle": ("Center the active window", "Центрировать активное окно"),
    "HotkeyHint": ("Click the field and press a combination. Backspace clears, Esc cancels",
                   "Нажмите на поле и введите сочетание. Backspace — отключить, Esc — отмена"),
    "HotkeyNextMonitorTitle": ("Move to the next monitor", "Перенести на следующий монитор"),
    "HotkeyNextMonitorDescription": ("Moves the active window to the next monitor and centers it there. Maximized windows stay maximized",
                                     "Переносит активное окно на следующий монитор и центрирует его там. Развёрнутое окно остаётся развёрнутым"),
    "HotkeyPreviousMonitorTitle": ("Move to the previous monitor", "Перенести на предыдущий монитор"),
    "HotkeyPreviousMonitorDescription": ("Same, in the opposite direction", "То же самое в обратную сторону"),
    "HotkeyUndoTitle": ("Put the window back", "Вернуть окно на место"),
    "HotkeyUndoDescription": ("Returns the active window to where it was before Centerly moved it, including automatic centering. Press again to move it back",
                              "Возвращает активное окно туда, где оно было до перемещения Centerly, в том числе после автоцентрирования. Повторное нажатие отменяет возврат"),
    "HotkeyBoxEmpty": ("Not set", "Не задано"),
    "HotkeyBoxPrompt": ("Press a combination…", "Нажмите сочетание клавиш…"),

    "SectionAutoCenter": ("Auto-centering", "Автоцентрирование"),
    "AutoModeTitle": ("Which apps to center", "Какие приложения центрировать"),
    "AutoModeDescription": ("Each option has its own list below: apps to center or apps to leave alone",
                            "У каждого варианта свой список ниже: какие приложения центрировать или какие не трогать"),
    "AutoModeListed": ("Only apps from the list", "Только приложения из списка"),
    "AutoModeAll": ("All apps except exclusions", "Все, кроме исключений"),
    "RulesEmpty": ("Add applications whose windows should be centered as soon as they open.",
                   "Добавьте приложения, окна которых нужно центрировать сразу при открытии."),
    "RulesEmptyAllApps": ("New windows of all apps are centered. Add apps whose windows should be left alone.",
                          "Новые окна всех приложений центрируются. Добавьте приложения, окна которых трогать не нужно."),
    "RuleOnlyFirstWindow": ("Only the first window after the app starts", "Только первое окно после запуска приложения"),
    "RuleIncludeDialogs": ("Also center dialogs and secondary windows", "Также центрировать диалоги и дополнительные окна"),
    "RuleRemove": ("Remove from the list", "Удалить из списка"),
    "AddRunning": ("Add a running app", "Добавить из запущенных"),
    "AddByCrosshair": ("Drag onto a window", "Перетащите на окно"),
    "AddByCrosshairTooltip": ("Hold the mouse button, drag onto any window and release",
                              "Зажмите кнопку мыши, наведите на нужное окно и отпустите"),
    "AddExe": ("Choose an .exe…", "Выбрать .exe…"),

    "SectionGeneral": ("General", "Общее"),
    "OversizeTitle": ("Windows larger than the screen", "Окна больше экрана"),
    "OversizeDescription": ("What to do when a window doesn't fit into the monitor's work area",
                            "Что делать, если окно не помещается в рабочую область монитора"),
    "OversizeKeepSize": ("Keep the size", "Не менять размер"),
    "OversizeShrink": ("Shrink to fit the screen", "Уменьшить до размера экрана"),
    "AutostartTitle": ("Start with Windows", "Запуск вместе с Windows"),
    "AutostartDescription": ("Starts minimized to the notification area", "Запускается свёрнутым в область уведомлений"),
    "AutostartOff": ("Don't start", "Не запускать"),
    "AutostartNormal": ("At sign-in", "При входе в систему"),
    "AutostartElevated": ("At sign-in, as administrator", "При входе, с правами администратора"),
    "AdminTitle": ("Administrator rights", "Права администратора"),
    "AdminDescription": ("Needed to move windows of programs running as administrator",
                         "Нужны, чтобы перемещать окна программ, запущенных от имени администратора"),
    "AdminRunning": ("Running as administrator", "Запущено с правами администратора"),
    "AdminRestart": ("Restart as administrator", "Перезапустить от имени администратора"),
    "LanguageTitle": ("Language", "Язык"),
    "LanguageDescription": ("Takes effect after a restart", "Применяется после перезапуска"),
    "LanguageSystem": ("Same as Windows", "Как в системе"),
    "RestartNow": ("Restart", "Перезапустить"),
    "UpdatesTitle": ("Updates", "Обновления"),
    "UpdatesAutoCheck": ("Check for updates automatically", "Проверять обновления автоматически"),
    "UpdatesCheckNow": ("Check now", "Проверить сейчас"),
    "UpdatesChecking": ("Checking…", "Проверка…"),
    "UpdatesUpToDate": ("You have the latest version", "Установлена последняя версия"),
    "UpdatesAvailableFormat": ("Version {0} is available", "Доступна версия {0}"),
    "UpdatesInstall": ("Update and restart", "Обновить и перезапустить"),
    "UpdatesInstallingFormat": ("Downloading… {0}%", "Загрузка… {0}%"),
    "UpdatesInstallFailed": ("Couldn't install the update. See the log for details.",
                             "Не удалось установить обновление. Подробности в логе."),
    "SettingsFolder": ("Settings folder", "Папка настроек"),
    "Logs": ("Logs", "Логи"),
    "VersionFormat": ("Version {0}", "Версия {0}"),
    "TelegramTooltip": ("Author's Telegram", "Telegram автора"),

    "MsgHotkeyUnsuitableTitle": ("Unsuitable combination", "Неподходящее сочетание"),
    "MsgHotkeyUnsuitableText": ("“{0}” would interfere with normal typing. Add Ctrl, Alt or Win.",
                                "«{0}» будет мешать обычному набору текста. Добавьте Ctrl, Alt или Win."),
    "MsgHotkeyInUseTitle": ("The combination is taken", "Сочетание занято"),
    "MsgHotkeyInUseText": ("“{0}” is already used by another app or by Windows. Choose another one.",
                           "«{0}» уже использует другое приложение или Windows. Выберите другое."),
    "MsgHotkeyDuplicateText": ("“{0}” is already assigned to another action.", "«{0}» уже назначено на другое действие."),
    "MsgHotkeyFailedTitle": ("Couldn't assign the combination", "Не удалось назначить сочетание"),
    "MsgHotkeyFailedText": ("Windows didn't allow registering “{0}”. See the log for details.",
                            "Windows не позволила зарегистрировать «{0}». Подробности в логе."),
    "MsgHotkeyNotWorkingTitle": ("A hotkey doesn't work", "Сочетание клавиш не работает"),
    "MsgHotkeyNotWorkingText": ("“{0}” is currently taken by another app. Choose another combination.",
                                "«{0}» сейчас занято другим приложением. Выберите другое сочетание."),
    "MsgAlreadyInListTitle": ("Already in the list", "Уже в списке"),
    "MsgAlreadyInListText": ("“{0}” is already in the list.", "«{0}» уже есть в списке."),
    "MsgCrosshairHintTitle": ("Drag the crosshair", "Перетащите прицел"),
    "MsgCrosshairHintText": ("Press and hold the mouse button on this button, move the pointer onto the window you need and release it.",
                             "Зажмите кнопку мыши на этой кнопке, наведите указатель на нужное окно и отпустите."),
    "MsgAutostartFailedTitle": ("Couldn't change autostart", "Не удалось изменить автозапуск"),
    "MsgAutostartFailedText": ("The change was cancelled or failed. See the log for details.",
                               "Изменение отменено или завершилось ошибкой. Подробности в логе."),

    "PickerTitle": ("Add an application", "Добавить приложение"),
    "PickerAdd": ("Add", "Добавить"),
    "PickerSearch": ("Search", "Поиск"),
    "PickerEmpty": ("No suitable running apps. Start the app you need or choose its .exe file.",
                    "Нет подходящих запущенных приложений. Запустите нужное приложение или выберите его .exe-файл."),
    "Cancel": ("Cancel", "Отмена"),
    "ExeDialogTitle": ("Choose a program", "Выберите программу"),
    "ExeDialogFilter": ("Programs (*.exe)|*.exe", "Программы (*.exe)|*.exe"),

    "TrayOpen": ("Open Centerly", "Открыть Centerly"),
    "TrayPause": ("Pause", "Приостановить"),
    "TrayExit": ("Exit", "Выход"),
    "TrayTooltipPaused": ("Centerly — paused", "Centerly — приостановлено"),

    "NotifyHotkeyText": ("“{0}” is taken by another app. Open Centerly to choose another one.",
                         "«{0}» занято другим приложением. Откройте Centerly, чтобы выбрать другое."),
    "NotifySettingsResetTitle": ("Settings were reset", "Настройки сброшены"),
    "NotifySettingsResetText": ("The settings file was damaged and has been saved as a backup. Default settings are used.",
                                "Файл настроек был повреждён и сохранён как резервная копия. Используются настройки по умолчанию."),
    "NotifyTrayTitle": ("Centerly keeps running", "Centerly продолжает работать"),
    "NotifyTrayText": ("The app is in the notification area. Use its icon menu to exit.",
                       "Приложение свёрнуто в область уведомлений. Выйти можно через меню значка."),
    "NotifyElevatedTitle": ("Couldn't move the window", "Не удалось переместить окно"),
    "NotifyElevatedText": ("The window belongs to a program running as administrator. Restart Centerly as administrator to move such windows.",
                           "Окно принадлежит программе, запущенной от имени администратора. Чтобы перемещать такие окна, перезапустите Centerly от имени администратора."),
    "NotifyUpdateTitle": ("An update is available", "Доступно обновление"),
    "NotifyUpdateText": ("Centerly {0} is available. Open the app to install it.",
                         "Доступна версия Centerly {0}. Откройте приложение, чтобы установить её."),

    "ErrorStartup": ("Couldn't start Centerly:\n\n{0}\n\nDetails were written to the log:\n{1}",
                     "Не удалось запустить Centerly:\n\n{0}\n\nПодробности записаны в лог:\n{1}"),
    "ErrorUnexpected": ("An unexpected error occurred:\n\n{0}\n\nDetails were written to the log:\n{1}",
                        "Произошла непредвиденная ошибка:\n\n{0}\n\nПодробности записаны в лог:\n{1}"),
}

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by tools/strings.py, edit the strings there. -->
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""


def write(path: Path, index: int) -> None:
    body = "".join(
        f'  <data name="{key}" xml:space="preserve">\n    <value>{escape(values[index])}</value>\n  </data>\n'
        for key, values in STRINGS.items())
    path.write_text(HEADER + body + "</root>\n", encoding="utf-8", newline="\r\n")


def write_accessor(path: Path) -> None:
    properties = "".join(
        f"""
    /// <summary>{escape(values[0].splitlines()[0])}</summary>
    public static string {key} => Get("{key}");
""" for key, values in STRINGS.items())
    path.write_text(f"""// <auto-generated>Generated by tools/strings.py, edit the strings there.</auto-generated>
using System.Globalization;
using System.Resources;

namespace Centerly.App.Resources;

/// <summary>Localized UI strings; public so XAML can use {{x:Static}}.</summary>
public static class Strings
{{
    private static readonly ResourceManager Manager = new("Centerly.App.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string name) =>
        Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
{properties}}}
""", encoding="utf-8", newline="\r\n")


if __name__ == "__main__":
    resources = Path(__file__).resolve().parent.parent / "src" / "Centerly.App" / "Resources"
    write(resources / "Strings.resx", 0)
    write(resources / "Strings.ru.resx", 1)
    write_accessor(resources / "Strings.Designer.cs")
    print(f"{len(STRINGS)} strings written to {resources}")
