namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

public sealed class SchemaRegistryTipoDeAutenticacaoTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("99")]
    [InlineData("-1")]
    public void Validate_TipoNumericoLidoDaConfiguracao_Recusa(string tipo)
    {
        Dictionary<string, string?> valores = new()
        {
            ["SchemaRegistry:Url"] = "http://schema-registry:8081",
            ["SchemaRegistry:AuthType"] = tipo,
        };
        IConfiguration configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        SchemaRegistrySettings options = configuracao.GetSection(SchemaRegistrySettings.SectionName).Get<SchemaRegistrySettings>()!;

        ValidateOptionsResult resultado = new SchemaRegistrySettingsValidator().Validate(null, options);

        resultado.Failures.Should().Equal($"SchemaRegistry:AuthType '{tipo}' inválido. Use None, Basic ou OAuthBearer.");
    }
}
