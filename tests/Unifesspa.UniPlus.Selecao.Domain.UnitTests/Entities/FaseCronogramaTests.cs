namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioFaseCronograma;

/// <summary>
/// Cobertura de <see cref="FaseCronograma.Criar"/> (Story #851): as invariantes que a
/// factory prova sozinha — janela × <see cref="OrigemDataFase"/> (CA-07), a unicidade do
/// tipo de ato dentro da fase, a promessa de parecer individual sustentada por publicação
/// de resultado, e a âncora do prazo de recurso derivada do produto preliminar da própria
/// fase.
/// </summary>
public sealed class FaseCronogramaTests
{
    // ── Unidade responsável pela fase (ADR-0061, ADR-0129) ──

    [Fact(DisplayName = "A fase guarda o id, a sigla e o nome da unidade responsável informada")]
    public void UnidadeResponsavel_Informada_FicaGravadaNaFase()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, UnidadeResponsavel = UnidadesDeTeste.Cga });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.UnidadeResponsavel!.OrigemId.Should().Be(UnidadesDeTeste.Cga.OrigemId);
        resultado.Value.UnidadeResponsavel!.Codigo.Should().Be(UnidadesDeTeste.Cga.Codigo);
        resultado.Value.UnidadeResponsavel!.Sigla.Should().Be("CGA");
        resultado.Value.UnidadeResponsavel!.Nome.Should().Be("Centro de Gestão Acadêmica");
        resultado.Value.UnidadeResponsavel.Should().Be(UnidadesDeTeste.Cga);
    }

    [Fact(DisplayName = "Fases com a mesma unidade guardam valores independentes")]
    public void UnidadeResponsavel_CompartilhadaEntreFases_CadaFaseGuardaSuaCopia()
    {
        FaseCronograma primeira = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, UnidadeResponsavel = UnidadesDeTeste.Cga }).Value!;
        FaseCronograma segunda = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, UnidadeResponsavel = UnidadesDeTeste.Cga }).Value!;

        primeira.UnidadeResponsavel.Should().Be(segunda.UnidadeResponsavel);
        primeira.UnidadeResponsavel.Should().NotBeSameAs(segunda.UnidadeResponsavel);
    }

    [Fact(DisplayName = "O rascunho aceita fase ainda sem unidade responsável")]
    public void UnidadeResponsavel_Ausente_FaseEAceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, ComUnidade = false });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.UnidadeResponsavel.Should().BeNull();
    }

    // ── CA-07 — janela × OrigemData ──

    [Fact(DisplayName = "CA-07: fase de origem NO_EDITAL sem janela é recusada")]
    public void Janela_ObrigatoriaEmDataNoEdital_SemInicioNemFim_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NoEdital, Inicio = null, Fim = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaObrigatoriaEmDataNoEdital");
    }

    [Theory(DisplayName = "CA-07: fase de origem NO_EDITAL com apenas um lado da janela é recusada")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Janela_ObrigatoriaEmDataNoEdital_ParcialmenteInformada_Recusa(bool comInicio, bool comFim)
    {
        DateTimeOffset? inicio = comInicio ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : null;
        DateTimeOffset? fim = comFim ? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero) : null;

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NoEdital, Inicio = inicio, Fim = fim });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaObrigatoriaEmDataNoEdital");
    }

    [Fact(DisplayName = "CA-07 (contraprova): fase de origem NA_EXECUCAO sem janela é aceita — 'sem data' é estado válido")]
    public void Janela_OrigemNaExecucao_SemInicioNemFim_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Inicio = null, Fim = null });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Inicio.Should().BeNull();
        resultado.Value.Fim.Should().BeNull();
    }

    [Fact(DisplayName = "Fase de origem NA_EXECUCAO com a data final e sem início é aceita: só a data fixada no edital exige o período inteiro")]
    public void Periodo_OrigemNaExecucao_SoComFim_Aceita()
    {
        DateTimeOffset fim = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Inicio = null, Fim = fim });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Inicio.Should().BeNull();
        resultado.Value.Fim.Should().Be(fim);
    }

    [Fact(DisplayName = "CA-07: fase com Fim antes do Inicio é recusada (JanelaInvertida) — vale para qualquer OrigemData")]
    public void Janela_FimAntesDoInicio_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Inicio = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Fim = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaInvertida");
    }

    // ── A janela é guardada em UTC (issue #1124) ──

    [Theory(DisplayName = "A janela informada com offset é guardada em UTC, preservando o instante")]
    [InlineData(-3, 0)]
    [InlineData(5, 30)]
    [InlineData(0, 0)]
    public void Janela_ComOffset_EGuardadaEmUtc(int horas, int minutos)
    {
        TimeSpan offset = new(horas, minutos, 0);
        DateTimeOffset instanteUtc = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NoEdital, Inicio = instanteUtc.ToOffset(offset), Fim = instanteUtc.AddDays(2).ToOffset(offset) });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Inicio.Should().Be(instanteUtc, "o instante informado é preservado");
        resultado.Value.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero,
            "a coluna 'timestamp with time zone' só aceita a representação em UTC");
        resultado.Value.Fim.Should().Be(instanteUtc.AddDays(2));
        resultado.Value.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "A janela invertida é reconhecida pelo instante, não pela hora local de cada offset")]
    public void Janela_InvertidaEntreOffsetsDiferentes_Recusa()
    {
        // 10:00-03:00 é 13:00Z; 12:00Z é anterior — a hora local do fim (12) parece maior
        // que a do início (10), mas o instante é menor.
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Inicio = new DateTimeOffset(2027, 1, 25, 10, 0, 0, TimeSpan.FromHours(-3)), Fim = new DateTimeOffset(2027, 1, 25, 12, 0, 0, TimeSpan.Zero) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaInvertida");
        resultado.Error.Message.Should().NotContain("-03:00",
            "a mensagem descreve a janela na mesma representação em que ela é guardada");
    }

    private static FaseCronograma FaseReidratadaComJanelaEmBelem(DateTimeOffset instanteUtc) => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        Guid.CreateVersion7(),
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_PRELIMINAR", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: instanteUtc.ToOffset(TimeSpan.FromHours(-3)), Fim: instanteUtc.AddDays(2).ToOffset(TimeSpan.FromHours(-3)), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    [Fact(DisplayName = "Reidratar também guarda a janela em UTC")]
    public void Reidratar_ComOffset_GuardaEmUtc()
    {
        DateTimeOffset instanteUtc = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);

        FaseCronograma fase = FaseReidratadaComJanelaEmBelem(instanteUtc);

        fase.Inicio.Should().Be(instanteUtc);
        fase.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero);
        fase.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    // ── CA-01/CA-04 — a coleção de produtos e o ProduzResultado derivado ──

    [Fact(DisplayName = "CA-04: fase sem produto nenhum não produz resultado, e é aceita")]
    public void Produtos_Vazios_NaoProduzResultado()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [] });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ProduzResultado.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-01/CA-04: produto SEM papel não torna a fase produtora de resultado")]
    public void Produtos_SemPapel_NaoProduzResultado()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [ProdutoDaFase.Criar("COMUNICADO", null)] });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FaseCronograma fase = resultado.Value!;
        fase.ProduzResultado.Should().BeFalse(
            "o ato que não é resultado é publicado sem papel, e publicar aviso não torna a fase produtora");
        fase.Produtos.Should().ContainSingle().Which.Papel.Should().BeNull();
    }

    [Theory(DisplayName = "CA-04: produto COM papel torna a fase produtora de resultado")]
    [InlineData(PapelProdutoFase.Preliminar)]
    [InlineData(PapelProdutoFase.Definitivo)]
    public void Produtos_ComPapel_ProduzResultado(PapelProdutoFase papel)
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", papel)] });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ProduzResultado.Should().BeTrue();
    }

    // ── CA-03 — o par ato e papel uma única vez por fase ──

    /// <summary>
    /// O catálogo nomeia a matéria, não o papel: "Resultado da homologação das inscrições" é
    /// um código só, publicado uma vez como preliminar — que abre o ciclo recursal — e outra
    /// como definitivo, que o encerra. Recusar isso tornava o ciclo da homologação
    /// inexprimível, porque não existe ato "definitivo da homologação" no catálogo.
    /// </summary>
    [Fact(DisplayName = "CA-03: a mesma matéria publicada como preliminar e como definitiva é aceita")]
    public void Produtos_MesmoAtoEmPapeisDistintos_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Produtos = [
                    ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Produtos.Should().HaveCount(2);
    }

    [Fact(DisplayName = "A fase declara o gabarito uma vez como preliminar e outra como definitivo")]
    public void Produtos_GabaritoComCadaPapel_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Produtos = [
                    ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Definitivo),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-03: declarar o MESMO ato no MESMO papel duas vezes na fase é recusado")]
    public void Produtos_MesmoAtoEMesmoPapel_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Produtos = [
                    ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ],
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("FaseCronograma.AtoDuplicadoNaFase");
    }

    /// <summary>
    /// Ato que não é resultado tem papel nulo, e dois nulos são o mesmo papel — senão a fase
    /// publicaria o mesmo comunicado duas vezes sem nada que os distinga.
    /// </summary>
    [Fact(DisplayName = "CA-03: o mesmo ato sem papel duas vezes na fase é recusado")]
    public void Produtos_AtoSemPapelDuplicado_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Produtos = [
                    ProdutoDaFase.Criar("COMUNICADO", null),
                    ProdutoDaFase.Criar("COMUNICADO", null),
                ],
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("FaseCronograma.AtoDuplicadoNaFase");
    }
}
