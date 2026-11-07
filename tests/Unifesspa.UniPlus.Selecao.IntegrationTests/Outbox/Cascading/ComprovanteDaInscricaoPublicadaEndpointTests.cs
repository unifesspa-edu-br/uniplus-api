namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O comprovante de uma inscrição de processo publicado pela borda HTTP: o 200 com o corpo no contrato e o envio repetido com a
/// mesma <c>Idempotency-Key</c>, que devolve o mesmo recibo e emite um só comprovante.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ComprovanteDaInscricaoPublicadaEndpointTests
{
    private const string Media = "application/vnd.uniplus.comprovante-inscricao.v1+json";
    private const string Dono = "sub-comprovante-publicado";

    private readonly CascadingFixture _fixture;

    public ComprovanteDaInscricaoPublicadaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O dono lê o comprovante da inscrição enviada na versão publicada: 200, media type e corpo no contrato")]
    public async Task Dono_LeOComprovante_Responde200ComOCorpoDoContrato()
    {
        (Guid processoId, Guid versaoId) = await PublicarAsync(fimDasInscricoes: null);
        Guid inscricaoId = await SemearEnviadaAsync(processoId, versaoId);

        using HttpResponseMessage resposta = await ObterAsync(inscricaoId, 1);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.comprovante-inscricao.v1+json");
        using JsonDocument json = JsonDocument.Parse(corpo);
        JsonElement raiz = json.RootElement;
        raiz.GetProperty("inscricaoId").GetGuid().Should().Be(inscricaoId);
        (raiz.GetProperty("numero").GetInt32(), raiz.GetProperty("total").GetInt32(), raiz.GetProperty("vale").GetBoolean()).Should().Be((1, 1, true));
        raiz.GetProperty("processoSeletivoId").GetGuid().Should().Be(processoId);
        raiz.GetProperty("envio").GetProperty("numeroInscricao").GetString().Should().Be("000321");
        raiz.GetProperty("envio").GetProperty("fusoHorario").GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        raiz.GetProperty("candidato").GetProperty("cpfMascarado").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        raiz.GetProperty("candidato").GetProperty("nomeCivil").GetString().Should().Be(PersonasDeTeste.NomeFernandaDrumond);
        foreach (string lista in new[] { "opcoesDeCurso", "secoes", "derivados", "termosAceitos", "documentos" })
        {
            raiz.GetProperty(lista).ValueKind.Should().Be(JsonValueKind.Array, $"{lista} é obrigatório no contrato");
        }

        raiz.GetProperty("numeroDeOpcoesDeCurso").GetInt32().Should().BeGreaterThan(0);
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
    }

    [Fact(DisplayName = "Repetir o envio com a mesma Idempotency-Key e o mesmo corpo devolve o mesmo recibo e deixa um só comprovante")]
    public async Task EnvioRepetidoComAMesmaChave_DevolveOMesmoReciboEUmSoComprovante()
    {
        (Guid processoId, _) = await PublicarAsync(fimDasInscricoes: new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero));
        using HttpResponseMessage iniciada = await EnviarAsync(HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/inscricao", "inscricao", null, null, null);
        JsonElement rascunho = await iniciada.Content.ReadFromJsonAsync<JsonElement>();
        iniciada.StatusCode.Should().Be(HttpStatusCode.OK, rascunho.ToString());
        Guid inscricaoId = rascunho.GetProperty("id").GetGuid();
        object pedido = PedidoDeEnvio(rascunho);
        string chave = Guid.CreateVersion7().ToString("N");
        string etag = rascunho.GetProperty("eTag").GetString()!;

        using HttpResponseMessage primeira = await EnviarAsync(HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/inscricao/submissao", "recibo-da-inscricao", pedido, etag, chave);
        using HttpResponseMessage repetida = await EnviarAsync(HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/inscricao/submissao", "recibo-da-inscricao", pedido, etag, chave);

        string reciboDaPrimeira = await primeira.Content.ReadAsStringAsync();
        primeira.StatusCode.Should().Be(HttpStatusCode.OK, reciboDaPrimeira);
        repetida.StatusCode.Should().Be(HttpStatusCode.OK);
        (await repetida.Content.ReadAsStringAsync()).Should().Be(reciboDaPrimeira);
        JsonElement recibo = JsonDocument.Parse(reciboDaPrimeira).RootElement;
        recibo.GetProperty("inscricaoId").GetGuid().Should().Be(inscricaoId);
        recibo.GetProperty("numeroDoComprovante").GetInt32().Should().Be(1);
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        (await escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>().ObterEmissoesAsync([inscricaoId])).Should().HaveCount(1);
    }

    [Fact(DisplayName = "O candidato percorre o ciclo da inscrição pela borda: lê, avalia, salva, envia, cancela e descarta o novo rascunho")]
    public async Task CicloDaInscricao_PercorridoPelaBorda()
    {
        (Guid processoId, _) = await PublicarAsync(fimDasInscricoes: new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero));
        string rota = $"/api/selecao/processos-seletivos/{processoId}/inscricao";
        JsonElement rascunho = await LerAsync(await EnviarAsync(HttpMethod.Post, rota, "inscricao", null, null, null));
        object pedido = PedidoDeEnvio(rascunho);

        (await EnviarAsync(HttpMethod.Get, rota, "inscricao", null, null, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnviarAsync(HttpMethod.Get, $"{rota}/formulario", "formulario-inscricao", null, null, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnviarAsync(HttpMethod.Post, $"{rota}/avaliacoes", "avaliacao-da-inscricao", pedido, null, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement salvo = await LerAsync(await EnviarAsync(HttpMethod.Put, rota, "inscricao", pedido, rascunho.GetProperty("eTag").GetString(), null));
        using HttpResponseMessage semPrecondicao = await EnviarAsync(HttpMethod.Post, $"{rota}/submissao", "recibo-da-inscricao", pedido, null, null);
        semPrecondicao.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        (await EnviarAsync(HttpMethod.Post, $"{rota}/submissao", "recibo-da-inscricao", pedido, salvo.GetProperty("eTag").GetString(), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement enviada = await LerAsync(await EnviarAsync(HttpMethod.Get, rota, "inscricao", null, null, null));
        using HttpResponseMessage cancelada = await EnviarAsync(HttpMethod.Post, $"{rota}/cancelamento", "inscricao", null, enviada.GetProperty("eTag").GetString(), null);
        cancelada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement novo = await LerAsync(await EnviarAsync(HttpMethod.Post, rota, "inscricao", null, null, null));
        using HttpResponseMessage descartada = await EnviarAsync(HttpMethod.Delete, rota, "inscricao", null, novo.GetProperty("eTag").GetString(), null);
        descartada.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static object PedidoDeEnvio(JsonElement rascunho) => new
    {
        ofertaCursoId = rascunho.GetProperty("ofertas")[0].GetProperty("ofertaCursoOrigemId").GetGuid(),
        respostas = RespostasDoEnvio(),
        grupos = new { },
        aceites = Array.Empty<object>(),
    };

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta)
    {
        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo.ToString());
        resposta.Dispose();
        return corpo;
    }

    private static Dictionary<string, object?> RespostasDoEnvio() => new(StringComparer.Ordinal)
    {
        ["NOME"] = PersonasDeTeste.NomeFernandaDrumond,
        ["DESEJA_NOME_SOCIAL"] = false,
        ["NACIONALIDADE"] = "NATO",
        ["CPF"] = PersonasDeTeste.FernandaDrumond.Valor,
        ["RG_NUMERO"] = "1234567",
        ["RG_ORGAO_EMISSOR"] = "SSP",
        ["RG_UF"] = "PA",
        ["RG_DATA_EMISSAO"] = "2010-01-01",
        ["DATA_NASCIMENTO"] = "2000-01-01",
        ["NATURALIDADE_UF"] = "PA",
        ["NATURALIDADE_MUNICIPIO"] = "1504208",
        ["NOME_MAE"] = "Maria Drumond",
        ["ESTADO_CIVIL"] = "SOLTEIRO",
        ["SEXO"] = "FEMININO",
        ["SEXO_CIVIL"] = "FEMININO",
        ["IDENTIDADE_GENERO"] = "PREFIRO_NAO_INFORMAR",
        ["ORIENTACAO_SEXUAL"] = "PREFIRO_NAO_INFORMAR",
        ["COR_RACA"] = "NAO_INFORMADO",
        ["EMAIL"] = "fernanda.drumond@example.com",
        ["TELEFONE"] = "94999999999",
        ["TIPO_ENDERECO"] = "URBANO",
        ["ENDERECO_RESIDENCIAL"] = new { cep = "68500000", cidade = new { codigoIbge = "1504208", uf = "PA" } },
    };

    private async Task<(Guid ProcessoId, Guid VersaoId)> PublicarAsync(DateTimeOffset? fimDasInscricoes)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client, fimDasInscricoes: fimDasInscricoes);
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        Guid versaoId = await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).Select(v => v.Id).SingleAsync();

        return (processoId, versaoId);
    }

    private async Task<Guid> SemearEnviadaAsync(Guid processoId, Guid versaoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), processoId, PersonasDeTeste.FernandaDrumond, Dono, TestAuthHandler.DefaultIssuer, SituacaoInscricao.Submetida, versaoId, null, null,
            "000321", null, null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CPF"] = JsonSerializer.SerializeToElement(PersonasDeTeste.FernandaDrumond.Valor) },
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
            {
                ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
            },
            revisao: 1, DateTimeOffset.UtcNow, null);
        IInscricaoRepository repositorio = escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>();
        await repositorio.AdicionarAsync(inscricao);
        await repositorio.AdicionarComprovanteAsync(ComprovanteInscricao.Emitir(
            inscricao.Id, "000321", DateTimeOffset.UtcNow, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
                ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
            },
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], []));
        await escopo.ServiceProvider.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();

        return inscricao.Id;
    }

    private Task<HttpResponseMessage> ObterAsync(Guid inscricaoId, int numero) =>
        EnviarAsync(HttpMethod.Get, $"/api/selecao/inscricoes/{inscricaoId}/comprovantes/{numero}", "comprovante-inscricao", null, null, null);

    private WebApplicationFactory<HostAssemblyMarker> HostComGeoQueConfirma() =>
        _fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConferenciaEnderecoGeo>();
            services.AddSingleton<IConferenciaEnderecoGeo>(new FakeConferenciaEnderecoGeo()
                .ComMunicipio("1504208", "PA", "Marabá")
                .ComCep("68500000", "1504208", "PA"));
        }));

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string rota, string recurso, object? corpo, string? ifMatch, string? chave)
    {
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeoQueConfirma();
        using HttpClient client = host.CreateClient();
        using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Dono);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, PersonasDeTeste.FernandaDrumond.Valor);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.NameHeader, PersonasDeTeste.NomeFernandaDrumond);
        request.Headers.TryAddWithoutValidation("Accept", $"application/vnd.uniplus.{recurso}.v1+json");
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", chave ?? Guid.CreateVersion7().ToString("N"));
        }

        return await client.SendAsync(request);
    }
}
