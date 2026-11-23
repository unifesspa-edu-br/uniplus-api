namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A etapa cuja nota vem do ENEM: única por processo, compõe a média, sem banca, sem produto
/// nem recurso em ato, e só sob
/// classificação baseada em ENEM calculada pela média ponderada — conferida dos dois
/// lados, porque a classificação é gravada depois das etapas no fluxo de configuração.
/// </summary>
public sealed class ProcessoSeletivoEtapaNotaEnemTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PSVR 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static EtapaProcesso EtapaNotaEnem(decimal peso = 1m, int ordem = 1) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcesso EtapaRedacao(decimal peso = 1m, int ordem = 2) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Redação", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcesso EtapaNotaEnemForaDaMedia(CaraterEtapa carater, int ordem = 1) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static ReferenciaRegra RegraDoRecurso =>
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64));

    private static ParametrosDeclaradosDoRecurso PrazoDoRecurso => ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas);

    private static RecursoDaEtapa RecursoPorCiencia() =>
        RecursoDaEtapa.Criar(AncoraDoRecurso.CienciaIndividual, RegraDoRecurso, PrazoDoRecurso, Guid.Empty).Value!;

    private static RecursoDaEtapa RecursoEmAto() =>
        RecursoDaEtapa.Criar(AncoraDoRecurso.AtoPublicado, RegraDoRecurso, PrazoDoRecurso, Guid.CreateVersion7()).Value!;

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static ConfiguracaoClassificacao ClassificacaoMediaPonderada(bool baseadoEmEnem) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            BaseadoEmEnem = baseadoEmEnem,
            ResolucaoPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            QuadroPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : [],
            EscalaNotaFinal = 100m,
        }).Value!;

    private static ConfiguracaoClassificacao ClassificacaoImportadaDoEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            BaseadoEmEnem = true,
        }).Value!;

    [Fact(DisplayName = "A etapa declara nota do ENEM pelo atributo do tipo congelado")]
    public void DeclaraNotaDoEnem_PeloAtributoDoTipo()
    {
        EtapaNotaEnem().DeclaraNotaDoEnem.Should().BeTrue();
        EtapaRedacao().DeclaraNotaDoEnem.Should().BeFalse();
    }

    [Theory(DisplayName = "O código do tipo não declara a nota do ENEM; só o atributo congelado declara")]
    [InlineData("NOTA_ENEM", false, false)]
    [InlineData("OUTRO_CODIGO", true, true)]
    public void DeclaraNotaDoEnem_NaoDependeDoCodigo(string codigo, bool notaDeOrigemNoEnem, bool esperado)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Etapa", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), codigo, "Etapa", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

        etapa.DeclaraNotaDoEnem.Should().Be(esperado);
    }

    [Fact(DisplayName = "Processo novo, sem classificação, aceita a etapa de nota do ENEM — a coerência fica para a classificação")]
    public void DefinirEtapas_SemClassificacao_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()]);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Arranjo híbrido: nota do ENEM peso 6 e redação peso 4 convivem e somam 10 no divisor da média")]
    public void DefinirEtapas_ArranjoHibrido_EntraNoDivisor()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: true)).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem(peso: 6m), EtapaRedacao(peso: 4m)]);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Declaracao().Avaliacao.CalcularDivisorMedia().Should().Be(10m);
    }

    [Fact(DisplayName = "Duas etapas de nota do ENEM no mesmo processo são recusadas")]
    public void DefinirEtapas_DuasEtapasNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnem(ordem: 1), EtapaNotaEnem(ordem: 2)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
    }

    [Theory(DisplayName = "Etapa de nota do ENEM que não compõe a média é recusada, pedindo caráter e peso")]
    [InlineData(CaraterEtapa.Eliminatoria)]
    [InlineData(CaraterEtapa.Classificatoria)]
    [InlineData(CaraterEtapa.Ambas)]
    public void DefinirEtapas_NotaEnemForaDaMedia_Recusa(CaraterEtapa carater)
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso redacao = EtapaRedacao();

        Result result = processo.DefinirEtapas([EtapaNotaEnemForaDaMedia(carater), redacao]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
        result.Error.Message.Should().Contain("caráter").And.Contain("peso");
    }

    [Fact(DisplayName = "Só com a etapa do ENEM fora da média, a recusa é a dela, não a de nenhuma etapa compor a nota")]
    public void DefinirEtapas_SoNotaEnemForaDaMedia_RecusaEspecificaAntesDaGeral()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnemForaDaMedia(CaraterEtapa.Eliminatoria)]);

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com banca é recusada")]
    public void DefinirEtapas_NotaEnemComBanca_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))]);

        Result result = processo.DefinirEtapas([etapa]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com parecer individual e recurso ancorado na ciência é aceita")]
    public void DefinirEtapas_NotaEnemComParecerERecursoPorCiencia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([RecursoPorCiencia()]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa]);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Etapa de nota do ENEM sem parecer e sem recurso continua válida — os dois são admitidos, não exigidos")]
    public void DefinirEtapas_NotaEnemSemParecerNemRecurso_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()]);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Theory(DisplayName = "Etapa de nota do ENEM com qualquer produto é recusada, apontando a regra de recurso da fase")]
    [InlineData(PapelProdutoFase.Preliminar)]
    [InlineData(PapelProdutoFase.Definitivo)]
    [InlineData(null)]
    public void DefinirEtapas_NotaEnemComProduto_Recusa(PapelProdutoFase? papel)
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", papel)]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa]);

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
        result.Error.Message.Should().Contain("regra de recurso da fase");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com recurso ancorado em ato é recusada pela regra do ENEM, não pela âncora")]
    public void DefinirEtapas_NotaEnemComRecursoEmAto_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirRecursos([RecursoEmAto()]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa]);

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
    }

    [Fact(DisplayName = "Ordem das recusas: duplicidade antes de fora da média, fora da média antes de banca, banca antes de produto")]
    public void DefinirEtapas_OrdemDasRecusas()
    {
        EtapaProcesso comTudo = EtapaNotaEnemForaDaMedia(CaraterEtapa.Eliminatoria);
        comTudo.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))]);
        comTudo.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();

        NovoProcesso().DefinirEtapas([comTudo, EtapaNotaEnem(ordem: 2)])
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
        NovoProcesso().DefinirEtapas([comTudo])
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");

        EtapaProcesso comBancaEProduto = EtapaNotaEnem();
        comBancaEProduto.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))]);
        comBancaEProduto.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        NovoProcesso().DefinirEtapas([comBancaEProduto])
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Fact(DisplayName = "Duas etapas do ENEM sob classificação incoerente: a causa do conjunto das etapas sai antes da classificação")]
    public void DefinirEtapas_DuplicidadeDoEnem_SaiAntesDaClassificacaoIncoerente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false)).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = EtapaNotaEnemForaDaMedia(CaraterEtapa.Eliminatoria);
        etapa.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))]);

        processo.DefinirEtapas([etapa, EtapaNotaEnem(ordem: 2)])
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
    }

    [Fact(DisplayName = "Banca e parecer continuam valendo para as demais etapas")]
    public void DefinirEtapas_OutraEtapaComBancaEParecer_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso redacao = EtapaRedacao();
        redacao.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))]);
        redacao.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem(), redacao]);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Voltar às etapas e incluir nota do ENEM num processo cuja classificação não é ENEM é recusado")]
    public void DefinirEtapas_ClassificacaoNaoEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false)).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Sob classificação que não é ENEM, a banca da etapa do ENEM é recusada antes da classificação, que só é apontada quando é a única causa")]
    public void DefinirEtapas_ClassificacaoNaoEnemEBanca_RecusaPelaBanca()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false)).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))]);

        Result result = processo.DefinirEtapas([etapa]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Fact(DisplayName = "Sob classificação que não é ENEM, a etapa do ENEM sem outra causa é recusada pela classificação")]
    public void DefinirEtapas_ClassificacaoNaoEnemSemOutraCausa_RecusaPelaClassificacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false)).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()]);

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "A recusa por duplicidade nomeia as etapas de nota do ENEM declaradas")]
    public void DefinirEtapas_DuasEtapasNotaEnem_MensagemNomeiaAsEtapas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso primeira = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "ENEM 2024", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        EtapaProcesso segunda = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "ENEM 2025", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

        Result result = processo.DefinirEtapas([primeira, segunda]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("\"ENEM 2024\"").And.Contain("\"ENEM 2025\"");
    }

    [Fact(DisplayName = "Classificação ENEM importada não admite etapa de nota do ENEM — não há fórmula que a componha")]
    public void DefinirEtapas_ClassificacaoEnemImportada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoImportadaDoEnem()).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Declarar classificação não ENEM depois da etapa de nota do ENEM é recusado")]
    public void DefinirClassificacao_NaoEnemComEtapaNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaNotaEnem()]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
        processo.Classificacao.Should().BeNull("a classificação recusada não é gravada");
    }

    [Fact(DisplayName = "Declarar classificação ENEM importada depois da etapa de nota do ENEM é recusado")]
    public void DefinirClassificacao_EnemImportadaComEtapaNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaNotaEnem()]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaDoEnem());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Classificação ENEM pela média ponderada aceita a etapa de nota do ENEM já gravada")]
    public void DefinirClassificacao_EnemMediaPonderada_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaNotaEnem()]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: true));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "A banca da etapa do ENEM só é recusada pela declaração das etapas: a coerência com a classificação não a repete")]
    public void ConferirEtapaDeNotaDoEnem_ComBancaQueAvalia_NaoRepeteARecusaDaDeclaracao()
    {
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))]);

        RegrasDasEtapas.ConferirEtapaDeNotaDoEnem([etapa], ClassificacaoMediaPonderada(baseadoEmEnem: true)).Should().BeNull();
        RegrasDasEtapas.ConferirEtapaDeNotaDoEnem([etapa], ClassificacaoMediaPonderada(baseadoEmEnem: false))!
            .Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
        NovoProcesso().DefinirEtapas([etapa]).Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Fact(DisplayName = "Processo sem etapa de nota do ENEM não é afetado pela classificação não ENEM")]
    public void DefinirClassificacao_SemEtapaNotaEnem_NaoEnem_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaRedacao()]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }
}
