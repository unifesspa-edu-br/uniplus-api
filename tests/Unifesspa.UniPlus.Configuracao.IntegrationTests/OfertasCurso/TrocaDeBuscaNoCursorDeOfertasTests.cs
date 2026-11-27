namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Net;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Continuar o cursor emitido para uma busca de ofertas de curso sob outra busca.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TrocaDeBuscaNoCursorDeOfertasTests : IAsyncLifetime
{
    private const string SiglaDaUnidade = "ICHBUSCA";

    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosCursoresDeConfiguracao _apoio;
    private readonly List<Guid> _cursos = [];

    public TrocaDeBuscaNoCursorDeOfertasTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosCursoresDeConfiguracao(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverCursosAsync(_cursos);

    [Theory(DisplayName = "O cursor emitido para uma busca de ofertas só continua com a mesma busca, sem contar os espaços das pontas")]
    [InlineData("q=outrabusca", HttpStatusCode.BadRequest)]
    [InlineData("q=%20%20" + SiglaDaUnidade + "%20", HttpStatusCode.OK)]
    public async Task Listar_CursorDeUmaBuscaContinuadoComOutroTexto_RecusaSoQuandoABuscaMuda(string busca, HttpStatusCode esperado)
    {
        Guid cursoId = await SemearTresOfertasAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        string rota = ApoioDaOrdenacaoAlfabetica.OfertasCurso;
        using HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"{rota}?cursoId={cursoId}&q={SiglaDaUnidade}&sort=programaDeOferta&limit=1", UriKind.Relative));
        string? proxima = ApoioDaOrdenacaoAlfabetica.LinkRel(primeira, "next");
        proxima.Should().NotBeNull("limit=1 com 3 ofertas na busca deve emitir próxima página");

        using HttpResponseMessage seguinte = await client.GetAsync(
            new Uri(proxima!.Replace($"q={SiglaDaUnidade}", busca, StringComparison.Ordinal), UriKind.RelativeOrAbsolute));

        seguinte.StatusCode.Should().Be(esperado);
    }

    [Fact(DisplayName = "O cursor emitido para as ofertas de um curso é recusado quando continuado com outro curso")]
    public async Task Listar_CursorDeUmCursoContinuadoComOutro_Retorna400()
    {
        Guid cursoId = await SemearTresOfertasAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        string rota = ApoioDaOrdenacaoAlfabetica.OfertasCurso;
        using HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"{rota}?cursoId={cursoId}&sort=programaDeOferta&limit=1", UriKind.Relative));
        string? proxima = ApoioDaOrdenacaoAlfabetica.LinkRel(primeira, "next");
        proxima.Should().NotBeNull("limit=1 com 3 ofertas deve emitir próxima página");

        using HttpResponseMessage seguinte = await client.GetAsync(
            new Uri(proxima!.Replace($"cursoId={cursoId}", $"cursoId={Guid.NewGuid()}", StringComparison.Ordinal), UriKind.RelativeOrAbsolute));

        seguinte.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> SemearTresOfertasAsync()
    {
        Curso curso = Curso.Criar($"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}", "Engenharia Florestal", "Bacharelado", "Graduação", null).ValorEsperado();
        LocalOferta local = LocalOferta.Criar(
            new("CAMPUS_SEDE", null, "1504208", "Marabá", "PA", null, null),
            DateTimeOffset.UtcNow).ValorEsperado();

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        dbContext.AddRange(curso, local);
        dbContext.AddRange(new[] { "REGULAR", "PARFOR", "PRONERA" }.Select(programa => NovaOferta(curso.Id, local.Id, programa)));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        _cursos.Add(curso.Id);

        return curso.Id;
    }

    private static OfertaCurso NovaOferta(Guid cursoId, Guid localId, string programa) =>
        OfertaCurso.Criar(
            cursoId,
            localId,
            UnidadeOfertante.Criar(Guid.CreateVersion7(), SiglaDaUnidade, "Instituto de Ciências Humanas", "CENTRO").ValorEsperado(),
            new DeclaracaoDaOfertaDeCurso(
                $"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), programa, "PRESENCIAL", "EXTENSIVO", "REGULAR",
                ["MATUTINO"], BaseLegal: "Lei de teste")).ValorEsperado();
}
