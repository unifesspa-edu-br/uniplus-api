namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que da fase entra no resultado preliminar: mudar o que entra muda a entrada, e mudar só o que descreve a fase
/// ou o que ela publica não muda.
/// </summary>
public sealed class EntradaDoPreliminarDaFaseTests
{
    private const string Materia = "RESULTADO_PROVA";
    private static readonly Guid OrigemDaFase = Guid.Parse("0197d6f0-2222-7000-8000-000000000001");
    private static readonly Guid TipoDeBancaA = Guid.Parse("0197d6f0-3333-7000-8000-00000000000a");
    private static readonly Guid TipoDeBancaB = Guid.Parse("0197d6f0-3333-7000-8000-00000000000b");
    private static readonly DateTimeOffset Inicio = new(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, Func<FaseCronograma>> QueEntram = new()
    {
        [nameof(FaseCronograma.Codigo)] = () => Fase(origem: o => o with { Codigo = "PROVA_OBJETIVA" }),
        [nameof(FaseCronograma.Inicio)] = () => Fase(decisoes: d => d with { Inicio = Inicio.AddHours(1) }),
        [nameof(FaseCronograma.Fim)] = () => Fase(decisoes: d => d with { Fim = Inicio.AddDays(6) }),
        [nameof(FaseCronograma.UnidadeResponsavelOrigemId)] = () => Fase(decisoes: d => d with { UnidadeResponsavel = UnidadesDeTeste.Cga }),
        [nameof(FaseCronograma.EmiteParecerIndividual)] = () => Fase(publicacoes: p => p with { EmiteParecerIndividual = true }),
        [nameof(FaseCronograma.OrigemData)] = () => Fase(origem: o => o with { OrigemData = OrigemDataFase.NaExecucao }),
        [nameof(FaseCronograma.AgrupaEtapas)] = () => Fase(origem: o => o with { AgrupaEtapas = true }),
        [nameof(FaseCronograma.PermiteComplementacao)] = () => Fase(origem: o => o with { PermiteComplementacao = true }),
        [nameof(FaseCronograma.ColetaInscricao)] = () => Fase(origem: o => o with { ColetaInscricao = true }),
        [nameof(FaseCronograma.ColetaSolicitacaoIsencao)] = () => Fase(origem: o => o with { ColetaSolicitacaoIsencao = true }),
        [nameof(FaseCronograma.RegraRecurso)] = () => ComRecurso(prazo: 2m, ancora: 0, Materia),
        [nameof(FaseCronograma.BancasRequeridas)] = () => Fase(bancas: [Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental)]),
        [nameof(FaseCronograma.TiposDeDecisao)] = () => ComTipos(TipoMotivoDeDecisao.Classificacao.Codigo()),
    };

