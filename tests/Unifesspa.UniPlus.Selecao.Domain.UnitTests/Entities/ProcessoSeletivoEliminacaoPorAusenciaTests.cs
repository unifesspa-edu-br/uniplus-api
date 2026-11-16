namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A eliminação por ausência cita uma etapa e tem as guardas de etapa órfã da eliminação por
/// nota mínima, mais a recusa na etapa do ENEM, cuja falta é a falta em dia de prova.
/// </summary>
public sealed class ProcessoSeletivoEliminacaoPorAusenciaTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PSVR 2026", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static EtapaProcesso EtapaComNota(Guid? id = null) =>
        EtapaProcesso.Reidratar(
            id ?? Guid.CreateVersion7(),
            new IdentificacaoDaEtapa(Nome: "Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    private static EtapaProcesso EtapaSemNota() =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Entrevista", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ENTREVISTA", "Entrevista", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcesso EtapaNotaEnem(Guid? id = null) =>
        EtapaProcesso.Reidratar(
            id ?? Guid.CreateVersion7(),
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    private static ConfiguracaoClassificacao ClassificacaoComAusenciaEm(Guid etapaId, bool baseadoEmEnem = false) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)),
            RegraArredondamento = ReferenciaRegra.De(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('b', 64)),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('c', 64)),
            RegrasEliminacao = [RegraEliminacao.Criar(
                ReferenciaRegra.De(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "v1", new string('d', 64)),
                new ArgsElimAusenciaNaEtapa(etapaId)).Value!],
            BaseadoEmEnem = baseadoEmEnem,
            ResolucaoPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            QuadroPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : [],
            EscalaNotaFinal = 100m,
        }).Value!;

    [Fact(DisplayName = "Eliminação por ausência em etapa sem nota é aceita")]
    public void DefinirClassificacao_AusenciaEmEtapaSemNota_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso entrevista = EtapaSemNota();
        processo.DefinirEtapas([EtapaComNota(), entrevista], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirClassificacao(ClassificacaoComAusenciaEm(entrevista.Id), PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Eliminação por ausência em etapa com nota é aceita")]
    public void DefinirClassificacao_AusenciaEmEtapaComNota_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso objetiva = EtapaComNota();
        processo.DefinirEtapas([objetiva], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirClassificacao(ClassificacaoComAusenciaEm(objetiva.Id), PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Eliminação por ausência em etapa que não existe no processo é recusada no campo da etapa")]
    public void DefinirClassificacao_AusenciaEmEtapaInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaComNota()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirClassificacao(ClassificacaoComAusenciaEm(Guid.CreateVersion7()), PrecondicaoIfMatch.Ausente);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [("regrasEliminacao[0].etapaRef", "ProcessoSeletivo.EtapaRefEliminacaoInexistente")]);
    }

    [Fact(DisplayName = "Eliminação por ausência na etapa do ENEM é recusada no campo da etapa")]
    public void DefinirClassificacao_AusenciaNaEtapaDoEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso enem = EtapaNotaEnem();
        processo.DefinirEtapas([enem], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirClassificacao(ClassificacaoComAusenciaEm(enem.Id, baseadoEmEnem: true), PrecondicaoIfMatch.Ausente);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [("regrasEliminacao[0].etapaRef", "ProcessoSeletivo.AusenciaNaEtapaDoEnem")]);
    }

    [Fact(DisplayName = "Remover a etapa citada pela eliminação por ausência é recusado")]
    public void DefinirEtapas_RemoverEtapaDaAusencia_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso entrevista = EtapaSemNota();
        processo.DefinirEtapas([EtapaComNota(), entrevista], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoComAusenciaEm(entrevista.Id), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas([EtapaComNota()], PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaReferenciadaPorClassificacao");
    }

    [Fact(DisplayName = "A etapa citada pela eliminação por ausência não pode passar a ser a do ENEM")]
    public void DefinirEtapas_EtapaDaAusenciaViraEnem_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid etapaId = Guid.CreateVersion7();
        processo.DefinirEtapas([EtapaComNota(etapaId)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoComAusenciaEm(etapaId, baseadoEmEnem: true), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas([EtapaNotaEnem(etapaId)], PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AusenciaNaEtapaDoEnem");
    }
}
