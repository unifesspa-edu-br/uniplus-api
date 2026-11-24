namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCenarioDeIndicadores;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class TestesDaConsultaDosIndicadoresDoProcesso : IAsyncLifetime
{
    private readonly ApoioDoCenarioDeIndicadores _apoio;
    private readonly InscricoesDoCenarioDeIndicadores _inscricoes;

    public TestesDaConsultaDosIndicadoresDoProcesso(CascadingFixture cenario)
    {
        _apoio = new ApoioDoCenarioDeIndicadores(cenario);
        _inscricoes = new InscricoesDoCenarioDeIndicadores(cenario, _apoio);
    }

    public Task InitializeAsync() => _apoio.LimparConcessoesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Consulta de indicadores retorna contrato ordenado, valores zero e links sem armazenamento")]
    public async Task PublicadoSemInscricoes_RetornaContratoV1()
    {
        Guid processoId = await _apoio.SemearPublicadoAsync(nameof(PublicadoSemInscricoes_RetornaContratoV1));
        await _apoio.ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        DateTimeOffset antes = DateTimeOffset.UtcNow;
        using HttpResponseMessage resposta = await _apoio.EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);
        DateTimeOffset depois = DateTimeOffset.UtcNow;

        await ConferirRespostaEArmazenamentoAsync(resposta);
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = documento.RootElement;
        ConferirIdentidadeEInstante(raiz, processoId, antes, depois);
        ConferirIndicadoresSemInscricoes(raiz);
        ConferirLinksEPrivacidade(raiz, processoId);
    }

    [Fact(DisplayName = "Consulta de indicadores exige autenticação, negociação v1 e autorização na unidade dona")]
    public async Task AutenticacaoNegociacaoEEscopo_SeguemContrato()
    {
        Guid processoId = await _apoio.SemearPublicadoAsync(nameof(AutenticacaoNegociacaoEEscopo_SeguemContrato));
        await _apoio.ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        await ConferirAutenticacaoENegociacaoAsync(processoId);
        await ConferirEscopoDaConcessaoAsync(processoId);
    }

    private async Task ConferirAutenticacaoENegociacaoAsync(Guid processoId)
    {
        string rota = $"{Rota}/{processoId}/indicadores";
        using HttpResponseMessage anonimo = await _apoio.EnviarAsync(null, rota, MediaType);
        using HttpResponseMessage autorizado = await _apoio.EnviarAsync(Pessoa, rota, MediaType);
        using HttpResponseMessage tipoNaoAceito = await _apoio.EnviarAsync(Pessoa, rota, "application/vnd.uniplus.indicadores-do-processo.v9+json");
        ConferirRespostasDeAutenticacaoEContrato(anonimo, autorizado, tipoNaoAceito);
    }

    private async Task ConferirEscopoDaConcessaoAsync(Guid processoId)
    {
        Guid outraUnidade = Guid.CreateVersion7();
        await _apoio.ConcederNaUnidadeAsync("indicadores.outra.unidade", outraUnidade, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        string rota = $"{Rota}/{processoId}/indicadores";
        using HttpResponseMessage semConcessao = await _apoio.EnviarAsync("indicadores.sem.concessao", rota, MediaType);
        using HttpResponseMessage papelSemConcessao = await _apoio.EnviarAsync(
            "indicadores.admin", rota, MediaType, new(Papel: "plataforma-admin"));
        using HttpResponseMessage respostaDeOutraUnidade = await _apoio.EnviarAsync("indicadores.outra.unidade", rota, MediaType);
        ConferirRespostasDeEscopo(semConcessao, papelSemConcessao, respostaDeOutraUnidade);
    }

    [Fact(DisplayName = "Rascunho é visível com permissão de configurar e ocultado de quem só acompanha")]
    public async Task Rascunho_DiferenciaConfigurarDeAcompanhar()
    {
        Guid processoId = await _apoio.SemearRascunhoAsync(nameof(Rascunho_DiferenciaConfigurarDeAcompanhar));
        await _apoio.ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await _apoio.ConcederAsync("indicadores.somente.acompanhar", processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);

        using HttpResponseMessage configurador = await _apoio.EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage acompanha = await _apoio.EnviarAsync("indicadores.somente.acompanhar", $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage inexistente = await _apoio.EnviarAsync("indicadores.somente.acompanhar", $"{Rota}/{Guid.CreateVersion7()}/indicadores", MediaType);

        await ConferirRascunhoAutorizadoAsync(configurador);
        await ConferirErrosIndistinguiveisAsync(acompanha, inexistente);
    }

    [Fact(DisplayName = "Inscrição submetida conta uma vez; cancelamento, rascunho e comprovantes não criam indicadores extras")]
    public async Task Contagens_ConsideramSomenteEstadosAtuaisDaInscricao()
    {
        Guid processoId = await _apoio.SemearPublicadoAsync(
            nameof(Contagens_ConsideramSomenteEstadosAtuaisDaInscricao),
            new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero));
        await _inscricoes.SemearInscricoesAsync(processoId);
        await _inscricoes.DescartarRascunhoPelaApiAsync(processoId);
        await _apoio.ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);

        using HttpResponseMessage resposta = await _apoio.EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);

        await ConferirContagensAsync(resposta, 2L, 2L, 2L);
    }

    [Fact(DisplayName = "Servidor com inscrição submetida não consulta indicadores administrativos")]
    public async Task ServidorInscrito_Retorna403()
    {
        Guid processoId = await _apoio.SemearPublicadoAsync(nameof(ServidorInscrito_Retorna403));
        await _apoio.ConcederAsync(SubInscrito, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        await _inscricoes.SemearInscricaoAsync(processoId, PersonasDeTeste.FernandaDrumond, SubInscrito, SituacaoInscricao.Submetida);

        using HttpResponseMessage resposta = await _apoio.EnviarAsync(SubInscrito, $"{Rota}/{processoId}/indicadores", MediaType);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("impedido_por_inscricao_no_processo");
    }

    private static async Task ConferirRespostaEArmazenamentoAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(MediaType);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    private static void ConferirIdentidadeEInstante(JsonElement raiz, Guid processoId, DateTimeOffset antes, DateTimeOffset depois)
    {
        Propriedades(raiz).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        raiz.GetProperty("processoSeletivoId").GetGuid().Should().Be(processoId);
        DateTimeOffset consultadoEm = raiz.GetProperty("consultadoEm").GetDateTimeOffset();
        consultadoEm.Offset.Should().Be(TimeSpan.Zero);
        consultadoEm.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }

    private static void ConferirIndicadoresSemInscricoes(JsonElement raiz)
    {
        JsonElement[] indicadores = [.. raiz.GetProperty("indicadores").EnumerateArray()];
        indicadores.Select(i => i.GetProperty("codigo").GetString()).Should().Equal("INSCRICOES", "INSCRITOS", "RASCUNHOS");
        indicadores.Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(0L, 0L, 0L);
        indicadores.Select(i => i.GetProperty("rotulo").GetString()).Should().Equal(
            "Inscrições", "Inscritos", "Rascunhos");
        indicadores.Select(i => i.GetProperty("descricao").GetString()).Should().Equal(
            "Submetidas e não canceladas", "Pessoas distintas com inscrição submetida e não cancelada", "Não enviados");
        indicadores.Should().AllSatisfy(item =>
            Propriedades(item).Should().BeEquivalentTo(["codigo", "valor", "rotulo", "descricao"]));
    }

    private static void ConferirLinksEPrivacidade(JsonElement raiz, Guid processoId)
    {
        raiz.TryGetProperty("_links", out JsonElement links).Should().BeTrue(raiz.ToString());
        Propriedades(links).Should().BeEquivalentTo(["self", "processo"]);
        links.GetProperty("self").GetString().Should().Be($"{Rota}/{processoId}/indicadores");
        links.GetProperty("processo").GetString().Should().Be($"{Rota}/{processoId}");
        raiz.ToString().Should().NotContain("cpf").And.NotContain("\"sub\"").And.NotContain("nome").And.NotContain("email");
    }

    private static void ConferirRespostasDeAutenticacaoEContrato(
        HttpResponseMessage anonimo,
        HttpResponseMessage autorizado,
        HttpResponseMessage tipoNaoAceito)
    {
        anonimo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        autorizado.StatusCode.Should().Be(HttpStatusCode.OK);
        tipoNaoAceito.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    private static void ConferirRespostasDeEscopo(
        HttpResponseMessage semConcessao,
        HttpResponseMessage papelSemConcessao,
        HttpResponseMessage outraUnidade)
    {
        semConcessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        papelSemConcessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        outraUnidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task ConferirRascunhoAutorizadoAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("indicadores").EnumerateArray()
            .Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(0L, 0L, 0L);
    }

    private static string[] Propriedades(JsonElement objeto) =>
        objeto.EnumerateObject().Select(p => p.Name).ToArray();

    private static async Task ConferirErrosIndistinguiveisAsync(HttpResponseMessage rascunho, HttpResponseMessage inexistente)
    {
        rascunho.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(rascunho.StatusCode);
        JsonObject corpoDoRascunho = JsonNode.Parse(await rascunho.Content.ReadAsStringAsync())!.AsObject();
        JsonObject corpoDoInexistente = JsonNode.Parse(await inexistente.Content.ReadAsStringAsync())!.AsObject();
        corpoDoRascunho.Remove("instance");
        corpoDoRascunho.Remove("traceId");
        corpoDoInexistente.Remove("instance");
        corpoDoInexistente.Remove("traceId");
        JsonNode.DeepEquals(corpoDoRascunho, corpoDoInexistente).Should().BeTrue();
        CabecalhosEstaveis(rascunho.Headers, rascunho.Content.Headers)
            .Should().BeEquivalentTo(CabecalhosEstaveis(inexistente.Headers, inexistente.Content.Headers));
    }

    private static Dictionary<string, string[]> CabecalhosEstaveis(HttpHeaders resposta, HttpHeaders conteudo) =>
        resposta.Concat(conteudo)
            .Where(cabecalho => !new[] { "Date", "X-Request-Id", "X-Correlation-Id", "traceparent" }
                .Contains(cabecalho.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(cabecalho => cabecalho.Key, cabecalho => cabecalho.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
}
