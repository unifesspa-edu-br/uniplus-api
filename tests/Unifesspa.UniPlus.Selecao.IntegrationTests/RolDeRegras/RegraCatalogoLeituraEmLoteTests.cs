namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

/// <summary>
/// A leitura das regras do catálogo por pares de código e versão, numa só consulta, contra Postgres real: só o par que existe
/// volta, o par repetido volta uma vez e o texto com caractere nulo, que o banco recusaria, não tem regra e não derruba a leitura.
/// </summary>
public sealed class RegraCatalogoLeituraEmLoteTests : IClassFixture<RegraCatalogoDbFixture>
{
    private const string FormulaMediaPonderada = "FORMULA_MEDIA_PONDERADA";

    private readonly RegraCatalogoDbFixture _fixture;

    public RegraCatalogoLeituraEmLoteTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ListarPorReferenciasAsync devolve só as regras dos pares pedidos, e o par sem regra não volta")]
    public async Task ListarPorReferenciasAsync_DevolveSoOsParesQueExistem()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> regras = await reader.ListarPorReferenciasAsync(
            [(FormulaMediaPonderada, "v1"), (FormulaMediaPonderada, "v99"), ("REGRA_QUE_NAO_EXISTE", "v1")], CancellationToken.None);

        RegraCatalogo regra = regras.Should().ContainSingle().Subject;
        (regra.Codigo, regra.Versao, regra.Tipo).Should().Be((FormulaMediaPonderada, "v1", TipoRegra.RegraCalculo));
    }

    [Fact(DisplayName = "ListarPorReferenciasAsync devolve o par repetido no lote uma só vez")]
    public async Task ListarPorReferenciasAsync_ParRepetido_VoltaUmaVez()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> regras = await reader.ListarPorReferenciasAsync(
            [(FormulaMediaPonderada, "v1"), (FormulaMediaPonderada, "v1")], CancellationToken.None);

        regras.Should().ContainSingle();
    }

    [Fact(DisplayName = "ListarPorReferenciasAsync não devolve regra de par com o caractere nulo e não falha")]
    public async Task ListarPorReferenciasAsync_CaractereNulo_NaoTemRegra()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> regras = await reader.ListarPorReferenciasAsync(
            [($"{FormulaMediaPonderada}\0", "v1"), (FormulaMediaPonderada, "v\01")], CancellationToken.None);

        regras.Should().BeEmpty();
    }

    [Fact(DisplayName = "ListarPorReferenciasAsync com o lote vazio devolve lista vazia")]
    public async Task ListarPorReferenciasAsync_LoteVazio_DevolveListaVazia()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        (await reader.ListarPorReferenciasAsync([], CancellationToken.None)).Should().BeEmpty();
    }
}
