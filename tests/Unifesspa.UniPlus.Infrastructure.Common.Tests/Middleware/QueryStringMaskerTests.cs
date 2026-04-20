namespace Unifesspa.UniPlus.Infrastructure.Common.Tests.Middleware;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public class QueryStringMaskerTests
{
    [Fact]
    public void Mascarar_QueryVazia_DeveRetornarStringVazia()
    {
        QueryStringMasker masker = CriarMasker();

        string resultado = masker.Mascarar(QueryString.Empty);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Mascarar_QueryApenasComInterrogacao_DevePreservar()
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?");
    }

    [Theory]
    [InlineData("?cpf=12345678900", "?cpf=***")]
    [InlineData("?email=teste@teste.com", "?email=***")]
    [InlineData("?senha=123abc", "?senha=***")]
    [InlineData("?password=secret", "?password=***")]
    [InlineData("?token=jwt.abc.def", "?token=***")]
    [InlineData("?cpf=", "?cpf=***")]
    public void Mascarar_ParametroSensivelIsolado_DeveSubstituirValor(string entrada, string esperado)
    {
        QueryStringMasker masker = CriarMasker();

        string resultado = masker.Mascarar(new QueryString(entrada));

        resultado.Should().Be(esperado);
    }

    [Theory]
    [InlineData("CPF")]
    [InlineData("Cpf")]
    [InlineData("CpF")]
    [InlineData("EMAIL")]
    [InlineData("Email")]
    public void Mascarar_NomeSensivelComGrafiaVariada_DeveMascararCaseInsensitive(string chave)
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new($"?{chave}=valor-sensivel");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be($"?{chave}=***");
    }

    [Fact]
    public void Mascarar_ComParametrosNaoSensiveis_DevePreservarValores()
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?page=1&sort=asc");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?page=1&sort=asc");
    }

    [Fact]
    public void Mascarar_MisturaDeParametros_DeveMascararApenasSensiveis()
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?page=2&cpf=12345678900&sort=asc&email=foo@bar.com");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?page=2&cpf=***&sort=asc&email=***");
    }

    [Fact]
    public void Mascarar_ParametroSemValor_DevePreservar()
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?flag");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?flag");
    }

    [Fact]
    public void Mascarar_ValoresUrlEncoded_DevePreservarEncodingEmNaoSensiveis()
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?nome=Jos%C3%A9&cidade=S%C3%A3o%20Paulo");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?nome=Jos%C3%A9&cidade=S%C3%A3o%20Paulo");
    }

    [Fact]
    public void Mascarar_ChaveSensivelUrlEncoded_DeveReconhecerAposDecodificar()
    {
        // "%63%70%66" é "cpf" percent-encoded. A comparação precisa ignorar o
        // encoding para não deixar cliente ofuscar a chave e vazar PII.
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?%63%70%66=12345678900");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?%63%70%66=***");
    }

    [Fact]
    public void Mascarar_ParametrosSensiveisDuplicados_DeveMascararTodasOcorrencias()
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?cpf=111&cpf=222&cpf=333");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?cpf=***&cpf=***&cpf=***");
    }

    [Fact]
    public void Mascarar_AmpersandsConsecutivos_NaoDeveProduzirParSeparadorVazio()
    {
        QueryStringMasker masker = CriarMasker();
        QueryString query = new("?a=1&&b=2");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?a=1&b=2");
    }

    [Fact]
    public void Mascarar_ComNomesCustomizadosViaOpcoes_DeveAplicarConfiguracaoInjetada()
    {
        // Garante que o masker respeita a configuração injetada, não uma lista
        // hardcoded — requisito para que appsettings possa ampliar a proteção
        // sem recompilar (ex.: adicionar "matricula" em produção).
        RequestLoggingOptions opcoes = new() { NomesParametrosSensiveis = ["matricula", "rg"] };
        QueryStringMasker masker = CriarMasker(opcoes);
        QueryString query = new("?cpf=123&matricula=98765&rg=AB-12");

        string resultado = masker.Mascarar(query);

        // cpf não está mais na lista customizada — preserva. matricula e rg são mascarados.
        resultado.Should().Be("?cpf=123&matricula=***&rg=***");
    }

    [Fact]
    public void Mascarar_ComValorMascaradoCustomizado_DeveUsarMascaraInjetada()
    {
        RequestLoggingOptions opcoes = new() { ValorMascarado = "[REDACTED]" };
        QueryStringMasker masker = CriarMasker(opcoes);
        QueryString query = new("?cpf=12345678900");

        string resultado = masker.Mascarar(query);

        resultado.Should().Be("?cpf=[REDACTED]");
    }

    [Fact]
    public void Construtor_ComOptionsNulas_DeveLancarArgumentNullException()
    {
        Func<QueryStringMasker> acao = () => new QueryStringMasker(null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    private static QueryStringMasker CriarMasker(RequestLoggingOptions? opcoes = null)
    {
        opcoes ??= new RequestLoggingOptions();
        return new QueryStringMasker(Options.Create(opcoes));
    }
}
