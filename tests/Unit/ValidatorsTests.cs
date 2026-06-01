using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Planara.Files.Validators;

namespace Planara.Files.Tests.Unit;

public class ValidatorsTests
{
    [Theory]
    [InlineData("image.png", "image/png")]
    [InlineData("image.jpg", "image/jpeg")]
    [InlineData("image.jpeg", "image/jpeg")]
    [InlineData("image.webp", "image/webp")]
    [InlineData("model.obj", "model/obj")]
    [InlineData("model.obj", "application/octet-stream")]
    [InlineData("model.obj", "text/plain")]
    [InlineData("model.obj", "")]
    public void FormFile_AllowedFile_Succeeds(string fileName, string contentType)
    {
        var validator = new FormFileValidator();
        var file = CreateFile(fileName, contentType, "content");

        var result = validator.Validate(file);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FormFile_EmptyFile_Fails()
    {
        var validator = new FormFileValidator();
        var file = CreateFile("image.png", "image/png", "");

        var result = validator.Validate(file);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x =>
            x.ErrorMessage.Contains("пустым", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormFile_TooLargeFile_Fails()
    {
        var validator = new FormFileValidator();
        var file = CreateFile("image.png", "image/png", "content", length: 1024L * 1024L * 101L);

        var result = validator.Validate(file);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x =>
            x.ErrorMessage.Contains("100 МБ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormFile_FileWithoutExtension_Fails()
    {
        var validator = new FormFileValidator();
        var file = CreateFile("file", "image/png", "content");

        var result = validator.Validate(file);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x =>
            x.ErrorMessage.Contains("Неподдерживаемый", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormFile_UnsupportedExtension_Fails()
    {
        var validator = new FormFileValidator();
        var file = CreateFile("file.exe", "application/octet-stream", "content");

        var result = validator.Validate(file);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x =>
            x.ErrorMessage.Contains("Неподдерживаемый", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormFile_ImageWithWrongContentType_Fails()
    {
        var validator = new FormFileValidator();
        var file = CreateFile("image.png", "text/plain", "content");

        var result = validator.Validate(file);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x =>
            x.ErrorMessage.Contains("Неподдерживаемый", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Guid_NotEmpty_Succeeds()
    {
        var validator = new GuidValidator();

        var result = validator.Validate(Guid.NewGuid());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Guid_Empty_Fails()
    {
        var validator = new GuidValidator();

        var result = validator.Validate(Guid.Empty);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x =>
            x.ErrorMessage.Contains("ID", StringComparison.OrdinalIgnoreCase));
    }

    private static IFormFile CreateFile(
        string fileName,
        string contentType,
        string content,
        long? length = null)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, length ?? bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}