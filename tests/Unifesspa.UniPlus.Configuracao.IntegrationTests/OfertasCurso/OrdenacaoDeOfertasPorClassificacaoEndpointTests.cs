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
/// A paginação por cursor da listagem de ofertas quando a ordem é o programa ou o formato: o cursor leva o código do
/// campo, e a continuação o lê de volta, sozinho ou junto de outro campo, nos dois sentidos.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OrdenacaoDeOfertasPorClassificacaoEndpointTests
{
    private const int OfertasPorPagina = 2;

    private static readonly (string Programa, string Formato)[] OfertasSemeadas =
    [
        ("REGULAR", "EAD"),
        ("REGULAR", "PRESENCIAL"),
        ("PARFOR", "PRESENCIAL"),
        ("PARFOR", "EAD"),
        ("PRONERA", "SEMIPRESENCIAL"),
        ("PRONERA", "PRESENCIAL"),
        ("FORMA_PARA", "EAD"),
    ];

    private readonly ConfiguracaoEndpointFixture _fixture;

    public OrdenacaoDeOfertasPorClassificacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "A travessia por rel=\"next\" ordenada por programa ou formato mantém a ordem e não repete nem omite oferta")]
    [InlineData("programaDeOferta")]
    [InlineData("-programaDeOferta")]
    [InlineData("formatoPedagogico")]
    [InlineData("-formatoPedagogico")]
    [InlineData("programaDeOferta,-formatoPedagogico")]
    [InlineData("-programaDeOferta,formatoPedagogico")]
    [InlineData("formatoPedagogico,-programaDeOferta")]
    [InlineData("-formatoPedagogico,programaDeOferta")]
    public Task Listar_OrdenadoPorClassificacao_PercorreTodasAsPaginasNaOrdem(string ordenacao) =>
        ConferirTravessiaAsync(ordenacao);

    private async Task ConferirTravessiaAsync(string ordenacao)
    {
        Guid cursoId = await SemearOfertasDoCursoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        IReadOnlyList<JsonElement> percorridas = await PercorrerAsync(client, cursoId, ordenacao);

        percorridas.Select(ClassificacaoDe).Select(o => Ordenados(o, ordenacao))
            .Should().Equal(OrdemEsperada(ordenacao).Select(o => Ordenados(o, ordenacao)));
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

    private static (string Programa, string Formato) ClassificacaoDe(JsonElement oferta) =>
        (oferta.GetProperty("programaDeOferta").GetProperty("codigo").GetString() ?? string.Empty,
         oferta.GetProperty("formatoPedagogico").GetProperty("codigo").GetString() ?? string.Empty);

    // Dentro de um empate o motor desempata pelo Id, que não segue a ordem de criação: só os campos ordenados são comparados.
    private static (string Programa, string Formato) Ordenados((string Programa, string Formato) oferta, string ordenacao) =>
        (ordenacao.Contains("programaDeOferta", StringComparison.Ordinal) ? oferta.Programa : string.Empty,
         ordenacao.Contains("formatoPedagogico", StringComparison.Ordinal) ? oferta.Formato : string.Empty);

    private static IEnumerable<(string Programa, string Formato)> OrdemEsperada(string ordenacao)
    {
        string[] campos = ordenacao.Split(',');
        IOrderedEnumerable<(string Programa, string Formato)> ordenadas = Ordenar(OfertasSemeadas, ChaveDe(campos[0]), campos[0].StartsWith('-'));

        return campos.Skip(1).Aggregate(ordenadas, (acumulada, campo) => Desempatar(acumulada, ChaveDe(campo), campo.StartsWith('-')));
    }

    private static Func<(string Programa, string Formato), string> ChaveDe(string campo) =>
        campo.EndsWith("programaDeOferta", StringComparison.Ordinal) ? oferta => oferta.Programa : oferta => oferta.Formato;

    private static IOrderedEnumerable<(string Programa, string Formato)> Ordenar(
        IEnumerable<(string Programa, string Formato)> ofertas, Func<(string Programa, string Formato), string> chave, bool decrescente) =>
        decrescente ? ofertas.OrderByDescending(chave, StringComparer.Ordinal) : ofertas.OrderBy(chave, StringComparer.Ordinal);

    private static IOrderedEnumerable<(string Programa, string Formato)> Desempatar(
        IOrderedEnumerable<(string Programa, string Formato)> ofertas, Func<(string Programa, string Formato), string> chave, bool decrescente) =>
        decrescente ? ofertas.ThenByDescending(chave, StringComparer.Ordinal) : ofertas.ThenBy(chave, StringComparer.Ordinal);

    private async Task<Guid> SemearOfertasDoCursoAsync()
    {
        Curso curso = Curso.Criar($"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}", "Engenharia Florestal", "Bacharelado", "Graduação", null).ValorEsperado();
        LocalOferta local = LocalOferta.Criar(
            new(TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA", null, null),
            DateTimeOffset.UtcNow).ValorEsperado();

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        dbContext.AddRange(curso, local);
        dbContext.AddRange(OfertasSemeadas.Select(o => NovaOferta(curso.Id, local.Id, o.Programa, o.Formato)));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return curso.Id;
    }

    private static OfertaCurso NovaOferta(Guid cursoId, Guid localId, string programa, string formato) =>
        OfertaCurso.Criar(
            cursoId,
            localId,
            UnidadeOfertante.Criar(Guid.CreateVersion7(), "ICH", "Instituto de Ciências Humanas", "CENTRO").ValorEsperado(),
            new DeclaracaoDaOfertaDeCurso(
                $"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), programa, formato, "EXTENSIVO", "REGULAR", ["MATUTINO"],
                BaseLegal: "Lei de teste")).ValorEsperado();
}
