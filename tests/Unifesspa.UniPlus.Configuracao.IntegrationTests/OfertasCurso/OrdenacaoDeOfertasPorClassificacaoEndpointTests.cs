namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A paginação por cursor da listagem de ofertas quando a ordem é o programa, o formato ou um dos regimes: o cursor leva o
/// código do campo, e a continuação o lê de volta, sozinho ou junto de outro campo, nos dois sentidos.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OrdenacaoDeOfertasPorClassificacaoEndpointTests
{
    private const int OfertasPorPagina = 2;

    private static readonly ClassificacaoDaOferta[] OfertasSemeadas =
    [
        new("REGULAR", "EAD", "EXTENSIVO", "REGULAR"),
        new("REGULAR", "PRESENCIAL", "INTENSIVO", "INTEGRAL"),
        new("PARFOR", "PRESENCIAL", "ALTERNANCIA_PEDAGOGICA", "REGULAR"),
        new("PARFOR", "EAD", "EXTENSIVO", "INTEGRAL"),
        new("PRONERA", "SEMIPRESENCIAL", "INTENSIVO", "INTEGRAL"),
        new("PRONERA", "PRESENCIAL", "ALTERNANCIA_PEDAGOGICA", "INTEGRAL"),
        new("FORMA_PARA", "EAD", "EXTENSIVO", "REGULAR"),
    ];

    private static readonly Dictionary<string, Func<ClassificacaoDaOferta, string>> ValorPorCampo = new(StringComparer.Ordinal)
    {
        ["programaDeOferta"] = oferta => oferta.Programa,
        ["formatoPedagogico"] = oferta => oferta.Formato,
        ["regimeDeFuncionamento"] = oferta => oferta.Funcionamento,
        ["regimeDeTurno"] = oferta => oferta.RegimeDeTurno,
    };

    private readonly ConfiguracaoEndpointFixture _fixture;

    public OrdenacaoDeOfertasPorClassificacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "A travessia por rel=\"next\" ordenada por programa, formato ou regime mantém a ordem e não repete nem omite oferta")]
    [InlineData("programaDeOferta")]
    [InlineData("-programaDeOferta")]
    [InlineData("formatoPedagogico")]
    [InlineData("-formatoPedagogico")]
    [InlineData("programaDeOferta,-formatoPedagogico")]
    [InlineData("-programaDeOferta,formatoPedagogico")]
    [InlineData("formatoPedagogico,-programaDeOferta")]
    [InlineData("-formatoPedagogico,programaDeOferta")]
    [InlineData("regimeDeFuncionamento")]
    [InlineData("-regimeDeFuncionamento")]
    [InlineData("regimeDeTurno")]
    [InlineData("-regimeDeTurno")]
    [InlineData("regimeDeFuncionamento,-regimeDeTurno")]
    [InlineData("-regimeDeFuncionamento,regimeDeTurno")]
    [InlineData("regimeDeTurno,-programaDeOferta")]
    [InlineData("-regimeDeFuncionamento,formatoPedagogico")]
    public Task Listar_OrdenadoPorClassificacao_PercorreTodasAsPaginasNaOrdem(string ordenacao)
    {
        ArgumentNullException.ThrowIfNull(ordenacao);

        return ConferirTravessiaAsync(ordenacao);
    }

    private async Task ConferirTravessiaAsync(string ordenacao)
    {
        Guid cursoId = await SemearOfertasDoCursoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        IReadOnlyList<JsonElement> percorridas = await PercorrerAsync(client, cursoId, ordenacao);

        string[] camposOrdenados = [.. ordenacao.Split(',').Select(c => c.TrimStart('-'))];
        percorridas.Select(ClassificacaoDe).Select(o => Projetar(o, camposOrdenados))
            .Should().Equal(OrdemEsperada(ordenacao).Select(o => Projetar(o, camposOrdenados)));
        percorridas.Select(o => o.GetProperty("id").GetGuid()).Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "Cursor emitido sob a ordenação por programa não é aceito sob a ordenação por formato")]
    public async Task Listar_CursorDeOutraOrdenacao_Retorna400()
    {
        Guid cursoId = await SemearOfertasDoCursoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"{ApoioDaOrdenacaoAlfabetica.OfertasCurso}?cursoId={cursoId}&limit={OfertasPorPagina}&sort=programaDeOferta", UriKind.Relative));
        string proxima = ApoioDaOrdenacaoAlfabetica.LinkRel(primeira, "next").Should().NotBeNull().And.Subject;

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri(proxima.Replace("sort=programaDeOferta", "sort=formatoPedagogico", StringComparison.Ordinal), UriKind.RelativeOrAbsolute));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<IReadOnlyList<JsonElement>> PercorrerAsync(HttpClient client, Guid cursoId, string ordenacao)
    {
        List<JsonElement> ofertas = [];
        string? url = $"{ApoioDaOrdenacaoAlfabetica.OfertasCurso}?cursoId={cursoId}&limit={OfertasPorPagina}&sort={ordenacao}";
        while (url is not null)
        {
            using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            ofertas.AddRange(await ApoioDaOrdenacaoAlfabetica.LerItensAsync(resposta));
            url = ApoioDaOrdenacaoAlfabetica.LinkRel(resposta, "next");
        }

        return ofertas;
    }

    private static ClassificacaoDaOferta ClassificacaoDe(JsonElement oferta) =>
        new(CodigoDe(oferta, "programaDeOferta"), CodigoDe(oferta, "formatoPedagogico"), CodigoDe(oferta, "regimeDeFuncionamento"), CodigoDe(oferta, "regimeDeTurno"));

    private static string CodigoDe(JsonElement oferta, string campo) =>
        oferta.GetProperty(campo).GetProperty("codigo").GetString() ?? string.Empty;

    // Dentro de um empate a paginação por chave desempata pelo Id, que não segue a ordem de criação: só os campos ordenados são comparados.
    private static string Projetar(ClassificacaoDaOferta oferta, string[] camposOrdenados) =>
        string.Join('|', camposOrdenados.Select(campo => ValorPorCampo[campo](oferta)));

    private static IEnumerable<ClassificacaoDaOferta> OrdemEsperada(string ordenacao)
    {
        string[] campos = ordenacao.Split(',');
        IOrderedEnumerable<ClassificacaoDaOferta> ordenadas = Ordenar(OfertasSemeadas, campos[0]);

        return campos.Skip(1).Aggregate(ordenadas, Desempatar);
    }

    private static IOrderedEnumerable<ClassificacaoDaOferta> Ordenar(IEnumerable<ClassificacaoDaOferta> ofertas, string campo) =>
        campo.StartsWith('-')
            ? ofertas.OrderByDescending(ValorPorCampo[campo[1..]], StringComparer.Ordinal)
            : ofertas.OrderBy(ValorPorCampo[campo], StringComparer.Ordinal);

    private static IOrderedEnumerable<ClassificacaoDaOferta> Desempatar(IOrderedEnumerable<ClassificacaoDaOferta> ofertas, string campo) =>
        campo.StartsWith('-')
            ? ofertas.ThenByDescending(ValorPorCampo[campo[1..]], StringComparer.Ordinal)
            : ofertas.ThenBy(ValorPorCampo[campo], StringComparer.Ordinal);

    private async Task<Guid> SemearOfertasDoCursoAsync()
    {
        Curso curso = Curso.Criar($"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}", "Engenharia Florestal", "Bacharelado", "Graduação", null).ValorEsperado();
        LocalOferta local = LocalOferta.Criar(
            new("CAMPUS_SEDE", null, "1504208", "Marabá", "PA", null, null),
            DateTimeOffset.UtcNow).ValorEsperado();

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        dbContext.AddRange(curso, local);
        dbContext.AddRange(OfertasSemeadas.Select(o => NovaOferta(curso.Id, local.Id, o)));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return curso.Id;
    }

    private static OfertaCurso NovaOferta(Guid cursoId, Guid localId, ClassificacaoDaOferta classificacao) =>
        OfertaCurso.Criar(
            cursoId,
            localId,
            UnidadeOfertante.Criar(Guid.CreateVersion7(), "ICH", "Instituto de Ciências Humanas", "CENTRO").ValorEsperado(),
            new DeclaracaoDaOfertaDeCurso(
                $"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), classificacao.Programa, classificacao.Formato,
                classificacao.Funcionamento, classificacao.RegimeDeTurno, TurnosDoRegime(classificacao.RegimeDeTurno),
                BaseLegal: "Lei de teste")).ValorEsperado();

    private static string[] TurnosDoRegime(string regime) =>
        regime == "INTEGRAL" ? ["MATUTINO", "VESPERTINO"] : ["MATUTINO"];

    private sealed record ClassificacaoDaOferta(string Programa, string Formato, string Funcionamento, string RegimeDeTurno);
}
