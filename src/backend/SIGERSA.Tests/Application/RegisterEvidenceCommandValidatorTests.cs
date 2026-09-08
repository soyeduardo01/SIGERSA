using SIGERSA.Application.Evidences;

namespace SIGERSA.Tests.Application;

public sealed class RegisterEvidenceCommandValidatorTests
{
    [Fact]
    public void ValidateShouldRejectRelativeSupabasePath()
    {
        var command = new RegisterEvidenceCommand(
            Guid.NewGuid(),
            "evidencias",
            "casos/../secreto.pdf",
            "application/pdf",
            100,
            new string('a', 64));

        var result = new RegisterEvidenceCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.SupabasePath));
    }
}
