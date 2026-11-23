namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A leitura em lote de fases canônicas por código (ADR-0056), contra Postgres real. É ela que
/// diz ao documento da configuração quais fases citadas existem: uma fase removida que
/// continuasse respondendo faria o cronograma declarar uma fase que saiu do cadastro.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class FaseCanonicaReaderPorCodigosTests : IAsyncLifetime
{
    private const string Admin = "admin-a";
    private const string Ensalamento = "ENSALAMENTO";
    private const string Classificacao = "CLASSIFICACAO";
    private const string ResultadoFinal = "RESULTADO_FINAL";

    private static readonly string[] CodigosUsados = [Ensalamento, Classificacao, ResultadoFinal];

    private readonly ConfiguracaoDbFixture _fixture;

    public FaseCanonicaReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => ApagarFasesUsadasAsync();

    public Task DisposeAsync() => ApagarFasesUsadasAsync();

    [Fact(DisplayName = "ListarVivasPorCodigosAsync devolve só as fases vivas pedidas, ordenadas por código")]
    public async Task ListarVivasPorCodigosAsync_DevolveSoAsPedidasOrdenadas()
    {
        await CadastrarAsync(vivas: [Ensalamento, Classificacao, ResultadoFinal], removidas: []);

        IReadOnlyList<FaseCanonicaView> fases = await ListarAsync(ResultadoFinal, Classificacao);

        fases.Select(f => f.Codigo).Should().Equal(Classificacao, ResultadoFinal);
        fases.Should().OnlyContain(f => f.Id != Guid.Empty && f.Nome.Length > 0);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve código inexistente nem fora do formato")]
    public async Task ListarVivasPorCodigosAsync_CodigoInexistenteOuMalFormado_NaoVolta()
    {
        await CadastrarAsync(vivas: [Ensalamento], removidas: []);

        IReadOnlyList<FaseCanonicaView> fases =
            await ListarAsync(Ensalamento, "FASE_QUE_NAO_EXISTE", "fase-minuscula", string.Empty);

        fases.Select(f => f.Codigo).Should().Equal(Ensalamento);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve fase removida")]
    public async Task ListarVivasPorCodigosAsync_FaseRemovida_NaoVolta()
    {
        await CadastrarAsync(vivas: [Ensalamento], removidas: [Classificacao]);

        IReadOnlyList<FaseCanonicaView> fases = await ListarAsync(Ensalamento, Classificacao);

        fases.Select(f => f.Codigo).Should().Equal(Ensalamento);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com códigos repetidos devolve cada fase uma só vez")]
    public async Task ListarVivasPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaFase()
    {
        await CadastrarAsync(vivas: [Ensalamento, Classificacao], removidas: []);

        IReadOnlyList<FaseCanonicaView> fases =
            await ListarAsync(Ensalamento, Classificacao, Ensalamento, $" {Ensalamento} ");

        fases.Select(f => f.Codigo).Should().Equal(Classificacao, Ensalamento);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com lote vazio não devolve fase")]
    public async Task ListarVivasPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivas: [Ensalamento], removidas: []);

        IReadOnlyList<FaseCanonicaView> fases = await ListarAsync();

        fases.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<FaseCanonicaView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await new FaseCanonicaReader(leitura).ListarVivasPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivas, string[] removidas)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext(Admin);
        List<FaseCanonica> fases = [.. vivas.Concat(removidas).Select(Fase)];
        escrita.FasesCanonicas.AddRange(fases);
        await escrita.SaveChangesAsync();

        escrita.FasesCanonicas.RemoveRange(fases.Where(f => removidas.Contains(f.Codigo.Valor)));
        await escrita.SaveChangesAsync();
    }

    private async Task ApagarFasesUsadasAsync()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM configuracao.tipos_de_fase WHERE codigo = ANY(@p0)",
            new NpgsqlParameter("p0", CodigosUsados));
    }

    private static FaseCanonica Fase(string codigo) =>
        FaseCanonica.Criar(
            codigo, $"Fase {codigo}", null, null, "NO_EDITAL",
            new SinalizadoresDaFase(
                AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: false)).Value!;
}
