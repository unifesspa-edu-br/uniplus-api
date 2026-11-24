namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class RegrasDasEtapas
{
    /// <summary>
    /// A ausência à etapa do ENEM já é a falta em dia de prova do ENEM: a regra por etapa só
    /// vale para as demais.
    /// </summary>
    internal static bool AusenciaNaEtapaDoEnem(ParametrosRegraEliminacao parametros, IEnumerable<EtapaProcesso> etapas) =>
        parametros is ParametrosEliminacaoPorAusenciaNaEtapa ausencia
        && etapas.Any(e => e.Id == ausencia.EtapaId && e.DeclaraNotaDoEnem);

    internal static DomainError AusenciaNaEtapaDoEnemRecusada() => new(
        "ProcessoSeletivo.AusenciaNaEtapaDoEnem",
        "A eliminação por ausência não se aplica à etapa de nota do ENEM, cuja falta é tratada pela eliminação por falta em dia de prova do ENEM.");

    /// <summary>
    /// As causas de trocar o conjunto de etapas, todas juntas e na ordem em que orientam quem edita: o conjunto, a coerência com
    /// o ENEM, o que a configuração já cita e a fase e o período no cronograma. O fechamento do ciclo recursal é conferido na publicação. Uma
    /// lista vazia é válida: o processo sem prova (classificação importada) não tem etapa.
    /// </summary>
    internal static Result ConferirSubstituicao(ConfiguracaoDeclarada configuracao, IReadOnlyList<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        EtapasDoProcesso declaradas = EtapasDoProcesso.Das(etapas);
        IReadOnlyList<FieldError> causas = CausasDaSubstituicao(configuracao, etapas, declaradas);

        return causas.Count == 0 ? Result.Success() : Result.ValidationFailure(causas);
    }

    // A coerência com o ENEM vem depois das causas do conjunto: a classificação é gravada depois das etapas, e num processo
    // novo ela ainda é nula aqui. A etapa que declara fase fora do cronograma não tem janela a conferir contra ela.
    private static IReadOnlyList<FieldError> CausasDaSubstituicao(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<EtapaProcesso> etapas, EtapasDoProcesso declaradas)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases);

        return
        [
            .. declaradas.Conferir(),
            .. CausasDaRecusa.Da(EtapaNotaEnemSemClassificacaoEnem(etapas, configuracao.Avaliacao.Classificacao)),
            .. CausasDaRecusa.Da(RecusaDasReferenciasDaConfiguracao(configuracao, etapas)),
            .. EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, declaradas),
            .. EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, declaradas),
        ];
    }

    // O que a configuração já cita da etapa não pode ficar sem ela nem mudar de lugar.
    private static DomainError? RecusaDasReferenciasDaConfiguracao(ConfiguracaoDeclarada configuracao, IReadOnlyList<EtapaProcesso> etapas)
    {
        List<Guid> novosIds = [.. etapas.Select(e => e.Id)];

        return EtapaCitadaPeloDesempate(configuracao, novosIds)
            ?? EtapaCitadaPelaClassificacao(configuracao, etapas, novosIds)
            ?? EtapaCitadaPelaExigenciaDocumental(configuracao, novosIds)
            ?? EtapaQueMudouDeFaseDaExigencia(configuracao, etapas);
    }

    // Trocar as etapas deixaria o critério apontando para uma etapa removida: desempate inexecutável.
    private static DomainError? EtapaCitadaPeloDesempate(ConfiguracaoDeclarada configuracao, List<Guid> novosIds)
    {
        foreach (CriterioDesempate criterio in configuracao.Avaliacao.CriteriosDesempate)
        {
            if (criterio.Parametros is ParametrosDesempateMaiorNotaEtapa parametros && !novosIds.Contains(parametros.EtapaId))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaReferenciadaPorDesempate",
                    $"A etapa {parametros.EtapaId} é referenciada por um critério de desempate (ordem {criterio.Ordem}) e não pode ser removida sem antes reconfigurar o desempate.");
            }
        }

        return null;
    }

    private static DomainError? EtapaCitadaPelaClassificacao(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<EtapaProcesso> etapas, List<Guid> novosIds) =>
        configuracao.Avaliacao.Classificacao is { } classificacao
            ? EtapaOrfaDeRegraDeEliminacao(classificacao, novosIds) ?? AusenciaQueTocaAEtapaDoEnem(classificacao, etapas)
            : null;

    private static DomainError? EtapaOrfaDeRegraDeEliminacao(ConfiguracaoClassificacao classificacao, List<Guid> novosIds) =>
        classificacao.RegrasEliminacao
            .Select(static r => r.Parametros.EtapaReferenciada())
            .FirstOrDefault(etapaRef => etapaRef is { } id && !novosIds.Contains(id)) is { } etapaOrfa
            ? new DomainError(
                "ProcessoSeletivo.EtapaReferenciadaPorClassificacao",
                $"A etapa {etapaOrfa} é referenciada por uma regra de eliminação da classificação e não pode ser removida sem antes reconfigurar a classificação.")
            : null;

    // A etapa que continua pode passar a ser a do ENEM, onde a ausência já é a falta em dia de prova.
    private static DomainError? AusenciaQueTocaAEtapaDoEnem(ConfiguracaoClassificacao classificacao, IReadOnlyList<EtapaProcesso> etapas) =>
        classificacao.RegrasEliminacao.Any(r => AusenciaNaEtapaDoEnem(r.Parametros, etapas))
            ? AusenciaNaEtapaDoEnemRecusada()
            : null;

    private static DomainError? EtapaCitadaPelaExigenciaDocumental(ConfiguracaoDeclarada configuracao, List<Guid> novosIds)
    {
        DocumentoExigido? exigenciaOrfa = configuracao.Exigencias.DocumentosExigidos
            .FirstOrDefault(d => d.ExigidoNaEtapaId is { } etapaRef && !novosIds.Contains(etapaRef));
        return exigenciaOrfa is null
            ? null
            : new DomainError(
                "ProcessoSeletivo.EtapaReferenciadaPorExigenciaDocumental",
                $"A etapa {exigenciaOrfa.ExigidoNaEtapaId} coleta o documento {exigenciaOrfa.TipoDocumentoNome} e não pode ser removida sem antes reconfigurar a exigência.");
    }

    // Manter o id não basta: a exigência declara a fase E a etapa em que o documento é coletado, e só
    // este caminho pode mover a etapa de fase depois de ela já estar referenciada.
    private static DomainError? EtapaQueMudouDeFaseDaExigencia(ConfiguracaoDeclarada configuracao, IReadOnlyList<EtapaProcesso> etapas) =>
        configuracao.Exigencias.DocumentosExigidos
            .Select(exigencia => MudancaDeFaseDaEtapaExigida(configuracao, etapas, exigencia))
            .FirstOrDefault(static recusa => recusa is not null);

    private static DomainError? MudancaDeFaseDaEtapaExigida(ConfiguracaoDeclarada configuracao, IReadOnlyList<EtapaProcesso> etapas, DocumentoExigido exigencia)
    {
        EtapaProcesso? etapaNova = etapas.FirstOrDefault(e => e.Id == exigencia.ExigidoNaEtapaId);
        FaseCronograma? faseDaExigencia = configuracao.Cronograma.CronogramaFases.FirstOrDefault(f => f.Id == exigencia.ExigidoNaFaseId);

        return exigencia.ExigidoNaEtapaId is not null
            && etapaNova is not null
            && faseDaExigencia is not null
            && !string.Equals(etapaNova.FaseCodigo, faseDaExigencia.Codigo, StringComparison.Ordinal)
                ? new DomainError(
                    "DocumentoExigido.EtapaNaoPertenceAFase",
                    $"A etapa {exigencia.ExigidoNaEtapaId} coleta o documento {exigencia.TipoDocumentoNome} na fase {faseDaExigencia.Codigo} e não pode passar para outra fase sem antes reconfigurar a exigência.")
                : null;
    }

    /// <summary>
    /// A etapa que declara nota de origem no ENEM (<see cref="EtapaProcesso.DeclaraNotaDoEnem"/>) é no máximo uma por processo,
    /// porque a nota do ENEM é uma só por candidato; compõe a média, porque é para gerar a nota final que o processo baseado em
    /// ENEM a declara; e só convive com classificação baseada em ENEM <b>e</b> calculada pela média ponderada, que é a fórmula
    /// em que ela entra como componente. Não pode ter banca com o papel que avalia o mérito, porque ninguém atribui a nota.
    /// Também não declara
    /// produto nem recurso ancorado em ato, porque o resultado é publicado pela fase. Essas duas últimas regras são da
    /// declaração das etapas (<see cref="ConjuntoDasEtapas"/>), que numera as etapas como o cliente as enviou, e aqui não
    /// se repetem. O parecer individual é admitido, não exigido: nesta etapa ele é o espelho do cálculo gerado pelo sistema, e
    /// é dele que corre o recurso individual ancorado na ciência.
    /// </summary>
    /// <param name="classificacao">
    /// <see langword="null"/> enquanto a classificação não foi gravada — aí a coerência
    /// com ela ainda não é conferível, e fica para quando for.
    /// </param>
    internal static DomainError? ConferirEtapaDeNotaDoEnem(
        IEnumerable<EtapaProcesso> etapas,
        ConfiguracaoClassificacao? classificacao)
    {
        List<EtapaProcesso> todas = [.. etapas];
        List<EtapaProcesso> etapasDoEnem = [.. todas.Where(static e => e.DeclaraNotaDoEnem)];

        // A coerência com a classificação sai primeiro porque é ela que decide se a etapa pode existir no processo:
        // recusar antes por outra causa levaria o operador a corrigir uma etapa que, sob esta classificação, ele teria
        // de remover de todo modo.
        return etapasDoEnem.Count == 0 ? null
            : EtapaNotaEnemSemClassificacaoEnem(etapasDoEnem, classificacao)
                ?? (etapasDoEnem.Count > 1
                    ? ConjuntoDasEtapas.NotaDoEnemDuplicada([.. etapasDoEnem.Select(static e => e.Nome)])
                    : CausaDaEtapaUnicaDoEnem(etapasDoEnem[0]));
    }

    /// <summary>A etapa de nota do ENEM sob classificação que não é a baseada em ENEM calculada pela média ponderada.</summary>
    internal static DomainError? EtapaNotaEnemSemClassificacaoEnem(IEnumerable<EtapaProcesso> etapas, ConfiguracaoClassificacao? classificacao) =>
        classificacao is { CalculaPelosPesosPorAreaDoEnem: false } && etapas.FirstOrDefault(static e => e.DeclaraNotaDoEnem) is { } etapaDoEnem
            ? new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e exige classificação baseada em ENEM, calculada pela média ponderada.")
            : null;

    // A banca e o produto da etapa do ENEM são recusados na declaração das etapas, que numera as etapas como o cliente as enviou:
    // repeti-los aqui, por outra lista, daria à mesma etapa dois números.
    private static DomainError? CausaDaEtapaUnicaDoEnem(EtapaProcesso etapaDoEnem) =>
        !etapaDoEnem.ComponeNota ? ConjuntoDasEtapas.NotaDoEnemNaoCompoeNota(etapaDoEnem.Nome) : null;

    /// <summary>
    /// A etapa é referenciada por configuração que não pertence à fase dela — critério de
    /// desempate por nota de etapa ou regra de eliminação que a cita.
    /// </summary>
    internal static bool EtapaReferenciadaPorClassificacaoOuDesempate(ConfiguracaoDeclarada configuracao, Guid etapaId) =>
        EtapaReferenciadaPorDesempate(configuracao, etapaId)
        || (configuracao.Avaliacao.Classificacao?.RegrasEliminacao ?? []).Any(regra => regra.Parametros.EtapaReferenciada() == etapaId);

    /// <summary>Como a configuração do processo cita a nota das etapas: pelo desempate e pela eliminação por nota mínima.</summary>
    internal static CitacoesDeNota CitacoesDeNotaDaConfiguracao(ConfiguracaoDeclarada configuracao) => new(
        [.. configuracao.Avaliacao.Etapas.Select(static e => e.Id).Where(id => EtapaReferenciadaPorDesempate(configuracao, id))],
        [.. configuracao.Avaliacao.Etapas.SelectMany(etapa => EliminacoesPorNotaMinimaDaEtapa(configuracao, etapa.Id).Select(parametros => (etapa.Id, parametros.NotaMinima)))]);

    internal static bool EtapaReferenciadaPorDesempate(ConfiguracaoDeclarada configuracao, Guid etapaId) =>
        configuracao.Avaliacao.CriteriosDesempate.Any(c => c.Parametros is ParametrosDesempateMaiorNotaEtapa parametros && parametros.EtapaId == etapaId);

    /// <summary>As regras de eliminação por nota mínima que citam a etapa.</summary>
    internal static IEnumerable<ParametrosEliminacaoPorNotaMinimaNaEtapa> EliminacoesPorNotaMinimaDaEtapa(ConfiguracaoDeclarada configuracao, Guid etapaId) =>
        (configuracao.Avaliacao.Classificacao?.RegrasEliminacao ?? [])
            .Select(static regra => regra.Parametros)
            .OfType<ParametrosEliminacaoPorNotaMinimaNaEtapa>()
            .Where(parametros => parametros.EtapaId == etapaId);
}
