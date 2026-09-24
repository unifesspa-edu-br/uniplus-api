namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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
        ProcessoSeletivo.Criar("PSVR 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static EtapaProcesso EtapaNotaEnem(decimal peso = 1m, int ordem = 1) =>
        EtapaProcesso.Criar("Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, peso: peso, ordem: ordem).Value!;

    private static EtapaProcesso EtapaRedacao(decimal peso = 1m, int ordem = 2) =>
        EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: peso, ordem: ordem).Value!;

    private static EtapaProcesso EtapaNotaEnemForaDaMedia(CaraterEtapa carater, int ordem = 1) =>
        EtapaProcesso.Criar("Nota do ENEM", carater, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, peso: null, ordem: ordem).Value!;

    private static ReferenciaRegra RegraDoRecurso =>
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!;

    private static ArgsRegraPrazoRecurso PrazoDoRecurso => new(
        PrazoValor: 48m, PrazoUnidade: UnidadePrazo.Horas,
        SuspensividadePrimeiraInstanciaValor: null, SuspensividadePrimeiraInstanciaUnidade: null,
        SuspensividadeSegundaInstanciaValor: null, SuspensividadeSegundaInstanciaUnidade: null);

    private static RecursoDaEtapa RecursoPorCiencia() =>
        RecursoDaEtapa.Criar(AncoraDoRecurso.CienciaIndividual, RegraDoRecurso, PrazoDoRecurso, Guid.Empty).Value!;

    private static RecursoDaEtapa RecursoEmAto() =>
        RecursoDaEtapa.Criar(AncoraDoRecurso.AtoPublicado, RegraDoRecurso, PrazoDoRecurso, Guid.CreateVersion7()).Value!;

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ConfiguracaoClassificacao ClassificacaoMediaPonderada(bool baseadoEmEnem) =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            1,
            [],
            baseadoEmEnem,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : []).Value!;

    private static ConfiguracaoClassificacao ClassificacaoImportadaDoEnem() =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            null,
            null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            1,
            [],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;

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
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Etapa", CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), codigo, "Etapa", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem).Value!,
            peso: 1m, ordem: 1).Value!;

        etapa.DeclaraNotaDoEnem.Should().Be(esperado);
    }

    [Fact(DisplayName = "Processo novo, sem classificação, aceita a etapa de nota do ENEM — a coerência fica para a classificação")]
    public void DefinirEtapas_SemClassificacao_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()], PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Arranjo híbrido: nota do ENEM peso 6 e redação peso 4 convivem e somam 10 no divisor da média")]
    public void DefinirEtapas_ArranjoHibrido_EntraNoDivisor()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: true), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem(peso: 6m), EtapaRedacao(peso: 4m)], PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.CalcularDivisorMedia().Should().Be(10m);
    }

    [Fact(DisplayName = "Duas etapas de nota do ENEM no mesmo processo são recusadas")]
    public void DefinirEtapas_DuasEtapasNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnem(ordem: 1), EtapaNotaEnem(ordem: 2)], PrecondicaoIfMatch.Ausente);

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

        Result result = processo.DefinirEtapas([EtapaNotaEnemForaDaMedia(carater), redacao], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
        result.Error.Message.Should().Contain("caráter").And.Contain("peso");
    }

    [Fact(DisplayName = "Só com a etapa do ENEM fora da média, a recusa é a dela, não a de nenhuma etapa compor a nota")]
    public void DefinirEtapas_SoNotaEnemForaDaMedia_RecusaEspecificaAntesDaGeral()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnemForaDaMedia(CaraterEtapa.Eliminatoria)], PrecondicaoIfMatch.Ausente);

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com banca é recusada")]
    public void DefinirEtapas_NotaEnemComBanca_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

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

        Result result = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Etapa de nota do ENEM sem parecer e sem recurso continua válida — os dois são admitidos, não exigidos")]
    public void DefinirEtapas_NotaEnemSemParecerNemRecurso_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()], PrecondicaoIfMatch.Ausente);

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
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", papel)]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
        result.Error.Message.Should().Contain("regra de recurso da fase");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com recurso ancorado em ato é recusada pela regra do ENEM, não pela âncora")]
    public void DefinirEtapas_NotaEnemComRecursoEmAto_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirRecursos([RecursoEmAto()]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
    }

    [Fact(DisplayName = "Ordem das recusas: duplicidade antes de fora da média, fora da média antes de banca, banca antes de produto")]
    public void DefinirEtapas_OrdemDasRecusas()
    {
        EtapaProcesso comTudo = EtapaNotaEnemForaDaMedia(CaraterEtapa.Eliminatoria);
        comTudo.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();
        comTudo.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();

        NovoProcesso().DefinirEtapas([comTudo, EtapaNotaEnem(ordem: 2)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
        NovoProcesso().DefinirEtapas([comTudo], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");

        EtapaProcesso comBancaEProduto = EtapaNotaEnem();
        comBancaEProduto.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();
        comBancaEProduto.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        NovoProcesso().DefinirEtapas([comBancaEProduto], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
    }

    [Fact(DisplayName = "Classificação incoerente sai antes de qualquer outra recusa da etapa do ENEM")]
    public void DefinirEtapas_ClassificacaoIncoerenteAntesDasDemais()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = EtapaNotaEnemForaDaMedia(CaraterEtapa.Eliminatoria);
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();

        processo.DefinirEtapas([etapa, EtapaNotaEnem(ordem: 2)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Banca e parecer continuam valendo para as demais etapas")]
    public void DefinirEtapas_OutraEtapaComBancaEParecer_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso redacao = EtapaRedacao();
        redacao.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();
        redacao.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem(), redacao], PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Voltar às etapas e incluir nota do ENEM num processo cuja classificação não é ENEM é recusado")]
    public void DefinirEtapas_ClassificacaoNaoEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Sob classificação que não é ENEM, a recusa aponta a classificação antes da banca — é ela que decide se a etapa pode existir")]
    public void DefinirEtapas_ClassificacaoNaoEnemEBanca_RecusaPelaClassificacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "A recusa por duplicidade nomeia as etapas de nota do ENEM declaradas")]
    public void DefinirEtapas_DuasEtapasNotaEnem_MensagemNomeiaAsEtapas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso primeira = EtapaProcesso.Criar("ENEM 2024", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, peso: 1m, ordem: 1).Value!;
        EtapaProcesso segunda = EtapaProcesso.Criar("ENEM 2025", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, peso: 1m, ordem: 2).Value!;

        Result result = processo.DefinirEtapas([primeira, segunda], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("\"ENEM 2024\"").And.Contain("\"ENEM 2025\"");
    }

    [Fact(DisplayName = "Classificação ENEM importada não admite etapa de nota do ENEM — não há fórmula que a componha")]
    public void DefinirEtapas_ClassificacaoEnemImportada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirClassificacao(ClassificacaoImportadaDoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([EtapaNotaEnem()], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Declarar classificação não ENEM depois da etapa de nota do ENEM é recusado")]
    public void DefinirClassificacao_NaoEnemComEtapaNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaNotaEnem()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false), PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
        processo.Classificacao.Should().BeNull("a classificação recusada não é gravada");
    }

    [Fact(DisplayName = "Declarar classificação ENEM importada depois da etapa de nota do ENEM é recusado")]
    public void DefinirClassificacao_EnemImportadaComEtapaNotaEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaNotaEnem()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoImportadaDoEnem(), PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "Classificação ENEM pela média ponderada aceita a etapa de nota do ENEM já gravada")]
    public void DefinirClassificacao_EnemMediaPonderada_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaNotaEnem()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: true), PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact(DisplayName = "Processo sem etapa de nota do ENEM não é afetado pela classificação não ENEM")]
    public void DefinirClassificacao_SemEtapaNotaEnem_NaoEnem_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaRedacao()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirClassificacao(ClassificacaoMediaPonderada(baseadoEmEnem: false), PrecondicaoIfMatch.Ausente);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }
}
