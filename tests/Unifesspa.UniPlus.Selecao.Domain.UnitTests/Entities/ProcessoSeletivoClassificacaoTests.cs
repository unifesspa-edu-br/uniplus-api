namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoProcessoSeletivoEmRascunho;

/// <summary>
/// Classificação do <see cref="ProcessoSeletivo"/> — 15º bloco canônico (Story #775): o vínculo
/// com a raiz, a integridade da referência das eliminações às etapas e a
/// indistinguibilidade entre tipos de processo.
/// </summary>
/// <remarks>
/// A recusa e o aceite de ELIMINACAO-CORTE-REDACAO/ELIMINACAO_ZERO_EM_AREA por BaseadoEmEnem vivem em
/// ConfiguracaoClassificacaoTests: <c>Criar</c> valida essa invariante, então não existe
/// ConfiguracaoClassificacao ENEM-inválida para entregar a <c>DefinirClassificacao</c>. O que
/// permanece na raiz depende de <c>_etapas</c>: a indistinguibilidade entre tipos e a
/// atomicidade tardia via etapa_ref órfão.
/// </remarks>
public sealed class ProcessoSeletivoClassificacaoTests
{
    [Fact(DisplayName = "DefinirClassificacao vincula a configuração à raiz")]
    public void DefinirClassificacao_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirClassificacao(NovaClassificacao());

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao.Should().NotBeNull();
        processo.Classificacao!.ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "DefinirClassificacao com etapa_ref de eliminação existente no processo tem sucesso")]
    public void DefinirClassificacao_EtapaRefEliminacaoExistente_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaDeProva("Objetiva", CaraterEtapa.Classificatoria, 1, 1m, null);
        processo.DefinirEtapas([etapa]);

        Result result = processo.DefinirClassificacao(NovaClassificacao([EliminacaoPorNotaMinimaNaEtapa(etapa.Id)]));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "DefinirEtapas remover etapa referenciada por eliminação da classificação é recusado (a restrição sobrevive)")]
    public void DefinirEtapas_RemoverEtapaReferenciadaPorClassificacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComEliminacaoNaEtapa(out _);

        Result result = processo.DefinirEtapas(
            [EtapaDeProva("Entrevista", CaraterEtapa.Classificatoria, 1, 1m, null)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaReferenciadaPorClassificacao");
    }

    [Fact(DisplayName = "DefinirEtapas mantendo a mesma etapa referenciada pela classificação tem sucesso")]
    public void DefinirEtapas_MesmaEtapaReferenciadaPorClassificacao_Sucesso()
    {
        ProcessoSeletivo processo = ProcessoComEliminacaoNaEtapa(out EtapaProcesso etapa);

        Result result = processo.DefinirEtapas([etapa]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "DefinirClassificacao com etapa_ref de eliminação inexistente no processo é recusado")]
    public void DefinirClassificacao_EtapaRefEliminacaoInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaDeProva("Objetiva", CaraterEtapa.Classificatoria, 1, 1m, null)]);

        Result result = processo.DefinirClassificacao(
            NovaClassificacao([EliminacaoPorNotaMinimaNaEtapa(Guid.CreateVersion7())]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefEliminacaoInexistente");
    }

    [Fact(DisplayName = "DefinirClassificacao produz o mesmo resultado em processos de Tipo diferente com a mesma configuração (indistinguibilidade, #850)")]
    public void DefinirClassificacao_TiposDiferentesMesmaConfiguracao_ResultadoIdentico()
    {
        ProcessoSeletivo psiq = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        ProcessoSeletivo sisu = NovoProcesso("SiSU 2026", TiposDeProcessoDeTeste.SiSU);

        Result resultadoPsiq = psiq.DefinirClassificacao(ClassificacaoEnemComCorteEmArea());
        Result resultadoSisu = sisu.DefinirClassificacao(ClassificacaoEnemComCorteEmArea());

        resultadoPsiq.IsSuccess.Should().BeTrue(resultadoPsiq.Error?.Message);
        resultadoSisu.IsSuccess.Should().Be(resultadoPsiq.IsSuccess,
            "o rótulo TipoProcesso não pode decidir o resultado — só BaseadoEmEnem, dado igual nos dois processos");
    }

    private static ProcessoSeletivo ProcessoComEliminacaoNaEtapa(out EtapaProcesso etapa)
    {
        ProcessoSeletivo processo = NovoProcesso();
        etapa = EtapaDeProva("Objetiva", CaraterEtapa.Classificatoria, 1, 1m, null);
        processo.DefinirEtapas([etapa]);
        processo.DefinirClassificacao(NovaClassificacao([EliminacaoPorNotaMinimaNaEtapa(etapa.Id)]))
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static RegraEliminacao EliminacaoPorNotaMinimaNaEtapa(Guid etapaId) =>
        RegraEliminacao.Criar(
            ReferenciaRegra.De(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo(), "v1", new string('d', 64)),
            new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapaId, 4m)).Value!;

    private static ConfiguracaoClassificacao ClassificacaoEnemComCorteEmArea() =>
        NovaClassificacao(
            [
                RegraEliminacao.Criar(
                    ReferenciaRegra.De(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo(), "v1", new string('e', 64)),
                    new ParametrosEliminacaoPorCorteEmAreaDoEnem("REDACAO", 400m)).Value!,
            ],
            baseadoEmEnem: true);

    private static ConfiguracaoClassificacao NovaClassificacao(
        IReadOnlyList<RegraEliminacao>? regrasEliminacao = null, bool baseadoEmEnem = false) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1", new string('a', 64)),
            RegraArredondamento = ReferenciaRegra.De(RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", new string('b', 64)),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", new string('c', 64)),
            RegrasEliminacao = regrasEliminacao ?? [],
            BaseadoEmEnem = baseadoEmEnem,
            ResolucaoPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            QuadroPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : [],
            EscalaNotaFinal = 100m,
        }).Value!;
}
