namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A etapa cuja nota vem do ENEM: única por processo, sem lançamento, e só sob
/// classificação baseada em ENEM calculada pela média ponderada — conferida dos dois
/// lados, porque a classificação é gravada depois das etapas no fluxo de configuração.
/// </summary>
public sealed class ProcessoSeletivoEtapaNotaEnemTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PSVR 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static EtapaProcesso EtapaNotaEnem(decimal peso = 1m, int ordem = 1) =>
        EtapaProcesso.Criar("Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), TipoEtapaCodigo.NotaEnem, "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true).Value!, peso: peso, ordem: ordem).Value!;

    private static EtapaProcesso EtapaRedacao(decimal peso = 1m, int ordem = 2) =>
        EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true).Value!, peso: peso, ordem: ordem).Value!;

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

    [Fact(DisplayName = "A etapa declara nota do ENEM pelo código do tipo congelado")]
    public void DeclaraNotaDoEnem_PeloCodigoDoTipo()
    {
        EtapaNotaEnem().DeclaraNotaDoEnem.Should().BeTrue();
        EtapaRedacao().DeclaraNotaDoEnem.Should().BeFalse();
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

    [Fact(DisplayName = "Etapa de nota do ENEM com banca requerida é recusada — a nota não é lançada")]
    public void DefinirEtapas_NotaEnemComBanca_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComLancamento");
    }

    [Fact(DisplayName = "Etapa de nota do ENEM com parecer individual é recusada — a nota não é lançada")]
    public void DefinirEtapas_NotaEnemComParecer_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaNotaEnem();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComLancamento");
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
        EtapaProcesso primeira = EtapaProcesso.Criar("ENEM 2024", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), TipoEtapaCodigo.NotaEnem, "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 1).Value!;
        EtapaProcesso segunda = EtapaProcesso.Criar("ENEM 2025", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), TipoEtapaCodigo.NotaEnem, "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 2).Value!;

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
