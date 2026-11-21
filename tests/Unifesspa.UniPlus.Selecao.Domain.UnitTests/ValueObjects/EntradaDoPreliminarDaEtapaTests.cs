namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que da etapa entra no resultado preliminar: mudar o que entra muda a entrada, e mudar só o que descreve a etapa
/// ou o que ela publica não muda.
/// </summary>
public sealed class EntradaDoPreliminarDaEtapaTests
{
    private const string Materia = "RESULTADO_REDACAO";
    private static readonly Guid OrigemDoTipo = Guid.Parse("0197d6f0-5555-7000-8000-000000000001");
    private static readonly DateTimeOffset Inicio = new(2027, 4, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, Func<EtapaProcesso>> QueEntram = new()
    {
        [nameof(EtapaProcesso.FaseCodigo)] = () => Etapa(identificacao: i => i with { FaseCodigo = "OUTRA_FASE" }),
        [nameof(EtapaProcesso.Carater)] = () => Etapa(identificacao: i => i with { Carater = CaraterEtapa.Classificatoria }),
        [nameof(EtapaProcesso.TipoEtapaOrigemId)] = () => Etapa(identificacao: i => i with { TipoEtapa = Tipo(Guid.Parse("0197d6f0-5555-7000-8000-000000000002")) }),
        [nameof(EtapaProcesso.Peso)] = () => Etapa(avaliacao: a => a with { Peso = 2m }),
        [nameof(EtapaProcesso.NotaMinima)] = () => Etapa(avaliacao: a => a with { NotaMinima = 5m }),
        [nameof(EtapaProcesso.NotaMaxima)] = () => Etapa(avaliacao: a => a with { NotaMaxima = 20m }),
        [nameof(EtapaProcesso.PassoDoLancamento)] = () => Etapa(avaliacao: a => a with { PassoDoLancamento = 0.5m }),
        [nameof(EtapaProcesso.FormaDeAvaliacao)] = () => Etapa(avaliacao: a => a with { FormaDeAvaliacao = FormaDeAvaliacao.Objetiva }),
        [nameof(EtapaProcesso.RegraConsolidacao)] = () => Etapa(avaliacao: a => a with { RegraConsolidacao = ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('a', 64)) }),
        [nameof(EtapaProcesso.EscalaDeAtendimento)] = () => Etapa(avaliacao: a => a with { EscalaDeAtendimento = [new("Atende", 1m), new("Atende em parte", 0.5m)] }),
        [nameof(EtapaProcesso.Inicio)] = () => Etapa(janela: (Inicio, null, false)),
        [nameof(EtapaProcesso.Fim)] = () => Etapa(janela: (null, Inicio.AddDays(3), false)),
        [nameof(EtapaProcesso.EmiteParecerIndividual)] = () => Etapa(janela: (null, null, true)),
        [nameof(EtapaProcesso.ItensDeAvaliacao)] = () => Etapa(itens: [new(null, "Clareza", 6m)]),
        [nameof(EtapaProcesso.Bancas)] = () => Etapa(completar: e => e.DefinirBancas([BancasDeTeste.DeRecursosDaEtapa()])),
        [nameof(EtapaProcesso.Recursos)] = () => Etapa(completar: e => AbrirRecurso(e, prazo: 2m, ancora: 0, Materia)),
        [nameof(EtapaProcesso.TiposDeDecisao)] = () => ComTipos(TipoMotivoDeDecisaoCodigo.Classificacao),
    };

