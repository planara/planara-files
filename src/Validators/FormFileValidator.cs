using FluentValidation;

namespace Planara.Files.Validators;

public class FormFileValidator : AbstractValidator<IFormFile>
{
    private const long MaxFileSize = 1024 * 1024 * 100;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".obj"
    };

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedObjContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "model/obj",
        "application/octet-stream",
        "text/plain"
    };

    public FormFileValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0)
            .WithMessage("Файл не может быть пустым.");

        RuleFor(x => x.Length)
            .LessThanOrEqualTo(MaxFileSize)
            .WithMessage("Размер файла не должен превышать 100 МБ.");

        RuleFor(x => x)
            .Must(IsAllowedFile)
            .WithMessage("Неподдерживаемый тип файла. Разрешены: png, jpg, jpeg, webp, obj.");
    }

    private static bool IsAllowedFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension))
            return false;

        if (!AllowedExtensions.Contains(extension))
            return false;

        if (string.IsNullOrWhiteSpace(file.ContentType))
            return extension.Equals(".obj", StringComparison.OrdinalIgnoreCase);

        if (extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
            return AllowedObjContentTypes.Contains(file.ContentType);

        return AllowedImageContentTypes.Contains(file.ContentType);
    }
}