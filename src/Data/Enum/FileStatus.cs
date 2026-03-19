namespace Planara.Files.Data.Enum;

/// <summary>
/// Статус файла
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// Загружается
    /// </summary>
    Uploading = 0,
    
    /// <summary>
    /// Загружен
    /// </summary>
    Ready = 1,
    
    /// <summary>
    /// Загрузка закончилась с ошибкой
    /// </summary>
    Failed = 2,
    
    /// <summary>
    /// Удален
    /// </summary>
    Deleted = 3
}