    private static readonly Dictionary<string, Func<EtapaProcesso>> QueNaoEntram = new()
    {
        [nameof(EtapaProcesso.Nome)] = () => Etapa(identificacao: i => i with { Nome = "Outro nome" }),
        [nameof(EtapaProcesso.Ordem)] = () => Etapa(identificacao: i => i with { Ordem = 5 }),
        [nameof(EtapaProcesso.TipoEtapa)] = () => Etapa(identificacao: i => i with { TipoEtapa = Tipo(OrigemDoTipo, "OUTRO_CODIGO", "Outro tipo") }),
        [nameof(EtapaProcesso.Produtos)] = () => Etapa(completar: e => e.DefinirProdutos([ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar)])),
    };

    public static TheoryData<string> PropriedadesQueEntram => new(QueEntram.Keys);

    public static TheoryData<string> PropriedadesQueNaoEntram => new(QueNaoEntram.Keys);

    [Theory(DisplayName = "Mudar só a propriedade da etapa que entra no preliminar muda a entrada")]
    [MemberData(nameof(PropriedadesQueEntram))]
    public void MudarPropriedadeQueEntra_MudaAEntrada(string propriedade) =>
        QueEntram[propriedade]().EntradaDoPreliminar().Should().NotBe(Etapa().EntradaDoPreliminar());

    [Theory(DisplayName = "Mudar só a propriedade da etapa que não entra no preliminar não muda a entrada")]
    [MemberData(nameof(PropriedadesQueNaoEntram))]
    public void MudarPropriedadeQueNaoEntra_NaoMudaAEntrada(string propriedade) =>
        QueNaoEntram[propriedade]().EntradaDoPreliminar().Should().Be(Etapa().EntradaDoPreliminar());

    [Fact(DisplayName = "Toda propriedade da etapa classificada como entrada tem variação que a prova, e só elas")]
    public void ClassificadasComoEntrada_TemVariacaoQueAsProva() =>
        ClassificacaoDasPropriedadesDoPreliminarTests.PropriedadesQueEntram(ClassificacaoDasPropriedadesDoPreliminarTests.DaEtapa)
            .Should().Equal(QueEntram.Keys.Order(StringComparer.Ordinal));

    [Fact(DisplayName = "Etapas iguais montadas em separado, de Ids diferentes, têm a mesma entrada e o mesmo código de dispersão")]
    public void EtapasIguais_TemAMesmaEntrada()
    {
        EntradaDoPreliminar primeira = Etapa().EntradaDoPreliminar();
        EntradaDoPreliminar segunda = Etapa().EntradaDoPreliminar();

        segunda.Should().Be(primeira);
        segunda.GetHashCode().Should().Be(primeira.GetHashCode());
    }

    [Fact(DisplayName = "A escala do peso não muda a entrada: o mesmo número com mais casas decimais é o mesmo valor")]
    public void PesoComMaisCasasDecimais_NaoMudaAEntrada() =>
        Etapa(avaliacao: a => a with { Peso = 1.000m }).EntradaDoPreliminar().Should().Be(Etapa().EntradaDoPreliminar());

    [Fact(DisplayName = "A ordem em que os itens de avaliação são declarados não muda a entrada")]
    public void OrdemDosItens_NaoMudaAEntrada()
    {
        ItemDeAvaliacaoDeclarado clareza = new(null, "Clareza", 5m);
        ItemDeAvaliacaoDeclarado coesao = new(null, "Coesão", 5m);

        Etapa(itens: [clareza, coesao]).EntradaDoPreliminar().Should().Be(Etapa(itens: [coesao, clareza]).EntradaDoPreliminar());
    }

    [Fact(DisplayName = "O rótulo do item entra em NFC: a forma decomposta e a composta do mesmo texto são a mesma entrada")]
    public void RotuloEmFormasUnicodeDiferentes_NaoMudaAEntrada() =>
        Etapa(itens: [new(null, "Coesão", 5m)]).EntradaDoPreliminar().Should().Be(Etapa(itens: [new(null, "Coesão", 5m)]).EntradaDoPreliminar());

    [Fact(DisplayName = "O prazo da janela recursal entra na entrada")]
    public void PrazoDoRecurso_MudaAEntrada() =>
        Etapa(completar: e => AbrirRecurso(e, prazo: 3m, ancora: 0, Materia)).EntradaDoPreliminar()
            .Should().NotBe(Etapa(completar: e => AbrirRecurso(e, prazo: 2m, ancora: 0, Materia)).EntradaDoPreliminar());

    [Fact(DisplayName = "Mudar só a guarda da vaga, a unidade do prazo ou a instância superior do recurso muda a entrada")]
    public void ParametrosDoRecurso_MudamAEntrada()
    {
        EntradaDoPreliminar baseDaEntrada = ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.Declarados()).EntradaDoPreliminar();

        ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.Declarados(guardaDaVaga: "naoGuarda")).EntradaDoPreliminar().Should().NotBe(baseDaEntrada);
        ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.Horas)).EntradaDoPreliminar().Should().NotBe(baseDaEntrada);
        ComParametrosDoRecurso(ParametrosDoRecursoDeTeste.ComInstanciaSuperior()).EntradaDoPreliminar().Should().NotBe(baseDaEntrada);
    }

    [Fact(DisplayName = "Mudar só o prazo da 2ª instância não muda a entrada: ele vale pela versão vigente quando o indeferimento é publicado")]
    public void PrazoDaSegundaInstancia_NaoMudaAEntrada()
    {
        ParametrosDeclaradosDoRecurso comInstanciaSuperior = ParametrosDoRecursoDeTeste.ComInstanciaSuperior();

        ComParametrosDoRecurso(comInstanciaSuperior with { PrazoDaSegundaInstancia = new PrazoDeclarado(5m, "diasUteis") }).EntradaDoPreliminar()
            .Should().Be(ComParametrosDoRecurso(comInstanciaSuperior).EntradaDoPreliminar());
    }

    [Fact(DisplayName = "O procedimento declarado na janela recursal entra na entrada, pela regra, e a ordem de declaração não importa")]
    public void ProcedimentosDaJanela_EntramNaEntrada()
    {
        EntradaDoPreliminar recorrecao = ComProcedimentos(RegraProcedimentoRecursoCodigo.Recorrecao, TipoMotivoDeDecisao.Classificacao);
        EntradaDoPreliminar recalculo = ComProcedimentos(RegraProcedimentoRecursoCodigo.Recalculo, TipoMotivoDeDecisao.Classificacao);

        recorrecao.Should().NotBe(recalculo);
        recorrecao.Should().NotBe(Etapa(completar: e => AbrirRecurso(e, prazo: 2m, ancora: 0, Materia)).EntradaDoPreliminar());
        ProcedimentosEmOrdem(TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.HomologacaoDasInscricoes)
            .Should().Be(ProcedimentosEmOrdem(TipoMotivoDeDecisao.HomologacaoDasInscricoes, TipoMotivoDeDecisao.Classificacao));
    }

    [Fact(DisplayName = "A âncora da janela recursal se identifica pelo tipo de ato e pelo papel, nunca pelo Id do produto")]
    public void AncoraDoRecurso_PeloAtoENuncaPeloId()
    {
        EtapaProcesso naPrimeira = Etapa(completar: e => AbrirRecurso(e, prazo: 2m, ancora: 0, Materia, "RESULTADO_ANALISE"));
        EtapaProcesso naSegunda = Etapa(completar: e => AbrirRecurso(e, prazo: 2m, ancora: 1, Materia, "RESULTADO_ANALISE"));

        naPrimeira.EntradaDoPreliminar().Should().NotBe(naSegunda.EntradaDoPreliminar());
        naPrimeira.EntradaDoPreliminar().Should().Be(Etapa(completar: e => AbrirRecurso(e, prazo: 2m, ancora: 0, Materia, "RESULTADO_ANALISE")).EntradaDoPreliminar());
    }

    [Fact(DisplayName = "A regra de deliberação da banca da etapa entra na entrada")]
    public void DeliberacaoDaBanca_MudaAEntrada() =>
        Etapa(completar: e => e.DefinirBancas([BancasDeTeste.DeRecursosDaEtapa(BancasDeTeste.Regra(integrantes: 5, quorum: 3))])).EntradaDoPreliminar()
            .Should().NotBe(Etapa(completar: e => e.DefinirBancas([BancasDeTeste.DeRecursosDaEtapa()])).EntradaDoPreliminar());

    [Fact(DisplayName = "Trocar um tipo de decisão declarado pela etapa muda a entrada")]
    public void TrocarTipoDeDecisao_MudaAEntrada() =>
        ComTipos(TipoMotivoDeDecisaoCodigo.Classificacao).EntradaDoPreliminar()
            .Should().NotBe(ComTipos(TipoMotivoDeDecisaoCodigo.AnaliseDocumental).EntradaDoPreliminar());

    [Fact(DisplayName = "A ordem em que os tipos de decisão são declarados não muda a entrada")]
    public void OrdemDosTiposDeDecisao_NaoMudaAEntrada() =>
        ComTipos(TipoMotivoDeDecisaoCodigo.Classificacao, TipoMotivoDeDecisaoCodigo.AnaliseDocumental).EntradaDoPreliminar()
            .Should().Be(ComTipos(TipoMotivoDeDecisaoCodigo.AnaliseDocumental, TipoMotivoDeDecisaoCodigo.Classificacao).EntradaDoPreliminar());

    private static EtapaProcesso ComParametrosDoRecurso(ParametrosDeclaradosDoRecurso parametros) =>
        Etapa(completar: e => AbrirRecursoComParametros(e, parametros, ProcedimentosDeRecurso.Nenhum, 0, Materia));

    private static EntradaDoPreliminar ComProcedimentos(string regra, TipoMotivoDeDecisao tipo) =>
        Etapa(completar: e => AbrirRecursoComProcedimentos(e, 2m, 0, ProcedimentosDeRecurso.De([Procedimento(tipo, regra)]), Materia)).EntradaDoPreliminar();

    private static EntradaDoPreliminar ProcedimentosEmOrdem(params TipoMotivoDeDecisao[] tipos) =>
        Etapa(completar: e => AbrirRecursoComProcedimentos(
            e, 2m, 0, ProcedimentosDeRecurso.De([.. tipos.Select(tipo => Procedimento(tipo, RegraProcedimentoRecursoCodigo.Recalculo))]), Materia)).EntradaDoPreliminar();

    private static ProcedimentoDeRecursoDeclarado Procedimento(TipoMotivoDeDecisao tipo, string regra) =>
        ProcedimentoDeRecursoDeclarado.De(tipo, null, ReferenciaRegra.De(regra, "v1", new string('a', 64)));

    private static EtapaProcesso Etapa(
        Func<IdentificacaoDaEtapa, IdentificacaoDaEtapa>? identificacao = null,
        Func<DeclaracaoDaAvaliacaoDaEtapa, DeclaracaoDaAvaliacaoDaEtapa>? avaliacao = null,
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens = null,
        (DateTimeOffset? Inicio, DateTimeOffset? Fim, bool EmiteParecer)? janela = null,
        Action<EtapaProcesso>? completar = null)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            Alterar(identificacao, new IdentificacaoDaEtapa("Prova de redação", CaraterEtapa.Ambas, Tipo(OrigemDoTipo), 1, "AVALIACAO")),
            Alterar(avaliacao, new DeclaracaoDaAvaliacaoDaEtapa(1m, null, 10m, null, FormaDeAvaliacao.Humana, null, null)),
            itens ?? [new(null, "Clareza", 5m)]).Value!;
        etapa.DefinirJanelaEParecer(janela?.Inicio, janela?.Fim, janela?.EmiteParecer ?? false).IsSuccess.Should().BeTrue();
        completar?.Invoke(etapa);

        return etapa;
    }

    private static EtapaProcesso ComTipos(params string[] tipos) =>
        Etapa(completar: e => e.DefinirTiposDeDecisao(tipos).IsSuccess.Should().BeTrue());

    private static T Alterar<T>(Func<T, T>? alteracao, T valor) => alteracao is null ? valor : alteracao(valor);

    private static TipoEtapaSnapshot Tipo(Guid origem, string codigo = "PROVA_DE_REDACAO", string nome = "Prova de redação") =>
        TipoEtapaSnapshot.Criar(origem, codigo, nome, admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    // A âncora do recurso é um dos preliminares da própria etapa, escolhido pelo índice.
    private static void AbrirRecurso(EtapaProcesso etapa, decimal prazo, int ancora, params string[] atos) =>
        AbrirRecursoComProcedimentos(etapa, prazo, ancora, ProcedimentosDeRecurso.Nenhum, atos);

    private static void AbrirRecursoComProcedimentos(EtapaProcesso etapa, decimal prazo, int ancora, ProcedimentosDeRecurso procedimentos, params string[] atos) =>
        AbrirRecursoComParametros(etapa, ParametrosDoRecursoDeTeste.Declarados(prazo, UnidadePrazo.DiasUteis), procedimentos, ancora, atos);

    private static void AbrirRecursoComParametros(
        EtapaProcesso etapa, ParametrosDeclaradosDoRecurso parametros, ProcedimentosDeRecurso procedimentos, int ancora, params string[] atos)
    {
        List<ProdutoDaEtapa> produtos = [.. atos.Select(static ato => ProdutoDaEtapa.Criar(ato, PapelProdutoFase.Preliminar))];
        RecursoDaEtapa recurso = RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado,
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)),
            parametros,
            produtos[ancora].Id,
            procedimentos).Value!;

        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([recurso]).IsSuccess.Should().BeTrue();
    }
}
