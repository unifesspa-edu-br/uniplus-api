namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

/// <summary>
/// A apuração das atribuições dos corretores num conceito categórico: o critério e o número de corretores
/// vêm da regra de consolidação, e a apuração só identifica o estado.
/// </summary>
public sealed class ConsolidacaoDoConceitoCategoricoTests
{
    private const CriterioDeApuracaoDoConceito Unanimidade = CriterioDeApuracaoDoConceito.Unanimidade;
    private const CriterioDeApuracaoDoConceito Simples = CriterioDeApuracaoDoConceito.MaioriaSimples;
    private const CriterioDeApuracaoDoConceito Absoluta = CriterioDeApuracaoDoConceito.MaioriaAbsoluta;

    private static ConceitoDaEtapa Conceito(params string[] codigos) =>
        ConceitoDaEtapa.Criar(
            "CONCEITO", "Conceito", FormaDoConceito.PorOpcoes, null,
            [.. codigos.Select(static codigo => new ValorCategoricoInformado(codigo, codigo, codigo == "APTO"))]).Value!;

    private static ConceitoDaEtapa AptoInapto() => Conceito("APTO", "INAPTO");

    private static ConceitoDaEtapa AptoInaptoEPendente() => Conceito("APTO", "INAPTO", "PENDENTE");

    private static ConceitoDaEtapa Numerico() =>
        ConceitoDaEtapa.Criar("NOTA", "Nota", FormaDoConceito.Numerico, new EscalaInformada(0m, 10m, 2), null).Value!;

    private static ResultadoDaConsolidacaoDoConceito Consolidar(
        CriterioDeApuracaoDoConceito criterio, int corretores, params string[] atribuicoes) =>
        ConsolidacaoDoConceitoCategorico.Consolidar(AptoInaptoEPendente(), criterio, corretores, atribuicoes);

    [Fact(DisplayName = "Dois corretores que atribuem Apto consolidam Apto por unanimidade")]
    public void Unanimidade_TodosIguais_Consolida()
    {
        ResultadoDaConsolidacaoDoConceito resultado = Consolidar(Unanimidade, 2, "APTO", "APTO");

        resultado.Should().Be(ResultadoDaConsolidacaoDoConceito.Consolidado("APTO"));
        resultado.Situacao.Should().Be(SituacaoDaConsolidacaoDoConceito.Consolidado);
    }

    [Fact(DisplayName = "Apto e Inapto sem unanimidade não consolidam")]
    public void Unanimidade_Divergente_SemUnanimidade()
    {
        Consolidar(Unanimidade, 2, "APTO", "INAPTO").Should().Be(ResultadoDaConsolidacaoDoConceito.SemUnanimidade);
    }

    [Fact(DisplayName = "Um corretor só consolida o que atribuiu")]
    public void UmCorretor_ConsolidaOQueAtribuiu()
    {
        Consolidar(Unanimidade, 1, "INAPTO").Should().Be(ResultadoDaConsolidacaoDoConceito.Consolidado("INAPTO"));
    }

    [Fact(DisplayName = "Pela maioria simples, dois contra um consolida o valor dos dois")]
    public void MaioriaSimples_DoisContraUm_Consolida()
    {
        Consolidar(Simples, 3, "APTO", "INAPTO", "APTO").Should().Be(ResultadoDaConsolidacaoDoConceito.Consolidado("APTO"));
    }

    [Fact(DisplayName = "Pela maioria simples, um contra um é empate")]
    public void MaioriaSimples_UmContraUm_Empate()
    {
        Consolidar(Simples, 2, "APTO", "INAPTO").Should().Be(ResultadoDaConsolidacaoDoConceito.Empate);
    }

    [Fact(DisplayName = "Pela maioria simples, um, um e um é empate")]
    public void MaioriaSimples_UmUmUm_Empate()
    {
        Consolidar(Simples, 3, "APTO", "INAPTO", "PENDENTE").Should().Be(ResultadoDaConsolidacaoDoConceito.Empate);
    }

    [Fact(DisplayName = "Pela maioria simples, dois contra um e um consolida o valor dos dois, e pela absoluta é empate")]
    public void MaioriaSimples_DoisUmUm_ConsolidaEAbsolutaNao()
    {
        Consolidar(Simples, 4, "APTO", "APTO", "INAPTO", "PENDENTE").Should().Be(ResultadoDaConsolidacaoDoConceito.Consolidado("APTO"));
        Consolidar(Absoluta, 4, "APTO", "APTO", "INAPTO", "PENDENTE").Should().Be(ResultadoDaConsolidacaoDoConceito.Empate);
    }

    [Fact(DisplayName = "Pela maioria absoluta, dois de três consolida")]
    public void MaioriaAbsoluta_DoisDeTres_Consolida()
    {
        Consolidar(Absoluta, 3, "INAPTO", "APTO", "INAPTO").Should().Be(ResultadoDaConsolidacaoDoConceito.Consolidado("INAPTO"));
    }

    [Fact(DisplayName = "Pela maioria absoluta, três de quatro consolida")]
    public void MaioriaAbsoluta_TresDeQuatro_Consolida()
    {
        Consolidar(Absoluta, 4, "APTO", "APTO", "APTO", "INAPTO").Should().Be(ResultadoDaConsolidacaoDoConceito.Consolidado("APTO"));
    }

