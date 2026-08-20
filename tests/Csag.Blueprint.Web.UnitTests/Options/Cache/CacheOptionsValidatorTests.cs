namespace Csag.Blueprint.Web.UnitTests.Options.Cache;

using Csag.Blueprint.Web.Options.Cache;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Unit tests for <see cref="CacheOptionsValidator"/>, covering provider selection and the
/// Redis connection string requirement sourced from <see cref="IConfiguration"/>.
/// </summary>
public sealed class CacheOptionsValidatorTests
{
    [Fact]
    public void Validate_NullProvider_Fails()
    {
        var validator = new CacheOptionsValidator(CreateConfiguration());
        var options = new CacheOptions { Provider = null };

        var result = validator.TestValidate(options);

        result.ShouldHaveValidationErrorFor(x => x.Provider)
            .WithErrorMessage("Blueprint:Cache:Provider must be set. Valid values are 'SqlServer' or 'Redis'");
    }

    [Fact]
    public void Validate_UndefinedProviderValue_Fails()
    {
        var validator = new CacheOptionsValidator(CreateConfiguration());
        var options = new CacheOptions { Provider = (CacheProvider)99 };

        var result = validator.TestValidate(options);

        result.ShouldHaveValidationErrorFor(x => x.Provider)
            .WithErrorMessage("Blueprint:Cache:Provider must be either 'SqlServer' or 'Redis'");
    }

    [Fact]
    public void Validate_SqlServerProvider_PassesWithoutRedisConnectionString()
    {
        var validator = new CacheOptionsValidator(CreateConfiguration());
        var options = new CacheOptions { Provider = CacheProvider.SqlServer };

        var result = validator.TestValidate(options);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RedisProviderWithoutConnectionString_Fails()
    {
        var validator = new CacheOptionsValidator(CreateConfiguration());
        var options = new CacheOptions { Provider = CacheProvider.Redis };

        var result = validator.TestValidate(options);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "ConnectionStrings:Redis must be set when CacheProvider is set to 'Redis'");
    }

    [Fact]
    public void Validate_RedisProviderWithConnectionString_Passes()
    {
        var validator = new CacheOptionsValidator(CreateConfiguration(redisConnectionString: "localhost:6379"));
        var options = new CacheOptions { Provider = CacheProvider.Redis };

        var result = validator.TestValidate(options);

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static IConfiguration CreateConfiguration(string? redisConnectionString = null)
    {
        var values = new Dictionary<string, string?>();
        if (redisConnectionString != null)
        {
            values["ConnectionStrings:Redis"] = redisConnectionString;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
