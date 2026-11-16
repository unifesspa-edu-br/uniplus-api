namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>Rota, fixture e montagem de requisições comuns aos testes do contrato HTTP da manutenção de fatos.</summary>
public abstract class FatoCandidatoAdminEndpointTestBase
{
    protected const string Base = "/api/configuracao/admin/fatos-candidato";
    protected static readonly Guid IdDeCorRaca = Guid.Parse("fa700000-0000-7000-8000-000000000001");

    protected ConfiguracaoEndpointFixture Fixture { get; }

    protected FatoCandidatoAdminEndpointTestBase(ConfiguracaoEndpointFixture fixture)
    {
        Fixture = fixture;
    }

    protected static object DefinicaoBooleana() => new
    {
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "SENSIVEL",
        finalidadeTratamento = "Enquadramento na reserva de vagas.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object DefinicaoCategorica() => new
    {
        dominio = "CATEGORICO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object FatoBooleano(string codigo) => new
    {
        codigo,
        nome = "Vínculo com outra instituição pública",
        descricao = "Descrição do fato.",
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        pontoResolucao = "HABILITACAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    protected static async Task<JsonDocument> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    protected static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo)
    {
        ArgumentNullException.ThrowIfNull(client);
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        return await client.SendAsync(request);
    }
}
