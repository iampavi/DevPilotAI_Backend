using DevPilotAI.Shared.Common;

namespace DevPilotAI.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult_WhenCalled()
    {
        // Act
        var result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult_WhenCalledWithAnError()
    {
        // Arrange
        var expectedError = new Error("Test.Error", "This is a test error.");

        // Act
        var result = Result.Failure(expectedError);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public void SuccessGeneric_ShouldContainValue_WhenSuccessful()
    {
        // Arrange
        var testValue = "Successfully created";

        // Act
        var result = Result.Success(testValue);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(testValue, result.Value);
    }

    [Fact]
    public void FailureGeneric_ShouldThrowException_WhenAccessingValue()
    {
        // Arrange
        var expectedError = new Error("Test.Error", "Failed");
        var result = Result.Failure<string>(expectedError);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