    [Fact(DisplayName = "Pela maioria absoluta, dois de quatro não alcança mais da metade e é empate")]
    public void MaioriaAbsoluta_DoisDeQuatro_Empate()
    {
        Consolidar(Absoluta, 4, "APTO", "APTO", "INAPTO", "INAPTO").Should().Be(ResultadoDaConsolidacaoDoConceito.Empate);
    }

    [Fact(DisplayName = "Pela maioria absoluta, um, um e um é empate")]
    public void MaioriaAbsoluta_UmUmUm_Empate()
    {
        Consolidar(Absoluta, 3, "APTO", "INAPTO", "PENDENTE").Should().Be(ResultadoDaConsolidacaoDoConceito.Empate);
    }

    [Theory(DisplayName = "Com menos atribuições que os corretores exigidos, a consolidação aguarda a correção, qualquer que seja o critério")]
    [InlineData(CriterioDeApuracaoDoConceito.Unanimidade)]
    [InlineData(CriterioDeApuracaoDoConceito.MaioriaSimples)]
    [InlineData(CriterioDeApuracaoDoConceito.MaioriaAbsoluta)]
    public void MenosAtribuicoesQueOExigido_AguardaACorrecao(CriterioDeApuracaoDoConceito criterio)
    {
        Consolidar(criterio, 3, "APTO", "APTO").Should().Be(ResultadoDaConsolidacaoDoConceito.AguardandoCorrecao);
        Consolidar(criterio, 2).Should().Be(ResultadoDaConsolidacaoDoConceito.AguardandoCorrecao);
    }

    [Fact(DisplayName = "Com mais atribuições que as exigidas, a apuração conta todas")]
    public void MaisAtribuicoesQueOExigido_ContaTodas()
    {
        Consolidar(Unanimidade, 2, "APTO", "APTO", "INAPTO").Should().Be(ResultadoDaConsolidacaoDoConceito.SemUnanimidade);
    }

    [Fact(DisplayName = "A ordem das atribuições não muda o resultado")]
    public void OrdemDasAtribuicoes_NaoMudaOResultado()
    {
        string[][] permutacoes =
        [
            ["APTO", "INAPTO", "APTO"], ["APTO", "APTO", "INAPTO"], ["INAPTO", "APTO", "APTO"],
        ];

        permutacoes.Select(static atribuicoes => Consolidar(Simples, 3, atribuicoes))
            .Should().AllBeEquivalentTo(ResultadoDaConsolidacaoDoConceito.Consolidado("APTO"));
    }

    [Fact(DisplayName = "A atribuição que não é valor do conceito é defeito de quem chama, ainda que falte corretor")]
    public void ValorForaDoConceito_Lanca()
    {
        Action consolidar = () => Consolidar(Simples, 3, "APTO", "REPROVADO");

        consolidar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "O conceito numérico não tem valor a consolidar")]
    public void ConceitoNumerico_Lanca()
    {
        Action consolidar = () => ConsolidacaoDoConceitoCategorico.Consolidar(Numerico(), Simples, 1, []);

        consolidar.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Menos de um corretor exigido é defeito de quem chama")]
    [InlineData(0)]
    [InlineData(-1)]
    public void CorretoresExigidosMenoresQueUm_Lanca(int corretores)
    {
        Action consolidar = () => Consolidar(Simples, corretores, "APTO");

        consolidar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "O critério desconhecido é defeito de quem chama")]
    public void CriterioDesconhecido_Lanca()
    {
        Action consolidar = () => Consolidar((CriterioDeApuracaoDoConceito)99, 1, "APTO");

        consolidar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "O critério desconhecido é defeito de quem chama, ainda que faltem atribuições")]
    public void CriterioDesconhecido_ComAtribuicoesAMenos_Lanca()
    {
        Action consolidar = () => Consolidar((CriterioDeApuracaoDoConceito)99, 3, "APTO");

        consolidar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Menos de um corretor exigido é defeito de quem chama, ainda que o critério seja desconhecido")]
    public void CorretoresExigidosInvalidosECriterioDesconhecido_Lanca()
    {
        Action consolidar = () => Consolidar((CriterioDeApuracaoDoConceito)99, 0);

        consolidar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "O conceito alterado no catálogo depois da cópia não muda o resultado da cópia antiga")]
    public void CatalogoAlterado_NaoMudaACopiaAntiga()
    {
        ConceitoDaEtapa copiaAntiga = AptoInapto();
        ConceitoDaEtapa catalogoNovo = Conceito("CONFORME", "NAO_CONFORME");

        ResultadoDaConsolidacaoDoConceito antiga = ConsolidacaoDoConceitoCategorico.Consolidar(copiaAntiga, Unanimidade, 2, ["APTO", "APTO"]);
        Action comACopiaNova = () => ConsolidacaoDoConceitoCategorico.Consolidar(catalogoNovo, Unanimidade, 2, ["APTO", "APTO"]);

        antiga.Should().Be(ResultadoDaConsolidacaoDoConceito.Consolidado("APTO"));
        comACopiaNova.Should().Throw<ArgumentException>();
    }
}
