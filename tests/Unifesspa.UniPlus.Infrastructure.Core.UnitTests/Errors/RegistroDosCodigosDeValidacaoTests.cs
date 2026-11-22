namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using FluentValidation;
using FluentValidation.Validators;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[CollectionDefinition(nameof(ConfiguracaoGlobalDoFluentValidation), DisableParallelization = true)]
public sealed class ConfiguracaoGlobalDoFluentValidation;

[Collection(nameof(ConfiguracaoGlobalDoFluentValidation))]
public sealed class RegistroDosCodigosDeValidacaoTests
{
    [Fact]
    public void AddDomainErrorMapper_InstalaResolvedor()
    {
        Func<IPropertyValidator, string> anterior = ValidatorOptions.Global.ErrorCodeResolver;
        try
        {
            ValidatorOptions.Global.ErrorCodeResolver = _ => "outro";
            new ServiceCollection().AddDomainErrorMapper(new ConfigurationBuilder().Build());
            ValidatorOptions.Global.ErrorCodeResolver.Should().Be((Func<IPropertyValidator, string>)ValidationErrorCodes.Resolve);
        }
        finally
        {
            ValidatorOptions.Global.ErrorCodeResolver = anterior;
        }
    }

    [Theory]
    [InlineData(ValidationErrorCodes.Raiz)]
    [InlineData(ValidationErrorCodes.Obrigatorio)]
    [InlineData(ValidationErrorCodes.Tamanho)]
    [InlineData(ValidationErrorCodes.Formato)]
    [InlineData(ValidationErrorCodes.Faixa)]
    [InlineData(ValidationErrorCodes.Regra)]
    public void AddDomainErrorMapper_RegistraCodigoDeValidacao(string codigo)
    {
        using ServiceProvider servicos = new ServiceCollection().AddDomainErrorMapper(new ConfigurationBuilder().Build()).BuildServiceProvider();
        IEnumerable<DomainErrorMapping> mapeamentos = servicos.GetServices<IDomainErrorRegistration>()
            .SelectMany(registro => registro.GetMappings()).Select(par => par.Value);
        mapeamentos.Should().Contain(mapeamento => mapeamento.Code == codigo);
    }
}
