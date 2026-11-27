namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDaPersistenciaDeOfertaCurso;

/// <summary>
/// A leitura em lote de ofertas de curso por código (ADR-0056), contra Postgres real. É ela que diz ao documento da
/// configuração quais ofertas citadas existem e traz, junto de cada uma, o nome, o grau e o grupo de área do curso: uma
/// oferta removida que continuasse respondendo faria o quadro de vagas citar uma oferta que saiu do cadastro.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class OfertaCursoReaderPorCodigosTests
{
    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDeOfertaCurso _apoio;
    private readonly string _prefixo = $"OFERTA_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    public OfertaCursoReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync devolve só as ofertas vivas pedidas, ordenadas por código, com o curso")]
    public async Task ListarVivasPorCodigosAsync_DevolveSoAsPedidasOrdenadasComOCurso()
    {
        (string a, string b, string naoPedida) = (Codigo("A"), Codigo("B"), Codigo("C"));
        await CadastrarAsync(vivas: [b, a, naoPedida], removidas: []);

        IReadOnlyList<OfertaCursoView> ofertas = await ListarAsync(b, a);

        ofertas.Select(o => o.Codigo).Should().Equal(a, b);
        ofertas.Should().OnlyContain(o => o.Id != Guid.Empty && o.CursoNome == "Engenharia Civil" && o.CursoGrau == "Bacharelado");
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve código inexistente, escrito de outra forma, vazio nem com caractere nulo")]
    public async Task ListarVivasPorCodigosAsync_CodigoSemOferta_NaoVolta()
    {
        string viva = Codigo("A");
        await CadastrarAsync(vivas: [viva], removidas: []);

        IReadOnlyList<OfertaCursoView> ofertas =
            await ListarAsync(viva, Codigo("INEXISTENTE"), $" {viva} ", string.Empty, $"{viva}\0");

        ofertas.Select(o => o.Codigo).Should().Equal(viva);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve oferta removida")]
    public async Task ListarVivasPorCodigosAsync_OfertaRemovida_NaoVolta()
    {
        (string viva, string removida) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivas: [viva], removidas: [removida]);

        IReadOnlyList<OfertaCursoView> ofertas = await ListarAsync(viva, removida);

        ofertas.Select(o => o.Codigo).Should().Equal(viva);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com códigos repetidos devolve cada oferta uma só vez")]
    public async Task ListarVivasPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaOferta()
    {
        (string a, string b) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivas: [a, b], removidas: []);

        IReadOnlyList<OfertaCursoView> ofertas = await ListarAsync(a, b, a);

        ofertas.Select(o => o.Codigo).Should().Equal(a, b);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com lote vazio não devolve oferta")]
    public async Task ListarVivasPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivas: [Codigo("A")], removidas: []);

        IReadOnlyList<OfertaCursoView> ofertas = await ListarAsync();

        ofertas.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<OfertaCursoView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await new OfertaCursoReader(leitura).ListarVivasPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivas, string[] removidas)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        List<OfertaCurso> ofertas = [.. vivas.Concat(removidas).Select(codigo => Nova(codigo, cursoId, localId))];
        foreach (OfertaCurso oferta in ofertas)
        {
            await _apoio.GravarAsync(oferta);
        }

        foreach (OfertaCurso removida in ofertas.Where(o => removidas.Contains(o.Codigo)))
        {
            await _apoio.RemoverAsync(removida.Id);
        }
    }

    private static OfertaCurso Nova(string codigo, Guid cursoId, Guid localId) =>
        OfertaCurso.Criar(
            cursoId, localId, NovaUnidade(),
            new DeclaracaoDaOfertaDeCurso(
                codigo, "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"])).ValorEsperado();

    private string Codigo(string sufixo) => $"{_prefixo}_{sufixo}";
}
