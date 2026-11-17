namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ProjecaoDaFaseDoCronogramaTests
{
    private static readonly Guid FaseId = new("01a0d640-0000-7000-8000-0000000000c1");
    private static readonly Guid OrigemId = new("01a0d640-0000-7000-8000-0000000000c2");
    private static readonly Guid ProdutoDoResultadoId = new("01a0d640-0000-7000-8000-0000000000e1");
    private static readonly Guid ProdutoDoGabaritoId = new("01a0d640-0000-7000-8000-0000000000e2");
    private static readonly Guid UnidadeId = new("01a0d640-0000-7000-8000-0000000000d1");
    private static readonly DateTimeOffset Inicio = new(2027, 2, 1, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2027, 2, 11, 2, 59, 59, TimeSpan.Zero);

    private static OrigemCanonicaDaFase Origem(bool agrupaEtapas, bool permiteComplementacao, bool coletaInscricao, bool coletaIsencao) => new(
        OrigemId: OrigemId,
        Codigo: "RESULTADO_PRELIMINAR",
        OrigemData: OrigemDataFase.NoEdital,
        AgrupaEtapas: agrupaEtapas,
        PermiteComplementacao: permiteComplementacao,
        ColetaInscricao: coletaInscricao,
        ColetaSolicitacaoIsencao: coletaIsencao);

    private static DecisoesDeCronogramaDaFase Cronograma(UnidadeResponsavelSnapshot? unidade) => new(
        Ordem: 3,
        Inicio: Inicio,
        Fim: Fim,
        FaseConcluinteCodigo: "RESULTADO_FINAL",
        UnidadeResponsavel: unidade);

    private static RegraRecursoFase RegraDeRecurso() => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "1", new string('a', 64)),
        new ParametrosRegraPrazoRecurso(
            PrazoValor: 2m,
            PrazoUnidade: UnidadePrazo.DiasUteis,
            SuspensividadePrimeiraInstanciaValor: 5m,
            SuspensividadePrimeiraInstanciaUnidade: UnidadePrazo.Dias,
            AdmiteInstanciaSuperior: true,
            SuspensividadeSegundaInstanciaValor: 3m,
            SuspensividadeSegundaInstanciaUnidade: UnidadePrazo.Dias),
        ProdutoDoResultadoId).Value!;

    private static ProdutoDaFase[] Produtos() =>
    [
        ProdutoDaFase.Reidratar(ProdutoDoResultadoId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
        ProdutoDaFase.Reidratar(ProdutoDoGabaritoId, "GABARITO", PapelProdutoFase.Preliminar),
    ];

    private static BancaRequerida Banca() => BancaRequerida.Criar(
        new Guid("01a0d640-0000-7000-8000-0000000000c4"),
        "BANCA_DE_RECURSOS",
        "Banca de recursos",
        []);

    private static FaseCronograma FaseCompleta(bool agrupaEtapas = true, bool permiteComplementacao = false, bool coletaInscricao = true, bool coletaIsencao = false) =>
        FaseCronograma.Reidratar(
            FaseId,
            Origem(agrupaEtapas, permiteComplementacao, coletaInscricao, coletaIsencao),
            Cronograma(new UnidadeResponsavelSnapshot(UnidadeId, "CEPS", "Centro de Processos Seletivos")),
            new PublicacoesDaFase(
                Produtos: Produtos(),
                EmiteParecerIndividual: true,
                RegraRecurso: RegraDeRecurso(),
                TiposDeDecisao: [TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, TipoMotivoDeDecisaoCodigo.Classificacao]),
            [Banca()]);

    private static FaseCronograma FaseSemUnidadeNemPublicacoes() =>
        FaseCronograma.Reidratar(
            FaseId,
            Origem(agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaIsencao: false),
            Cronograma(unidade: null),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            []);

    [Fact(DisplayName = "A fase projeta a identidade e as bancas requeridas no topo")]
    public void ProjetarFase_Identidade_FicaNoTopo()
    {
        FaseCronograma fase = FaseCompleta();

        FaseCronogramaDto dto = ProjecaoDaFaseDoCronograma.ProjetarFase(fase);

        dto.Id.Should().Be(FaseId);
        dto.Codigo.Should().Be("RESULTADO_PRELIMINAR");
        dto.BancasRequeridas.Should().ContainSingle().Which.Id.Should().Be(fase.BancasRequeridas.Single().Id);
    }

    [Theory(DisplayName = "Cada traço herdado da fase canônica cai no seu campo, nos dois sentidos")]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    public void ProjetarFase_TracosDaFaseCanonica_CaemNoCampoCerto(bool agrupaEtapas, bool permiteComplementacao, bool coletaInscricao, bool coletaIsencao)
    {
        FaseCronograma fase = FaseCompleta(agrupaEtapas, permiteComplementacao, coletaInscricao, coletaIsencao);

        FaseCanonicaCongeladaDto grupo = ProjecaoDaFaseDoCronograma.ProjetarFase(fase).FaseCanonica;

        grupo.Should().BeEquivalentTo(new FaseCanonicaCongeladaDto(
            OrigemId: OrigemId,
            OrigemData: "NO_EDITAL",
            AgrupaEtapas: agrupaEtapas,
            PermiteComplementacao: permiteComplementacao,
            ColetaInscricao: coletaInscricao,
            ColetaSolicitacaoIsencao: coletaIsencao));
    }

    [Fact(DisplayName = "O cronograma projeta a posição, a janela, a fase concluinte e a unidade responsável")]
    public void ProjetarFase_Cronograma_ProjetaCadaCampo()
    {
        CronogramaDaFaseDto grupo = ProjecaoDaFaseDoCronograma.ProjetarFase(FaseCompleta()).Cronograma;

        grupo.Should().BeEquivalentTo(new CronogramaDaFaseDto(
            Ordem: 3,
            Inicio: Inicio,
            Fim: Fim,
            FaseConcluinteCodigo: "RESULTADO_FINAL",
            UnidadeResponsavel: new UnidadeResponsavelDto(UnidadeId, "CEPS", "Centro de Processos Seletivos")));
    }

    [Fact(DisplayName = "As publicações projetam os produtos em ordem de ato, o resultado, o parecer, a regra de recurso e os tipos de decisão")]
    public void ProjetarFase_Publicacoes_ProjetaCadaCampo()
    {
        FaseCronograma fase = FaseCompleta();

        PublicacoesDaFaseDto grupo = ProjecaoDaFaseDoCronograma.ProjetarFase(fase).Publicacoes;

        grupo.Should().BeEquivalentTo(
            PublicacoesEsperadas(fase.RegraRecurso!),
            opcoes => opcoes.WithStrictOrdering());
    }

    [Fact(DisplayName = "A fase sem unidade, sem produto e sem regra de recurso mantém os grupos e anula só o que é ausente")]
    public void ProjetarFase_SemUnidadeNemPublicacoes_GruposNuncaNulos()
    {
        FaseCronogramaDto dto = ProjecaoDaFaseDoCronograma.ProjetarFase(FaseSemUnidadeNemPublicacoes());

        dto.Cronograma.UnidadeResponsavel.Should().BeNull();
        dto.Publicacoes.RegraRecurso.Should().BeNull();
        dto.Publicacoes.Produtos.Should().BeEmpty();
        dto.Publicacoes.ProduzResultado.Should().BeFalse();
        dto.Publicacoes.EmiteParecerIndividual.Should().BeFalse();
        dto.Publicacoes.TiposDeDecisao.Should().BeEmpty();
    }

    private static PublicacoesDaFaseDto PublicacoesEsperadas(RegraRecursoFase regra) => new(
        Produtos:
        [
            new ProdutoDaFaseDto(ProdutoDoGabaritoId, "GABARITO", "PRELIMINAR"),
            new ProdutoDaFaseDto(ProdutoDoResultadoId, "RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
        ],
        ProduzResultado: true,
        EmiteParecerIndividual: true,
        RegraRecurso: new RegraRecursoFaseDto(
            regra.Id,
            ProdutoDoResultadoId,
            new ReferenciaRegraDto(RegraPrazoRecursoCodigo.AncoradoEmAto, "1", new string('a', 64)),
            new ParametrosRegraPrazoRecursoDto(2m, UnidadePrazo.DiasUteis, 5m, UnidadePrazo.Dias, true, 3m, UnidadePrazo.Dias),
            []),
        TiposDeDecisao: [TipoMotivoDeDecisaoCodigo.Classificacao, TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa]);
}
