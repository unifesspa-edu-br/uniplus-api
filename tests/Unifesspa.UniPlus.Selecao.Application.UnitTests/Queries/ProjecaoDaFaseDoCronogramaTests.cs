namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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

    private static DecisoesDeCronogramaDaFase Cronograma(UnidadeResponsavelSnapshot? unidade, bool noMesmoMomentoDaAnterior = false) => new(
        Ordem: 3,
        Inicio: Inicio,
        Fim: Fim,
        UnidadeResponsavel: unidade)
    { NoMesmoMomentoDaAnterior = noMesmoMomentoDaAnterior };

    private static RegraRecursoFase RegraDeRecurso() => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.ComInstanciaSuperior(2m, UnidadePrazo.DiasUteis),
        ProdutoDoResultadoId).Value!;

    private static ProdutoDaFase[] Produtos() =>
    [
        ProdutoDaFase.Reidratar(ProdutoDoResultadoId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
        ProdutoDaFase.Reidratar(ProdutoDoGabaritoId, "GABARITO", PapelProdutoFase.Preliminar),
    ];

    private static BancaRequerida Banca() => BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
        TiposDeBancaCopiadosDeTeste.Nomeada(TiposDeBancaCopiadosDeTeste.DeRecursos(new Guid("01a0d640-0000-7000-8000-0000000000c4")), "Banca de recursos"),
        []);

    private static FaseCronograma FaseCompleta(
        bool agrupaEtapas = true, bool permiteComplementacao = false, bool coletaInscricao = true, bool coletaIsencao = false,
        RegraDeHomologacaoDasInscricoes? regraDeHomologacao = null, bool noMesmoMomentoDaAnterior = false) =>
        FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            FaseId,
            Origem(agrupaEtapas, permiteComplementacao, coletaInscricao, coletaIsencao),
            Cronograma(new UnidadeResponsavelSnapshot(UnidadeId, "CEPS", "CEPS", "Centro de Processos Seletivos"), noMesmoMomentoDaAnterior),
            new PublicacoesDaFase(
                Produtos: Produtos(),
                EmiteParecerIndividual: true,
                RegraRecurso: RegraDeRecurso(),
                TiposDeDecisao: [TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo(), TipoMotivoDeDecisao.Classificacao.Codigo()],
                RegraDeHomologacao: regraDeHomologacao),
            [Banca()]);

    private static FaseCronograma FaseSemUnidadeNemPublicacoes() =>
        FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
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

    [Fact(DisplayName = "A banca da fase projeta a cópia do tipo: o código e o papel com rótulo, as marcas e os tipos de decisão julgados")]
    public void ProjetarFase_Banca_ProjetaACopiaDoTipo()
    {
        BancaRequeridaDto banca = ProjecaoDaFaseDoCronograma.ProjetarFase(FaseCompleta()).BancasRequeridas.Single();

        banca.Chave.Should().Be("banca");
        banca.Codigo.Should().BeEquivalentTo(CodigoComRotulo<TipoDeBanca>.De(TipoDeBanca.AnaliseDeRecursos));
        banca.Papel.Should().BeEquivalentTo(CodigoComRotulo<PapelDaBanca>.De(PapelDaBanca.JulgaAPrimeiraInstancia));
        banca.ExigeDuasOuMaisBancas.Should().BeFalse();
        banca.EncerraRecursoNaPrimeiraInstancia.Should().BeFalse();
        banca.TiposDeDecisaoJulgados.Should().BeEmpty();
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

    [Theory(DisplayName = "O cronograma projeta a marca de mesmo momento como a fase a tem")]
    [InlineData(true)]
    [InlineData(false)]
    public void ProjetarFase_Cronograma_ProjetaAMarca(bool marcada)
    {
        FaseCronograma fase = FaseCompleta(noMesmoMomentoDaAnterior: marcada);

        ProjecaoDaFaseDoCronograma.ProjetarFase(fase).Cronograma.NoMesmoMomentoDaAnterior.Should().Be(marcada);
    }

    [Fact(DisplayName = "O cronograma projeta a posição, o período e a unidade responsável")]
    public void ProjetarFase_Cronograma_ProjetaCadaCampo()
    {
        CronogramaDaFaseDto grupo = ProjecaoDaFaseDoCronograma.ProjetarFase(FaseCompleta()).Cronograma;

        grupo.Should().BeEquivalentTo(new CronogramaDaFaseDto(
            Ordem: 3,
            NoMesmoMomentoDaAnterior: false,
            Inicio: Inicio,
            Fim: Fim,
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

    [Fact(DisplayName = "Os tipos de decisão da fase saem com código e rótulo, em ordem alfabética do código")]
    public void ProjetarFase_TiposDeDecisao_SaemComCodigoERotulo()
    {
        IReadOnlyList<CodigoComRotulo<TipoMotivoDeDecisao>> tipos = ProjecaoDaFaseDoCronograma.ProjetarFase(FaseCompleta()).Publicacoes.TiposDeDecisao;

        tipos.Select(static t => t.Codigo).Should().Equal(
            TipoMotivoDeDecisao.Classificacao.Codigo(), TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo());
        tipos.Select(static t => t.Rotulo).Should().Equal(
            TipoMotivoDeDecisao.Classificacao.Rotulo(), TipoMotivoDeDecisao.IsencaoDaTaxa.Rotulo());
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
        dto.Publicacoes.RegraDeHomologacao.Should().BeNull();
    }

    [Fact(DisplayName = "A regra de homologação é projetada na forma da escrita: resultado, alternativas na ordem e enquadramento")]
    public void ProjetarFase_RegraDeHomologacao_ProjetaNaFormaDaEscrita()
    {
        FaseCronograma fase = FaseCompleta(regraDeHomologacao: RegraDeHomologacao());

        RegraDeHomologacaoDto regra = ProjecaoDaFaseDoCronograma.ProjetarFase(fase).Publicacoes.RegraDeHomologacao!;

        regra.ResultadoAtoCodigo.Should().Be("RESULTADO_CLASSIFICACAO");
        CondicaoDeHomologacaoDto[] condicoes = [.. regra.Alternativas.Single().Condicoes];
        condicoes.Select(static c => (c.Tipo, c.EdicaoDoExame, c.MotivoDesfavoravelCodigo)).Should().Equal(
            ("RESULTADO_NO_RETORNO_DO_INEP", 2025, "MOTIVO_B"), ("RESULTADO_NO_RETORNO_DO_INEP", 2024, null));
        condicoes[0].Enquadramento.Should().BeNull();
        CondicaoPrecondicaoDto enquadramento = condicoes[1].Enquadramento!.Single().Single();
        (enquadramento.Fato, enquadramento.Operador, enquadramento.Valor.GetString()).Should().Be(("MODALIDADE", "IGUAL", "AMPLA"));
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
            ParametrosEsperados(),
            []),
        TiposDeDecisao: [CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.Classificacao), CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.IsencaoDaTaxa)],
        RegraDeHomologacao: null);

    private static RegraDeHomologacaoDasInscricoes RegraDeHomologacao() => RegraDeHomologacaoDasInscricoes.Declarar(
        "RESULTADO_CLASSIFICACAO",
        [
            AlternativaDeCondicoes.Declarar([
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2025, "MOTIVO_B", null).Value!,
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2024, null, EnquadramentoDaModalidade()).Value!,
            ]).Value!,
        ]);

    private static PredicadoDnf EnquadramentoDaModalidade() => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AMPLA")).Value!)]).Value!;

    private static ParametrosRegraPrazoRecursoDto ParametrosEsperados() => new(
        2m,
        CodigoComRotulo<UnidadePrazo>.De(UnidadePrazo.DiasUteis),
        true,
        3m,
        CodigoComRotulo<UnidadePrazo>.De(UnidadePrazo.DiasUteis),
        CodigoComRotulo<EfeitoSuspensivo>.De(EfeitoSuspensivo.SemEfeitoSuspensivo),
        CodigoComRotulo<GuardaDaVaga>.De(GuardaDaVaga.AteOResultadoDefinitivo));
}
