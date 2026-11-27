namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// As recusas da fronteira de <c>GET /entidades/{entidadeTipo}/{entidadeId}/atos</c> e a ordem em que
/// saem: o binder do cursor e do limite primeiro, depois o tipo da entidade, depois a chave de continuação do cursor,
/// e só então a consulta.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class AtosDaEntidadeRecusasEndpointTests
{
    private const string EntidadeValida = "OBJETO_A";
    private const string EntidadeInvalida = "processo-seletivo";
    private const string TituloDoTipoDeEntidadeInvalido = "Tipo de entidade inválido";
    private const string DetalheDoTipoDeEntidadeInvalido =
        "O tipo da entidade deve ser um rótulo em maiúsculas, com grupos separados por sublinhado.";
    private const string TipoDeEntidadeInvalido = "uniplus.publicacoes.ato_normativo.tipo_de_entidade_invalido";
    private const string CursorInvalido = "uniplus.cursor.invalido";
    private const string TituloDoCursorInvalido = "Cursor de paginação inválido";
    private const string DetalheDoCursorInvalido = "O cursor informado é inválido.";
    private const string CursorExpirado = "uniplus.cursor.expirado";
    private const string TituloDoCursorExpirado = "Cursor de paginação expirado";
    private const string DetalheDoCursorExpirado = "O cursor informado expirou.";
    private const string LimiteInvalido = "uniplus.cursor.limit_invalido";
    private const string TituloDoLimiteInvalido = "Tamanho de página inválido";
    private const string DetalheDoLimiteInvalido = "O parâmetro 'limit' deve estar entre 1 e 100.";
    private const string DirecaoInvalida = "uniplus.cursor.direcao_invalida";
    private const string TituloDaDirecaoInvalida = "Direção de paginação inválida";
    private const string DetalheDaDirecaoInvalida = "O parâmetro 'direction' deve ser 'next' ou 'prev'.";
    private const string ChaveQueNaoEhDaConsulta = "nao-e-chave-da-consulta";
    private const string CursorDeOutraConsulta = "uniplus.paginacao.cursor_invalido";
    private const string TituloDoCursorDeOutraConsulta = "Cursor inválido";
    private const string DetalheDoCursorDeOutraConsulta = "O cursor informado não continua esta consulta. Refaça a listagem sem cursor.";
    private const string CursorAdulterado = "nao-e-um-cursor";

    private static readonly DateTimeOffset ValidoAte = new(2999, 12, 31, 0, 0, 0, TimeSpan.Zero);
    private const string ChaveDeTeste = "chave-de-continuacao";

    private readonly PublicacoesEndpointFixture _fixture;

    public AtosDaEntidadeRecusasEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Tipo de entidade fora da grafia canônica é recusado com 400 antes de consultar os atos")]
    public async Task TipoDeEntidadeInvalido_Retorna400SemConsultar()
    {
        ConsultasAnotadas consultas = new();
        await using WebApplicationFactory<HostAssemblyMarker> host = consultas.SubstituirNoHost(_fixture.Factory);
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeInvalida, Guid.CreateVersion7());

        await ConferirTipoDeEntidadeInvalidoAsync(resposta);
        consultas.Enviadas.OfType<ListarAtosDaEntidadeQuery>().Should().BeEmpty("a recusa da fronteira sai antes da consulta");
    }

    [Theory(DisplayName = "Tipo de entidade com texto de dado pessoal é recusado sem devolver o texto na resposta")]
    [InlineData("000.000.000-00")]
    [InlineData("pessoa.ficticia@exemplo.invalido")]
    [InlineData("Pessoa Ficticia da Silva")]
    public async Task TipoDeEntidadeComTextoDeDadoPessoal_NaoVoltaNaResposta(string rotulo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(client, rotulo, Guid.CreateVersion7());

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(rotulo);
        await resposta.AssertNoPiiAsync();
    }

    [Theory(DisplayName = "Tipo de entidade com quebra de linha no fim é recusado com 400, como o PostgreSQL recusaria o rótulo")]
    [InlineData("PROCESSO_SELETIVO\n")]
    [InlineData("PROCESSO_SELETIVO\r\n")]
    public async Task TipoDeEntidadeComQuebraDeLinhaFinal_Retorna400(string rotulo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(client, rotulo, Guid.CreateVersion7());

        await ConferirTipoDeEntidadeInvalidoAsync(resposta);
    }

    [Theory(DisplayName = "Chave de continuação que não é a da consulta, ainda que seja só um instante, é recusada com 400 pela conferência")]
    [InlineData(ChaveQueNaoEhDaConsulta)]
    [InlineData("2026-03-13T12:00:00.0000000+00:00")]
    public async Task ChaveQueNaoEhDaConsulta_Retorna400PelaConferencia(string chave)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid entidade = Guid.CreateVersion7();
        string cursor = await CursorDaEntidadeAsync(_fixture.Factory.Services, EntidadeValida, entidade, chave);

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeValida, entidade, ComCursor(cursor));

        await ProblemaRegistrado.ConferirAsync(
            resposta, HttpStatusCode.BadRequest, CursorDeOutraConsulta, TituloDoCursorDeOutraConsulta, DetalheDoCursorDeOutraConsulta);
    }

    [Theory(DisplayName = "Chave de continuação com texto de dado pessoal é recusada sem devolver o texto na resposta")]
    [InlineData("000.000.000-00")]
    [InlineData("pessoa.ficticia@exemplo.invalido")]
    public async Task ChaveComTextoDeDadoPessoal_NaoVoltaNaResposta(string chave)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid entidade = Guid.CreateVersion7();
        string cursor = await CursorDaEntidadeAsync(_fixture.Factory.Services, EntidadeValida, entidade, chave);

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeValida, entidade, ComCursor(cursor));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(chave);
        await resposta.AssertNoPiiAsync();
    }

    [Fact(DisplayName = "Com tipo de entidade e chave de continuação inválidos, prevalece a recusa do tipo de entidade")]
    public async Task TipoEChaveInvalidos_PrevaleceOTipoDeEntidade()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid entidade = Guid.CreateVersion7();
        string cursor = await CursorDaEntidadeAsync(_fixture.Factory.Services, EntidadeInvalida, entidade, ChaveQueNaoEhDaConsulta);

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeInvalida, entidade, ComCursor(cursor));

        await ConferirTipoDeEntidadeInvalidoAsync(resposta);
    }

    [Fact(DisplayName = "Cursor adulterado com tipo de entidade inválido: prevalece a recusa do cursor, sem consultar os atos")]
    public async Task CursorAdulteradoETipoInvalido_PrevaleceOBinder()
    {
        ConsultasAnotadas consultas = new();
        await using WebApplicationFactory<HostAssemblyMarker> host = consultas.SubstituirNoHost(_fixture.Factory);
        using HttpClient client = host.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(
            client, EntidadeInvalida, Guid.CreateVersion7(), ComCursor(CursorAdulterado));

        await ConferirCursorInvalidoAsync(resposta);
        consultas.Enviadas.OfType<ListarAtosDaEntidadeQuery>().Should().BeEmpty("a recusa da fronteira sai antes da consulta");
    }

    [Fact(DisplayName = "Cursor adulterado com tipo de entidade válido continua recusado com o código do cursor inválido")]
    public async Task CursorAdulterado_ContinuaComOCodigoDoBinder()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(
            client, EntidadeValida, Guid.CreateVersion7(), ComCursor(CursorAdulterado));

        await ConferirCursorInvalidoAsync(resposta);
    }

    [Fact(DisplayName = "Limite fora da faixa com tipo de entidade inválido: prevalece a recusa do limite")]
    public async Task LimiteForaDaFaixaETipoInvalido_PrevaleceOLimite()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeInvalida, Guid.CreateVersion7(), "?limit=0");

        await ProblemaRegistrado.ConferirAsync(
            resposta, HttpStatusCode.UnprocessableEntity, LimiteInvalido, TituloDoLimiteInvalido, DetalheDoLimiteInvalido);
    }

    [Fact(DisplayName = "Direção desconhecida com tipo de entidade inválido: prevalece a recusa da direção")]
    public async Task DirecaoDesconhecidaETipoInvalido_PrevaleceADirecao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeInvalida, Guid.CreateVersion7(), "?direction=lateral");

        await ProblemaRegistrado.ConferirAsync(
            resposta, HttpStatusCode.UnprocessableEntity, DirecaoInvalida, TituloDaDirecaoInvalida, DetalheDaDirecaoInvalida);
    }

    [Fact(DisplayName = "Limite e direção inválidos juntos: prevalece a recusa do limite")]
    public async Task LimiteEDirecaoInvalidos_PrevaleceOLimite()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ListarAsync(
            client, EntidadeValida, Guid.CreateVersion7(), "?limit=0&direction=lateral");

        await ProblemaRegistrado.ConferirAsync(
            resposta, HttpStatusCode.UnprocessableEntity, LimiteInvalido, TituloDoLimiteInvalido, DetalheDoLimiteInvalido);
    }

    [Fact(DisplayName = "Cursor expirado com tipo de entidade inválido: prevalece a recusa do cursor expirado")]
    public async Task CursorExpiradoETipoInvalido_PrevaleceOBinder()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid entidade = Guid.CreateVersion7();
        string cursor = await CursorDaEntidadeAsync(
            _fixture.Factory.Services, EntidadeInvalida, entidade, ChaveDeTeste, DateTimeOffset.UnixEpoch);

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeInvalida, entidade, ComCursor(cursor));

        await ProblemaRegistrado.ConferirAsync(
            resposta, HttpStatusCode.Gone, CursorExpirado, TituloDoCursorExpirado, DetalheDoCursorExpirado);
    }

    [Fact(DisplayName = "Cursor de outra entidade com tipo de entidade inválido: prevalece a recusa do cursor")]
    public async Task CursorDeOutraEntidadeETipoInvalido_PrevaleceOBinder()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string cursor = await CursorDaEntidadeAsync(
            _fixture.Factory.Services, EntidadeInvalida, Guid.CreateVersion7(), ChaveDeTeste);

        HttpResponseMessage resposta = await ListarAsync(
            client, EntidadeInvalida, Guid.CreateVersion7(), ComCursor(cursor));

        await ConferirCursorInvalidoAsync(resposta);
    }

    [Theory(DisplayName = "Cursor sem chave de continuação ou com ela em branco, com tipo de entidade inválido: prevalece a recusa do cursor")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CursorSemChaveETipoInvalido_PrevaleceOBinder(string? chave)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid entidade = Guid.CreateVersion7();
        string cursor = await CursorDaEntidadeAsync(_fixture.Factory.Services, EntidadeInvalida, entidade, chave);

        HttpResponseMessage resposta = await ListarAsync(client, EntidadeInvalida, entidade, ComCursor(cursor));

        await ConferirCursorInvalidoAsync(resposta);
    }

    private static async Task<string> CursorDaEntidadeAsync(
        IServiceProvider servicos, string entidadeTipo, Guid entidadeId, string? chave, DateTimeOffset? expiraEm = null)
    {
        CursorEncoder encoder = servicos.GetRequiredService<CursorEncoder>();

        return await encoder.EncodeAsync(new CursorPayload(
            After: Guid.CreateVersion7().ToString(),
            Limit: 20,
            ResourceTag: CursorResourceTag.Compose("entidade-atos", [entidadeTipo, entidadeId.ToString()]),
            ExpiresAt: expiraEm ?? ValidoAte,
            Direction: PaginationDirection.Next,
            SortKey: chave));
    }

    private static string ComCursor(string cursor) => $"?cursor={Uri.EscapeDataString(cursor)}";

    private static Task<HttpResponseMessage> ListarAsync(
        HttpClient client, string entidadeTipo, Guid entidadeId, string consulta = "") =>
        client.GetAsync(new Uri(
            $"/api/publicacoes/entidades/{Uri.EscapeDataString(entidadeTipo)}/{entidadeId}/atos{consulta}",
            UriKind.Relative));

    private static Task ConferirCursorInvalidoAsync(HttpResponseMessage resposta) =>
        ProblemaRegistrado.ConferirAsync(
            resposta, HttpStatusCode.BadRequest, CursorInvalido, TituloDoCursorInvalido, DetalheDoCursorInvalido);

    private static Task ConferirTipoDeEntidadeInvalidoAsync(HttpResponseMessage resposta) =>
        ProblemaRegistrado.ConferirAsync(
            resposta, HttpStatusCode.BadRequest, TipoDeEntidadeInvalido, TituloDoTipoDeEntidadeInvalido, DetalheDoTipoDeEntidadeInvalido);
}
