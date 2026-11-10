using QuestPDF.Infrastructure;

namespace ResumeBuilder.Infrastructure.Pdf;

/// <summary>
/// Однократная настройка QuestPDF при первом экспорте в PDF.
/// </summary>
/// <remarks>
/// Раньше лицензия задавалась в статическом конструкторе экспортёра. Если нативная библиотека
/// QuestPDF не загружалась (например, на Windows ARM64, которую QuestPDF не поддерживает),
/// возникал <see cref="TypeInitializationException"/> уже при создании главного окна, и программа
/// не запускалась. Теперь ошибка возникает только при попытке экспорта в PDF и показывается
/// пользователю понятным сообщением, а остальные функции работают.
/// </remarks>
public static class QuestPdfSetup
{
    private static readonly object Sync = new();
    private static bool _configured;

    /// <summary>Настраивает QuestPDF (потокобезопасно, один раз).</summary>
    /// <exception cref="PlatformNotSupportedException">Нативная библиотека QuestPDF недоступна на этой платформе.</exception>
    public static void EnsureConfigured()
    {
        if (_configured) return;
        lock (Sync)
        {
            if (_configured) return;
            try
            {
                // Community-лицензия — бесплатна для учебных проектов
                QuestPDF.Settings.License = LicenseType.Community;
            }
            catch (Exception ex) when (ex is TypeInitializationException or DllNotFoundException or BadImageFormatException)
            {
                throw new PlatformNotSupportedException(
                    "Не удалось загрузить библиотеку QuestPDF для этой платформы " +
                    $"({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}). " +
                    "На Windows ARM64 запускайте приложение как x64.", ex);
            }
            _configured = true;
        }
    }
}
