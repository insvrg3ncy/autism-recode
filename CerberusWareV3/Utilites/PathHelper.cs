using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CerberusWareV3.Utilites;

/// <summary>
/// Кроссплатформенная утилита для работы с путями к файлам приложения
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Возвращает путь к папке данных приложения (кроссплатформенный)
    /// Windows: %APPDATA%/CerberusWare
    /// Linux: ~/.local/share/CerberusWare
    /// macOS: ~/Library/Application Support/CerberusWare
    /// </summary>
    public static string GetAppDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: %APPDATA%/CerberusWare
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CerberusWare");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: ~/.local/share/CerberusWare
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".local", "share", "CerberusWare");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: ~/Library/Application Support/CerberusWare
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, "Library", "Application Support", "CerberusWare");
        }
        else
        {
            // Fallback для других ОС
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".CerberusWare");
        }
    }

    /// <summary>
    /// Возвращает путь к папке конфигурации приложения
    /// </summary>
    public static string GetConfigDirectory()
    {
        return Path.Combine(GetAppDataDirectory(), "Config");
    }

    /// <summary>
    /// Открывает папку в файловом менеджере (кроссплатформенный)
    /// </summary>
    public static void OpenFolderInExplorer(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return;

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Diagnostics.Process.Start("explorer", folderPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Попробуем разные файловые менеджеры
                string[] fileManagers = { "xdg-open", "nautilus", "dolphin", "thunar", "pcmanfm" };
                foreach (var manager in fileManagers)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(manager, folderPath);
                        return;
                    }
                    catch
                    {
                        // Пробуем следующий
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                System.Diagnostics.Process.Start("open", folderPath);
            }
        }
        catch
        {
            // Игнорируем ошибки открытия папки
        }
    }
}

