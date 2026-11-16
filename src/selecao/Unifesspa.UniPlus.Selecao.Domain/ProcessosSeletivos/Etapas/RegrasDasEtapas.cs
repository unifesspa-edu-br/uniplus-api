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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
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
    internal static bool AusenciaNaEtapaDoEnem(ArgsRegraEliminacao args, IEnumerable<EtapaProcesso> etapas) =>
        args is ArgsElimAusenciaNaEtapa ausencia
        && etapas.Any(e => e.Id == ausencia.EtapaRef && e.DeclaraNotaDoEnem);

    internal static DomainError AusenciaNaEtapaDoEnemRecusada() => new(
        "ProcessoSeletivo.AusenciaNaEtapaDoEnem",
        "A eliminação por ausência não se aplica à etapa de nota do ENEM, cuja falta é tratada pela eliminação por falta em dia de prova do ENEM.");

    /// <summary>
    /// As recusas de trocar o conjunto de etapas, na ordem em que orientam quem edita: o conjunto, a coerência com o ENEM,
    /// o que a configuração já cita, a fase e a janela no cronograma e, por fim, o fechamento do ciclo recursal. Uma lista
    /// vazia é válida: o processo sem prova (classificação importada) não tem etapa.
    /// </summary>
    internal static Result ConferirSubstituicao(EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(estado);

        EtapasDoProcesso declaradas = EtapasDoProcesso.Das(etapas);

        // A coerência com o ENEM vem depois das causas do conjunto: a classificação é gravada depois das etapas, e num
        // processo novo ela ainda é nula aqui.
        DomainError? recusa = CausasDaRecusa.Primeira(declaradas.Conferir())
            ?? EtapaNotaEnemSemClassificacaoEnem(etapas, estado.Classificacao)
            ?? RecusaDasReferenciasDaConfiguracao(estado, etapas)
            ?? RecusaDaEtapaNoCronograma(estado, declaradas);

        return recusa is not null ? Result.Failure(recusa) : FechamentoDoCiclo(declaradas);
    }

    private static Result FechamentoDoCiclo(EtapasDoProcesso etapas)
    {
        IReadOnlyList<FieldError> violacoes = etapas.ConferirFechamentoDoCiclo();

        return violacoes.Count > 0 ? Result.ValidationFailure(violacoes) : Result.Success();
    }

    // O que a configuração já cita da etapa não pode ficar sem ela nem mudar de lugar.
    private static DomainError? RecusaDasReferenciasDaConfiguracao(EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas)
    {
        List<Guid> novosIds = [.. etapas.Select(e => e.Id)];

        return EtapaCitadaPeloDesempate(estado, novosIds)
            ?? EtapaCitadaPelaClassificacao(estado, etapas, novosIds)
            ?? EtapaCitadaPelaExigenciaDocumental(estado, novosIds)
            ?? EtapaQueMudouDeFaseDaExigencia(estado, etapas);
    }

    // A etapa declara a fase pelo código canônico, e não pelo id, que não sobrevive à reconciliação da restauração.
    private static DomainError? RecusaDaEtapaNoCronograma(EstadoDaConfiguracao estado, EtapasDoProcesso etapas)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(estado.CronogramaFases);

        return CausasDaRecusa.Primeira(EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas))
            ?? CausasDaRecusa.Primeira(EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas));
    }

    // Trocar as etapas deixaria o critério apontando para uma etapa removida: desempate inexecutável.
    private static DomainError? EtapaCitadaPeloDesempate(EstadoDaConfiguracao estado, List<Guid> novosIds)
    {
        foreach (CriterioDesempate criterio in estado.CriteriosDesempate)
        {
            if (criterio.Args is ArgsDesempateMaiorNotaEtapa args && !novosIds.Contains(args.EtapaRef))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaReferenciadaPorDesempate",
                    $"A etapa {args.EtapaRef} é referenciada por um critério de desempate (ordem {criterio.Ordem}) e não pode ser removida sem antes reconfigurar o desempate.");
            }
        }

        return null;
    }

    private static DomainError? EtapaCitadaPelaClassificacao(
        EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas, List<Guid> novosIds) =>
        estado.Classificacao is { } classificacao
            ? EtapaOrfaDeRegraDeEliminacao(classificacao, novosIds) ?? AusenciaQueTocaAEtapaDoEnem(classificacao, etapas)
            : null;

    private static DomainError? EtapaOrfaDeRegraDeEliminacao(ConfiguracaoClassificacao classificacao, List<Guid> novosIds) =>
        classificacao.RegrasEliminacao
            .Select(static r => r.Args.EtapaReferenciada())
            .FirstOrDefault(etapaRef => etapaRef is { } id && !novosIds.Contains(id)) is { } etapaOrfa
            ? new DomainError(
                "ProcessoSeletivo.EtapaReferenciadaPorClassificacao",
                $"A etapa {etapaOrfa} é referenciada por uma regra de eliminação da classificação e não pode ser removida sem antes reconfigurar a classificação.")
            : null;

    // A etapa que continua pode passar a ser a do ENEM, onde a ausência já é a falta em dia de prova.
    private static DomainError? AusenciaQueTocaAEtapaDoEnem(ConfiguracaoClassificacao classificacao, IReadOnlyList<EtapaProcesso> etapas) =>
        classificacao.RegrasEliminacao.Any(r => AusenciaNaEtapaDoEnem(r.Args, etapas))
            ? AusenciaNaEtapaDoEnemRecusada()
            : null;

    private static DomainError? EtapaCitadaPelaExigenciaDocumental(EstadoDaConfiguracao estado, List<Guid> novosIds)
    {
        DocumentoExigido? exigenciaOrfa = estado.DocumentosExigidos
            .FirstOrDefault(d => d.ExigidoNaEtapaId is { } etapaRef && !novosIds.Contains(etapaRef));
        return exigenciaOrfa is null
            ? null
            : new DomainError(
                "ProcessoSeletivo.EtapaReferenciadaPorExigenciaDocumental",
                $"A etapa {exigenciaOrfa.ExigidoNaEtapaId} coleta o documento {exigenciaOrfa.TipoDocumentoNome} e não pode ser removida sem antes reconfigurar a exigência.");
    }

    // Manter o id não basta: a exigência declara a fase E a etapa em que o documento é coletado, e só
    // este caminho pode mover a etapa de fase depois de ela já estar referenciada.
    private static DomainError? EtapaQueMudouDeFaseDaExigencia(EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas) =>
        estado.DocumentosExigidos
            .Select(exigencia => MudancaDeFaseDaEtapaExigida(estado, etapas, exigencia))
            .FirstOrDefault(static recusa => recusa is not null);

    private static DomainError? MudancaDeFaseDaEtapaExigida(EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas, DocumentoExigido exigencia)
    {
        EtapaProcesso? etapaNova = etapas.FirstOrDefault(e => e.Id == exigencia.ExigidoNaEtapaId);
        FaseCronograma? faseDaExigencia = estado.CronogramaFases.FirstOrDefault(f => f.Id == exigencia.ExigidoNaFaseId);

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
    /// A etapa que declara nota de origem no ENEM (<see cref="EtapaProcesso.DeclaraNotaDoEnem"/>):
    /// é no máximo uma por processo, porque a nota do ENEM é uma só por candidato; compõe a
    /// média, porque é para gerar a nota final que o processo baseado em ENEM a declara; não
    /// tem banca, porque ninguém atribui a nota e quem julga o recurso é declarado na fase de
    /// recursos; e não declara produto nem recurso ancorado em ato, porque o resultado é
    /// publicado pela fase e o recurso coletivo sobre a lista geral é o da regra de recurso da
    /// fase — uma janela por fato. O parecer individual é admitido, não exigido: nesta etapa
    /// ele é o espelho do cálculo gerado pelo sistema (notas por área, pesos do grupo
    /// congelado, média), e é dele que corre o recurso individual ancorado na ciência. Só
    /// convive com classificação baseada em ENEM <b>e</b> calculada pela média ponderada,
    /// que é a fórmula em que ela entra como componente.
    /// </summary>
    /// <param name="classificacao">
    /// <see langword="null"/> enquanto a classificação não foi gravada — aí a coerência
    /// com ela ainda não é conferível, e fica para quando for.
    /// </param>
    internal static DomainError? ConferirEtapaDeNotaDoEnem(
        IEnumerable<EtapaProcesso> etapas,
        ConfiguracaoClassificacao? classificacao)
    {
        List<EtapaProcesso> etapasDoEnem = [.. etapas.Where(static e => e.DeclaraNotaDoEnem)];

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

    private static DomainError? CausaDaEtapaUnicaDoEnem(EtapaProcesso etapaDoEnem) =>
        !etapaDoEnem.ComponeNota ? ConjuntoDasEtapas.NotaDoEnemNaoCompoeNota(etapaDoEnem.Nome)
        : etapaDoEnem.Bancas.Count > 0 ? ConjuntoDasEtapas.NotaDoEnemComBanca(etapaDoEnem.Nome)
        : etapaDoEnem.Produtos.Count > 0 || etapaDoEnem.Recursos.Any(static r => r.Ancora == AncoraDoRecurso.AtoPublicado)
            ? ConjuntoDasEtapas.NotaDoEnemComProdutoOuRecursoEmAto(etapaDoEnem.Nome)
        : null;

    /// <summary>
    /// A etapa é referenciada por configuração que não pertence à fase dela — critério de
    /// desempate por nota de etapa ou regra de eliminação que a cita.
    /// </summary>
    internal static bool EtapaReferenciadaPorClassificacaoOuDesempate(EstadoDaConfiguracao estado, Guid etapaId) =>
        EtapaReferenciadaPorDesempate(estado, etapaId)
        || (estado.Classificacao?.RegrasEliminacao ?? []).Any(regra => regra.Args.EtapaReferenciada() == etapaId);

    /// <summary>Como a configuração do processo cita a nota das etapas: pelo desempate e pela eliminação por nota mínima.</summary>
    internal static CitacoesDeNota CitacoesDeNotaDaConfiguracao(EstadoDaConfiguracao estado) => new(
        [.. estado.Etapas.Select(static e => e.Id).Where(id => EtapaReferenciadaPorDesempate(estado, id))],
        [.. estado.Etapas.SelectMany(etapa => EliminacoesPorNotaMinimaDaEtapa(estado, etapa.Id).Select(args => (etapa.Id, args.NotaMinima)))]);

    internal static bool EtapaReferenciadaPorDesempate(EstadoDaConfiguracao estado, Guid etapaId) =>
        estado.CriteriosDesempate.Any(c => c.Args is ArgsDesempateMaiorNotaEtapa args && args.EtapaRef == etapaId);

    /// <summary>As regras de eliminação por nota mínima que citam a etapa.</summary>
    internal static IEnumerable<ArgsElimNotaMinimaEtapa> EliminacoesPorNotaMinimaDaEtapa(EstadoDaConfiguracao estado, Guid etapaId) =>
        (estado.Classificacao?.RegrasEliminacao ?? [])
            .Select(static regra => regra.Args)
            .OfType<ArgsElimNotaMinimaEtapa>()
            .Where(args => args.EtapaRef == etapaId);
}