    private static readonly Dictionary<string, Func<FaseCronograma>> QueNaoEntram = new()
    {
        [nameof(FaseCronograma.Ordem)] = () => Fase(decisoes: d => d with { Ordem = 7 }),
        [nameof(FaseCronograma.UnidadeResponsavelSigla)] = () => Fase(decisoes: d => d with { UnidadeResponsavel = new(UnidadesDeTeste.Ceps.OrigemId, "CEPS-X", UnidadesDeTeste.Ceps.Nome) }),
        [nameof(FaseCronograma.UnidadeResponsavelNome)] = () => Fase(decisoes: d => d with { UnidadeResponsavel = new(UnidadesDeTeste.Ceps.OrigemId, UnidadesDeTeste.Ceps.Sigla, "Outro nome") }),
        [nameof(FaseCronograma.Produtos)] = () => Fase(publicacoes: p => p with { Produtos = [.. p.Produtos, ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo)] }),
    };

    public static TheoryData<string> PropriedadesQueEntram => new(QueEntram.Keys);

    public static TheoryData<string> PropriedadesQueNaoEntram => new(QueNaoEntram.Keys);

    [Theory(DisplayName = "Mudar só a propriedade da fase que entra no preliminar muda a entrada")]
    [MemberData(nameof(PropriedadesQueEntram))]
    public void MudarPropriedadeQueEntra_MudaAEntrada(string propriedade) =>
        QueEntram[propriedade]().EntradaDoPreliminar().Should().NotBe(Fase().EntradaDoPreliminar());

    [Fact(DisplayName = "Mudar só o prazo da 2ª instância não muda a entrada: ele vale pela versão vigente quando o indeferimento é publicado")]
    public void PrazoDaSegundaInstancia_NaoMudaAEntrada()
    {
        ParametrosDeclaradosDoRecurso comInstanciaSuperior = ParametrosDoRecursoDeTeste.ComInstanciaSuperior();

        ComParametrosDoRecurso(comInstanciaSuperior with { PrazoDaSegundaInstancia = new PrazoDeclarado(5m, "diasUteis") }).EntradaDoPreliminar()
            .Should().Be(ComParametrosDoRecurso(comInstanciaSuperior).EntradaDoPreliminar());
    }

    [Fact(DisplayName = "Mudar a guarda da vaga, a interposição ou a instância superior muda a entrada")]
    public void ParametrosDoRecurso_MudamAEntrada()
    {
        EntradaDoPreliminar baseDaEntrada = ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.Declarados()).EntradaDoPreliminar();

        ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.Declarados(guardaDaVaga: "naoGuarda")).EntradaDoPreliminar().Should().NotBe(baseDaEntrada);
        ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.Declarados(3m)).EntradaDoPreliminar().Should().NotBe(baseDaEntrada);
        ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.Horas)).EntradaDoPreliminar().Should().NotBe(baseDaEntrada);
        ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.ComInstanciaSuperior()).EntradaDoPreliminar().Should().NotBe(baseDaEntrada);
    }

    [Theory(DisplayName = "Mudar só a propriedade da fase que não entra no preliminar não muda a entrada")]
    [MemberData(nameof(PropriedadesQueNaoEntram))]
    public void MudarPropriedadeQueNaoEntra_NaoMudaAEntrada(string propriedade) =>
        QueNaoEntram[propriedade]().EntradaDoPreliminar().Should().Be(Fase().EntradaDoPreliminar());

    [Fact(DisplayName = "Toda propriedade da fase classificada como entrada tem variação que a prova, e só elas")]
    public void ClassificadasComoEntrada_TemVariacaoQueAsProva() =>
        ClassificacaoDasPropriedadesDoPreliminarTests.PropriedadesQueEntram(ClassificacaoDasPropriedadesDoPreliminarTests.DaFase)
            .Should().Equal(QueEntram.Keys.Order(StringComparer.Ordinal));

    [Fact(DisplayName = "Fases iguais montadas em separado, de Ids diferentes, têm a mesma entrada e o mesmo código de dispersão")]
    public void FasesIguais_TemAMesmaEntrada()
    {
        EntradaDoPreliminar primeira = ComRecurso(prazo: 2m, ancora: 0, Materia).EntradaDoPreliminar();
        EntradaDoPreliminar segunda = ComRecurso(prazo: 2m, ancora: 0, Materia).EntradaDoPreliminar();

        segunda.Should().Be(primeira);
        segunda.GetHashCode().Should().Be(primeira.GetHashCode());
    }

    [Fact(DisplayName = "O prazo da regra de recurso entra na entrada")]
    public void PrazoDoRecurso_MudaAEntrada() =>
        ComRecurso(prazo: 3m, ancora: 0, Materia).EntradaDoPreliminar().Should().NotBe(ComRecurso(prazo: 2m, ancora: 0, Materia).EntradaDoPreliminar());

    [Fact(DisplayName = "O procedimento declarado na regra de recurso entra na entrada, pela regra")]
    public void ProcedimentoDaRegraDeRecurso_MudaAEntrada()
    {
        FaseCronograma recalculo = ComRecursoEProcedimentos(2m, 0, Procedimentos(RegraProcedimentoRecursoCodigo.Recalculo), Materia);
        FaseCronograma recorrecao = ComRecursoEProcedimentos(2m, 0, Procedimentos(RegraProcedimentoRecursoCodigo.Recorrecao), Materia);

        recorrecao.EntradaDoPreliminar().Should().NotBe(recalculo.EntradaDoPreliminar());
        recalculo.EntradaDoPreliminar().Should().NotBe(ComRecurso(prazo: 2m, ancora: 0, Materia).EntradaDoPreliminar());
    }

    [Fact(DisplayName = "A âncora do recurso se identifica pelo tipo de ato e pelo papel, nunca pelo Id do produto")]
    public void AncoraDoRecurso_PeloAtoENuncaPeloId()
    {
        FaseCronograma naPrimeira = ComRecurso(prazo: 2m, ancora: 0, Materia, "RESULTADO_ANALISE");
        FaseCronograma naSegunda = ComRecurso(prazo: 2m, ancora: 1, Materia, "RESULTADO_ANALISE");

        naPrimeira.EntradaDoPreliminar().Should().NotBe(naSegunda.EntradaDoPreliminar());
        naPrimeira.EntradaDoPreliminar().Should().Be(ComRecurso(prazo: 2m, ancora: 0, Materia, "RESULTADO_ANALISE").EntradaDoPreliminar());
    }

    [Fact(DisplayName = "A regra de deliberação da banca entra na entrada")]
    public void DeliberacaoDaBanca_MudaAEntrada() =>
        Fase(bancas: [Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, BancasDeTeste.Regra(integrantes: 5, quorum: 3))]).EntradaDoPreliminar()
            .Should().NotBe(Fase(bancas: [Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, BancasDeTeste.Regra(integrantes: 3, quorum: 2))]).EntradaDoPreliminar());

    [Fact(DisplayName = "O papel que a banca copiou do tipo entra na entrada")]
    public void PapelCopiadoDaBanca_MudaAEntrada() =>
        ComBanca(papel: PapelDaBanca.AvaliaOMerito).EntradaDoPreliminar()
            .Should().NotBe(ComBanca(papel: PapelDaBanca.JulgaAPrimeiraInstancia).EntradaDoPreliminar());

    [Fact(DisplayName = "Os tipos de decisão que a banca copiou do tipo entram na entrada")]
    public void TiposDeDecisaoCopiadosDaBanca_MudamAEntrada() =>
        ComBanca(tipos: [TipoMotivoDeDecisao.Classificacao]).EntradaDoPreliminar()
            .Should().NotBe(ComBanca(tipos: [TipoMotivoDeDecisao.AnaliseDocumental]).EntradaDoPreliminar());

    [Fact(DisplayName = "As marcas que a banca copiou do tipo entram na entrada")]
    public void MarcasCopiadasDaBanca_MudamAEntrada()
    {
        ComBanca(exigeDuasOuMais: true).EntradaDoPreliminar().Should().NotBe(ComBanca().EntradaDoPreliminar());
        ComBanca(encerraNaPrimeira: true).EntradaDoPreliminar().Should().NotBe(ComBanca().EntradaDoPreliminar());
    }

    [Fact(DisplayName = "Trocar as regras entre duas bancas do mesmo tipo e dos mesmos candidatos atendidos muda a entrada")]
    public void RegrasTrocadasEntreBancasDoMesmoTipo_MudamAEntrada() =>
        Fase(bancas: [
                Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, BancasDeTeste.Regra(integrantes: 5, quorum: 3)),
                Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, BancasDeTeste.Regra(integrantes: 7, quorum: 4))]).EntradaDoPreliminar()
            .Should().NotBe(Fase(bancas: [
                Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, BancasDeTeste.Regra(integrantes: 5, quorum: 4)),
                Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, BancasDeTeste.Regra(integrantes: 7, quorum: 3))]).EntradaDoPreliminar());

    [Fact(DisplayName = "Os candidatos atendidos da banca entram na entrada, pelas origens das categorias")]
    public void CandidatosAtendidosDaBanca_MudamAEntrada()
    {
        Guid renda = Guid.Parse("0197d6f0-4444-7000-8000-000000000001");
        Guid raca = Guid.Parse("0197d6f0-4444-7000-8000-000000000002");

        Fase(bancas: [Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, candidatosAtendidos: [CategoriaJulgada.Criar(renda, "RENDA")])]).EntradaDoPreliminar()
            .Should().NotBe(Fase(bancas: [Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental, candidatosAtendidos: [CategoriaJulgada.Criar(raca, "RACA")])]).EntradaDoPreliminar());
    }

    [Fact(DisplayName = "A ordem em que as bancas são declaradas não muda a entrada")]
    public void OrdemDasBancas_NaoMudaAEntrada() =>
        Fase(bancas: [Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental), Banca(TipoDeBancaB, TipoDeBanca.Entrevista)]).EntradaDoPreliminar()
            .Should().Be(Fase(bancas: [Banca(TipoDeBancaB, TipoDeBanca.Entrevista), Banca(TipoDeBancaA, TipoDeBanca.AnaliseDocumental)]).EntradaDoPreliminar());

    [Fact(DisplayName = "Trocar um tipo de decisão declarado pela fase muda a entrada")]
    public void TrocarTipoDeDecisao_MudaAEntrada() =>
        ComTipos(TipoMotivoDeDecisao.Classificacao.Codigo()).EntradaDoPreliminar()
            .Should().NotBe(ComTipos(TipoMotivoDeDecisao.AnaliseDocumental.Codigo()).EntradaDoPreliminar());

    [Fact(DisplayName = "A ordem em que os tipos de decisão são declarados não muda a entrada")]
    public void OrdemDosTiposDeDecisao_NaoMudaAEntrada() =>
        ComTipos(TipoMotivoDeDecisao.Classificacao.Codigo(), TipoMotivoDeDecisao.AnaliseDocumental.Codigo()).EntradaDoPreliminar()
            .Should().Be(ComTipos(TipoMotivoDeDecisao.AnaliseDocumental.Codigo(), TipoMotivoDeDecisao.Classificacao.Codigo()).EntradaDoPreliminar());

    [Fact(DisplayName = "O instante da janela entra em UTC: o mesmo instante com outro deslocamento não muda a entrada")]
    public void InstanteComOutroDeslocamento_NaoMudaAEntrada() =>
        Fase(decisoes: d => d with { Inicio = Inicio.ToOffset(TimeSpan.FromHours(-3)) }).EntradaDoPreliminar().Should().Be(Fase().EntradaDoPreliminar());

    private static FaseCronograma Fase(
        Func<OrigemCanonicaDaFase, OrigemCanonicaDaFase>? origem = null,
        Func<DecisoesDeCronogramaDaFase, DecisoesDeCronogramaDaFase>? decisoes = null,
        Func<PublicacoesDaFase, PublicacoesDaFase>? publicacoes = null,
        IReadOnlyList<BancaRequerida>? bancas = null) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            Alterar(origem, new OrigemCanonicaDaFase(OrigemDaFase, "PROVA", OrigemDataFase.NoEdital, false, false, false, false)),
            Alterar(decisoes, new DecisoesDeCronogramaDaFase(1, Inicio, Inicio.AddDays(5), UnidadesDeTeste.Ceps)),
            Alterar(publicacoes, new PublicacoesDaFase([ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar)], false, null)),
            bancas ?? []).Value!;

    private static FaseCronograma ComBanca(
        PapelDaBanca papel = PapelDaBanca.AvaliaOMerito,
        IEnumerable<TipoMotivoDeDecisao>? tipos = null,
        bool exigeDuasOuMais = false,
        bool encerraNaPrimeira = false) =>
        Fase(bancas: [BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.Com(
                TipoDeBanca.AnaliseDocumental, papel, TipoDeBancaA, null, exigeDuasOuMais, encerraNaPrimeira, tipos ?? [TipoMotivoDeDecisao.Classificacao]),
            [],
            null)]);

    private static FaseCronograma ComTipos(params string[] tipos) => Fase(publicacoes: p => p with { TiposDeDecisao = tipos });

    private static T Alterar<T>(Func<T, T>? alteracao, T valor) => alteracao is null ? valor : alteracao(valor);

    // A âncora do recurso é um dos preliminares da própria fase, escolhido pelo índice.
    private static FaseCronograma ComRecurso(decimal prazo, int ancora, params string[] atos) =>
        ComRecursoEProcedimentos(prazo, ancora, ProcedimentosDeRecurso.Nenhum, atos);

    private static FaseCronograma ComParametrosDoRecurso(ParametrosDeclaradosDoRecurso parametros)
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar);
        RegraRecursoFase regra = RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)), parametros, preliminar.Id).Value!;

        return Fase(publicacoes: _ => new PublicacoesDaFase([preliminar], false, regra));
    }

    private static FaseCronograma ComRecursoEProcedimentos(decimal prazo, int ancora, ProcedimentosDeRecurso procedimentos, params string[] atos)
    {
        List<ProdutoDaFase> produtos = [.. atos.Select(static ato => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar))];
        RegraRecursoFase regra = RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)),
            ParametrosDoRecursoDeTeste.Declarados(prazo, UnidadePrazo.DiasUteis),
            produtos[ancora].Id,
            procedimentos).Value!;

        return Fase(publicacoes: _ => new PublicacoesDaFase(produtos, false, regra));
    }

    private static ProcedimentosDeRecurso Procedimentos(string regra) => ProcedimentosDeRecurso.De(
        [ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, ReferenciaRegra.De(regra, "v1", new string('a', 64)))]);

    private static BancaRequerida Banca(Guid origem, TipoDeBanca tipo, RegraDeDeliberacao? regra = null, IReadOnlyList<CategoriaJulgada>? candidatosAtendidos = null) =>
        BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.De(tipo, origem), candidatosAtendidos ?? [], regra);
}
