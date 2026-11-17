namespace Unifesspa.UniPlus.Host.IntegrationTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O <c>type</c> que o host emite aponta para a página publicada no portal: a base resolvida da
/// configuração (<c>ProblemType:BaseUri</c>) é a do contrato que os testes de recusa afirmam. Confere a
/// configuração do host montado por <see cref="MonolitoPostgresFixture"/>, que não sobrescreve a chave; um
/// fixture que passe a sobrescrevê-la fica fora desta conferência e precisa da sua.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class CatalogoDeErrosConcordaComOContratoTests(MonolitoPostgresFixture fixture)
{
    [Fact(DisplayName = "A base do type resolvida pelo host não é vazia e é a do catálogo publicado no contrato")]
    public void BaseDoTypeDoHost_EhADoContrato()
    {
        string baseDoHost = fixture.Factory.Services.GetRequiredService<IOptions<ProblemTypeOptions>>().Value.BaseUri;

        baseDoHost.Should().NotBeNullOrWhiteSpace();
        baseDoHost.Should().Be(CatalogoDeErrosDoContrato.Base);
    }
}
