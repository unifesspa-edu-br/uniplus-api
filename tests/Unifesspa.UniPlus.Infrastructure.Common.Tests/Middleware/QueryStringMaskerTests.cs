namespace Unifesspa.UniPlus.Infrastructure.Common.Tests.Middleware;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public class QueryStringMaskerTests
{
    [Fact]
    public void Mascarar_QueryVazia_DeveRetornarStringVazia()
    {
        string resultado = QueryStringMasker.Mascarar(QueryString.Empty);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Mascarar_QueryApenasComInterrogacao_DevePreservar()
    {
        QueryString query = new("?");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?");
    }

    [Theory]
    [InlineData("?cpf=12345678900", "?cpf=***")]
    [InlineData("?email=teste@teste.com", "?email=***")]
    [InlineData("?senha=123abc", "?senha=***")]
    [InlineData("?password=secret", "?password=***")]
    [InlineData("?token=jwt.abc.def", "?token=***")]
    public void Mascarar_ParametroSensivelIsolado_DeveSubstituirValor(string entrada, string esperado)
    {
        string resultado = QueryStringMasker.Mascarar(new QueryString(entrada));

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
        QueryString query = new($"?{chave}=valor-sensivel");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be($"?{chave}=***");
    }

    [Fact]
    public void Mascarar_ComParametrosNaoSensiveis_DevePreservarValores()
    {
        QueryString query = new("?page=1&sort=asc");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?page=1&sort=asc");
    }

    [Fact]
    public void Mascarar_MisturaDeParametros_DeveMascararApenasSensiveis()
    {
        QueryString query = new("?page=2&cpf=12345678900&sort=asc&email=foo@bar.com");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?page=2&cpf=***&sort=asc&email=***");
    }

    [Fact]
    public void Mascarar_ParametroSemValor_DevePreservar()
    {
        QueryString query = new("?flag");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?flag");
    }

    [Fact]
    public void Mascarar_ValoresUrlEncoded_DevePreservarEncodingEmNaoSensiveis()
    {
        QueryString query = new("?nome=Jos%C3%A9&cidade=S%C3%A3o%20Paulo");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?nome=Jos%C3%A9&cidade=S%C3%A3o%20Paulo");
    }

    [Fact]
    public void Mascarar_ChaveSensivelUrlEncoded_DeveReconhecerAposDecodificar()
    {
        // "%63%70%66" é "cpf" percent-encoded. A comparação precisa ignorar o
        // encoding para não deixar cliente ofuscar a chave e vazar PII.
        QueryString query = new("?%63%70%66=12345678900");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?%63%70%66=***");
    }

    [Fact]
    public void Mascarar_ParametrosSensiveisDuplicados_DeveMascararTodasOcorrencias()
    {
        QueryString query = new("?cpf=111&cpf=222&cpf=333");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?cpf=***&cpf=***&cpf=***");
    }

    [Fact]
    public void Mascarar_AmpersandsConsecutivos_NaoDeveProduzirParSeparadorVazio()
    {
        QueryString query = new("?a=1&&b=2");

        string resultado = QueryStringMasker.Mascarar(query);

        resultado.Should().Be("?a=1&b=2");
    }
}
