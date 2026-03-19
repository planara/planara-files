using Planara.Common.Database.Domain;
using Planara.Files.Data.Enum;

namespace Planara.Files.Data.Domain;

/// <summary>
/// Метаданные файла
/// </summary>
public class FileMetadata: BaseEntity
{
    /// <summary>
    /// ID пользователя
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Название файла
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Расширение файла
    /// </summary>
    public string Extension { get; set; } = string.Empty;
    
    /// <summary>
    /// Название бакета, в котором хранится файл
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// По какому ключу объект лежит в бакете
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип контента для http headers
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";
    
    /// <summary>
    /// Размер файла
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// Видимость файла
    /// </summary>
    public FileVisibility Visibility { get; set; } = FileVisibility.Private;
    
    /// <summary>
    /// Статус файла
    /// </summary>
    public FileStatus Status { get; set; } = FileStatus.Uploading;
    
    /// <summary>
    /// Контрольная сумма
    /// </summary>
    public string? Checksum { get; set; }
    
    /// <summary>
    /// Время удаления
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}