namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O grafo do formulário com grupos repetíveis (UNI-REQ-0146): o campo do grupo só é citado dentro
/// do próprio grupo, e só pelos campos seguintes; as regras do grupo e dos campos citam o que o
/// formulário conhece antes da posição do grupo.
/// </summary>
public sealed class GrafoDoGrupoTests
{
    private static readonly HashSet<string> NenhumConhecidoAntes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    [Fact]
    public void ValidarColeta_ItemQueCitaCampoDoGrupo_RecusaComoMembroForaDoGrupo()
    {
        DomainError? erro = Validar(
            [Item("RENDA", 2, "PARENTESCO")],
            [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact]
    public void ValidarColeta_CampoQueCitaCampoDeOutroGrupo_RecusaComoMembroForaDoGrupo()
    {
        DomainError? erro = Validar(
            [],
            [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0)), Grupo("BENS", 2, [], Subitem("VALOR", 0, "PARENTESCO"))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact]
    public void ValidarColeta_RegraDoGrupoQueCitaCampoDeOutroGrupo_RecusaComoMembroForaDoGrupo()
    {
        DomainError? erro = Validar(
            [],
            [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0)), Grupo("BENS", 2, ["PARENTESCO"], Subitem("VALOR", 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact]
    public void ValidarColeta_ExibicaoDaSecaoQueCitaCampoDoGrupo_RecusaComoMembroForaDoGrupo()
    {
        DomainError? erro = GrafoDoFormulario.ValidarColeta(
            [],
            [new EtapaDoGrafo("RENDA", 1, ["PARENTESCO"])],
            NenhumConhecidoAntes,
            SemDerivacoes,
            [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact]
    public void ValidarColeta_ExibicaoDaSecaoSoComGrupo_NaoCitaItemPosteriorAoGrupo()
    {
        DomainError? erro = GrafoDoFormulario.ValidarColeta(
            [Item("RENDA", 2)],
            [new EtapaDoGrafo("FAMILIA", 1, ["RENDA"])],
            NenhumConhecidoAntes,
            SemDerivacoes,
            [new GrupoDoGrafo("COMPOSICAO", 1, "FAMILIA", [], [Subitem("PARENTESCO", 0)])]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Fact]
    public void CitacaoInvalida_TermoQueCitaCampoDoGrupo_RecusaComoMembroForaDoGrupo()
    {
        GrupoDoGrafo grupo = Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0));

        DomainError? erro = GrafoDoFormulario.CitacaoInvalida(
            [], [], [new TermoDoGrafo("BANCO_CENTRAL", ["PARENTESCO"])], Dependencias([]), [grupo]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(2, GrafoFormularioErrorCodes.CitaFatoPosterior)]
    public void ValidarColeta_CampoQueCitaOutroCampoDoGrupo_SoAceitaOAnterior(int ordemDoCitado, string? esperado)
    {
        DomainError? erro = Validar(
            [],
            [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", ordemDoCitado), Subitem("SOB_GUARDA", 1, "PARENTESCO"))]);

        erro?.Code.Should().Be(esperado);
        (erro is null).Should().Be(esperado is null);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(2, GrafoFormularioErrorCodes.CitaFatoPosterior)]
    public void ValidarColeta_CampoDoGrupoQueCitaItem_SoAceitaOItemAnteriorAoGrupo(int ordemDoItem, string? esperado)
    {
        DomainError? erro = Validar(
            [Item("CONCORRER_RENDA", ordemDoItem)],
            [Grupo("COMPOSICAO", 1, [], Subitem("RENDA_DO_MEMBRO", 0, "CONCORRER_RENDA"))]);

        erro?.Code.Should().Be(esperado);
        (erro is null).Should().Be(esperado is null);
    }

    [Fact]
    public void ValidarColeta_RegraDoGrupoQueCitaItemPosterior_Recusa()
    {
        DomainError? erro = Validar(
            [Item("CONCORRER_RENDA", 2)],
            [Grupo("COMPOSICAO", 1, ["CONCORRER_RENDA"], Subitem("PARENTESCO", 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Theory]
    [InlineData("RENDA", "IDADE")]
    [InlineData("COMPOSICAO", "IDADE")]
    [InlineData("BENS", "RENDA")]
    [InlineData("BENS", "COMPOSICAO")]
    public void ValidarColeta_CodigoRepetidoEntreFatosEGrupos_Recusa(string codigoDoSegundoGrupo, string campoDoSegundoGrupo)
    {
        DomainError? erro = Validar(
            [Item("RENDA", 0)],
            [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0)), Grupo(codigoDoSegundoGrupo, 2, [], Subitem(campoDoSegundoGrupo, 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.FatoDuplicado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidarColeta_GrupoComCodigoDeFatoDerivadoOuPressuposto_Recusa(bool derivado)
    {
        HashSet<string> conhecidosAntes = new(derivado ? [] : ["RENDA_PER_CAPITA"], StringComparer.Ordinal);
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal);
        if (derivado)
        {
            derivacoes["RENDA_PER_CAPITA"] = [];
        }

        DomainError? erro = GrafoDoFormulario.ValidarColeta(
            [], [], conhecidosAntes, derivacoes, [Grupo("RENDA_PER_CAPITA", 1, [], Subitem("PARENTESCO", 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.FatoDuplicado);
    }

    [Fact]
    public void ValidarColeta_GrupoNaOrdemDeUmItem_Recusa()
    {
        DomainError? erro = Validar([Item("RENDA", 1)], [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.OrdemDuplicada);
    }

    [Fact]
    public void ValidarColeta_DoisCamposDoGrupoNaMesmaOrdem_Recusa()
    {
        DomainError? erro = Validar([], [Grupo("COMPOSICAO", 1, [], Subitem("PARENTESCO", 0), Subitem("IDADE", 0))]);

        erro!.Code.Should().Be(GrafoFormularioErrorCodes.OrdemDuplicada);
    }

    private static DomainError? Validar(IReadOnlyList<ItemDoGrafo> itens, IReadOnlyList<GrupoDoGrafo> grupos) =>
        GrafoDoFormulario.ValidarColeta(itens, [], NenhumConhecidoAntes, SemDerivacoes, grupos);

    private static DependenciasDoFormulario Dependencias(IReadOnlyList<ItemDoGrafo> itens) =>
        GrafoDoFormulario.Dependencias(itens, NenhumConhecidoAntes, SemDerivacoes);

    private static ItemDoGrafo Item(string fato, int ordem, params string[] citados) => new(fato, ordem, null, citados);

    private static ItemDoGrafo Subitem(string fato, int ordem, params string[] citados) => new(fato, ordem, null, citados);

    private static GrupoDoGrafo Grupo(string codigo, int ordem, string[] citados, params ItemDoGrafo[] subitens) =>
        new(codigo, ordem, null, citados, subitens);
}
