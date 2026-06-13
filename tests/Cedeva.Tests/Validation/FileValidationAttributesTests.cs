using System.ComponentModel.DataAnnotations;
using Cedeva.Website.Validation;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Cedeva.Tests.Validation;

public class FileValidationAttributesTests
{
    private static IFormFile FileWith(string fileName, long length = 123)
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.Length.Returns(length);
        return file;
    }

    private static ValidationResult? Validate(ValidationAttribute attribute, object? value) =>
        attribute.GetValidationResult(value, new ValidationContext(new object()));

    public class AllowedExtensions
    {
        private static readonly AllowedExtensionsAttribute Attribute =
            new(".pdf", ".jpg", ".png");

        [Theory]
        [InlineData("document.pdf")]
        [InlineData("photo.jpg")]
        [InlineData("image.png")]
        public void ListedExtension_Passes(string fileName) =>
            Validate(Attribute, FileWith(fileName)).Should().BeNull();

        [Theory]
        [InlineData("document.PDF")]
        [InlineData("photo.JPG")]
        [InlineData("image.PnG")]
        public void ListedExtension_IsCaseInsensitive(string fileName) =>
            Validate(Attribute, FileWith(fileName)).Should().BeNull();

        [Theory]
        [InlineData("malware.exe")]
        [InlineData("data.txt")]
        [InlineData("archive.zip")]
        public void UnlistedExtension_Fails(string fileName) =>
            Validate(Attribute, FileWith(fileName)).Should().NotBeNull();

        [Fact]
        public void NoExtension_Fails() =>
            Validate(Attribute, FileWith("noextension")).Should().NotBeNull();

        [Fact]
        public void Null_Passes_SoItComposesWithRequired() =>
            Validate(Attribute, null).Should().BeNull();

        [Fact]
        public void NonFileValue_Passes()
        {
            // The attribute only validates IFormFile instances; anything else passes.
            Validate(Attribute, "not a file").Should().BeNull();
        }

        [Fact]
        public void FailureMessage_ListsAllowedExtensions()
        {
            var result = Validate(Attribute, FileWith("malware.exe"));

            result.Should().NotBeNull();
            result!.ErrorMessage.Should().Contain(".pdf");
            result.ErrorMessage.Should().Contain(".jpg");
            result.ErrorMessage.Should().Contain(".png");
        }
    }

    public class MaxFileSize
    {
        private const int Limit = 1024;
        private static readonly MaxFileSizeAttribute Attribute = new(Limit);

        [Fact]
        public void SizeBelowLimit_Passes() =>
            Validate(Attribute, FileWith("x.pdf", Limit - 1)).Should().BeNull();

        [Fact]
        public void SizeEqualToLimit_Passes() =>
            Validate(Attribute, FileWith("x.pdf", Limit)).Should().BeNull();

        [Fact]
        public void SizeAboveLimit_Fails() =>
            Validate(Attribute, FileWith("x.pdf", Limit + 1)).Should().NotBeNull();

        [Fact]
        public void EmptyFile_Passes() =>
            Validate(Attribute, FileWith("x.pdf", 0)).Should().BeNull();

        [Fact]
        public void Null_Passes_SoItComposesWithRequired() =>
            Validate(Attribute, null).Should().BeNull();

        [Fact]
        public void NonFileValue_Passes() =>
            Validate(Attribute, "not a file").Should().BeNull();

        [Fact]
        public void FailureMessage_ContainsMegabyteLimit()
        {
            // 1 MB limit so the message renders a clean "1 MB".
            var attribute = new MaxFileSizeAttribute(1024 * 1024);

            var result = Validate(attribute, FileWith("x.pdf", 1024 * 1024 + 1));

            result.Should().NotBeNull();
            result!.ErrorMessage.Should().Contain("1");
        }
    }
}
