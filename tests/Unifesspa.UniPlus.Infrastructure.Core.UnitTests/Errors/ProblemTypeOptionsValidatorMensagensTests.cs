namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class ProblemTypeOptionsValidatorMensagensTests
{
    private const string Chave = "ProblemType:BaseUri";
    private const string Exemplo = "Ex.: https://unifesspa-edu-br.github.io/uniplus-developers/erros/";
    private const string BaseValida = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    private static ValidateOptionsResult Validar(string? baseUri, string? nome = null) =>
        new ProblemTypeOptionsValidator().Validate(nome, new ProblemTypeOptions { BaseUri = baseUri! });

    [Fact]
    public void Validate_BaseNula_RecusaComAMensagemDeObrigatorio()
    {
        Validar(baseUri: null).FailureMessage.Should().Be(
            $"{Chave} é obrigatório: sem ele o campo type do corpo de erro não aponta para o catálogo público. {Exemplo}");
    }

    [Fact]
    public void Validate_BaseComEspacosAoRedor_Aceita()
    {
        Validar($"  {BaseValida}  ").Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_CaminhoRelativo_RecusaComOValorRecebido()
    {
        Validar("/erros/").FailureMessage.Should().Be(
            $"{Chave} deve ser URI absoluta — o campo type da RFC 9457 é resolvido pelo consumidor sem contexto do servidor. Recebido: '/erros/'. {Exemplo}");
    }

    [Fact]
    public void Validate_HttpComQuery_RecusaPeloEsquemaAntesDaQuery()
    {
        Validar("http://exemplo.test/erros/?v=1").FailureMessage.Should().Be(
            $"{Chave} deve usar HTTPS. Recebido: 'http://exemplo.test/erros/?v=1'. {Exemplo}");
    }

    [Fact]
    public void Validate_HttpsComFragmento_RecusaComAMensagemDeQuery()
    {
        Validar($"{BaseValida}#topo").FailureMessage.Should().Be(
            $"{Chave} não aceita query string nem fragmento: o código do erro é concatenado ao fim do caminho. Recebido: '{BaseValida}#topo'. {Exemplo}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_NomeNuloOuVazio_ValidaComoPadrao(string? nome)
    {
        Validar(string.Empty, nome).Failed.Should().BeTrue();
    }
}
