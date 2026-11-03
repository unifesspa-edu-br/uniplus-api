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
    /// As violações do fechamento do ciclo recursal nas etapas, uma por matéria deixada aberta.
    /// </summary>
    /// <remarks>
    /// A etapa não tem fase concluinte: o ciclo dela fecha dentro dela mesma. O preliminar de um
    /// tipo de ato exige o definitivo do mesmo tipo na mesma etapa, pelo mesmo pareamento que as
    /// fases usam (<see cref="CicloRecursal"/>). Sem I/O e sem navegação.
    /// </remarks>
    internal static List<FieldError> ViolacoesDoFechamentoDoCicloDasEtapas(IReadOnlyList<EtapaProcesso> etapas)
    {
        List<FieldError> violacoes = [];

        for (int indice = 0; indice < etapas.Count; indice++)
        {
            EtapaProcesso etapa = etapas[indice];
            foreach (string tipo in etapa.TiposComCicloAberto())
            {
                violacoes.Add(new($"etapas[{indice}].produtos", new DomainError(
                    ProcessoSeletivoErrorCodes.EtapaComPreliminarSemDefinitivo,
                    $"A etapa '{etapa.Nome}' publica o resultado preliminar do ato '{tipo}' e não publica o definitivo do mesmo tipo — a etapa não tem fase concluinte, e o ciclo dela fecha dentro dela.")));
            }
        }

        return violacoes;
    }

    /// <summary>
    /// As recusas de trocar o conjunto de etapas, na ordem em que orientam quem edita. Uma lista vazia
    /// é válida: o processo sem prova (classificação importada) não tem etapa.
    /// </summary>
    internal static Result ConferirSubstituicao(EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(estado);

        List<Guid> novosIds = [.. etapas.Select(e => e.Id)];
        Dictionary<string, FaseCronograma> fasesPorCodigo = JanelaDaEtapaNaFase.FasesPorCodigo(estado.CronogramaFases);

        // A coerência com o ENEM vem antes da regra geral da nota: quem tem só a etapa do ENEM, sem
        // peso, recebe a causa real. A classificação é gravada depois das etapas, e num processo novo
        // ela ainda é nula aqui.
        DomainError? recusa =
            OrdemDuplicada(etapas)
            ?? ValidarEtapaDeNotaDoEnem(etapas, estado.Classificacao)
            ?? NenhumaEtapaComponeNota(etapas)
            ?? EtapaCitadaPeloDesempate(estado, novosIds)
            ?? EtapaCitadaPelaClassificacao(estado, etapas, novosIds)
            ?? EtapaCitadaPelaExigenciaDocumental(estado, novosIds)
            ?? EtapaQueMudouDeFaseDaExigencia(estado, etapas)
            ?? EtapaSemFaseNoCronograma(etapas, fasesPorCodigo)
            ?? JanelaDaEtapaNaFase.ViolacaoDaJanelaDaEtapaNaFase(etapas, fasesPorCodigo);
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        List<FieldError> violacoesDoCiclo = ViolacoesDoFechamentoDoCicloDasEtapas(etapas);
        return violacoesDoCiclo.Count > 0 ? Result.ValidationFailure(violacoesDoCiclo) : Result.Success();
    }

    private static DomainError? OrdemDuplicada(IReadOnlyList<EtapaProcesso> etapas)
    {
        List<int> ordensInformadas = [.. etapas.Where(e => e.Ordem.HasValue).Select(e => e.Ordem!.Value)];
        return ordensInformadas.Distinct().Count() != ordensInformadas.Count
            ? new DomainError(
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "Cada etapa deve ter uma ordem única dentro do processo.")
            : null;
    }

    // Sem etapa que componha a nota o divisor da média é zero, e a nota final dividiria por zero.
    private static DomainError? NenhumaEtapaComponeNota(IReadOnlyList<EtapaProcesso> etapas) =>
        etapas.Count > 0 && !etapas.Any(e => e.ComponeNota)
            ? new DomainError(
                "ProcessoSeletivo.NenhumaEtapaComponeNota",
                "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final.")
            : null;

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
        EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas, List<Guid> novosIds)
    {
        if (estado.Classificacao is not { } classificacao)
        {
            return null;
        }

        Guid? etapaOrfa = classificacao.RegrasEliminacao
            .Select(static r => r.Args.EtapaReferenciada())
            .FirstOrDefault(etapaRef => etapaRef is { } id && !novosIds.Contains(id));
        if (etapaOrfa is not null)
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaReferenciadaPorClassificacao",
                $"A etapa {etapaOrfa} é referenciada por uma regra de eliminação da classificação e não pode ser removida sem antes reconfigurar a classificação.");
        }

        // A etapa que continua pode passar a ser a do ENEM, onde a ausência já é a falta em dia de prova.
        return classificacao.RegrasEliminacao.Any(r => AusenciaNaEtapaDoEnem(r.Args, etapas))
            ? AusenciaNaEtapaDoEnemRecusada()
            : null;
    }

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
    private static DomainError? EtapaQueMudouDeFaseDaExigencia(EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> etapas)
    {
        foreach (DocumentoExigido exigencia in estado.DocumentosExigidos)
        {
            if (exigencia.ExigidoNaEtapaId is not { } etapaRef)
            {
                continue;
            }

            EtapaProcesso? etapaNova = etapas.FirstOrDefault(e => e.Id == etapaRef);
            FaseCronograma? faseDaExigencia = estado.CronogramaFases.FirstOrDefault(f => f.Id == exigencia.ExigidoNaFaseId);
            if (etapaNova is null
                || faseDaExigencia is null
                || string.Equals(etapaNova.FaseCodigo, faseDaExigencia.Codigo, StringComparison.Ordinal))
            {
                continue;
            }

            return new DomainError(
                "DocumentoExigido.EtapaNaoPertenceAFase",
                $"A etapa {etapaRef} coleta o documento {exigencia.TipoDocumentoNome} na fase {faseDaExigencia.Codigo} e não pode passar para outra fase sem antes reconfigurar a exigência.");
        }

        return null;
    }

    // A etapa declara a fase pelo código canônico, e não pelo id, que não sobrevive à reconciliação
    // da restauração. Etapa sem código continua aceita.
    private static DomainError? EtapaSemFaseNoCronograma(
        IReadOnlyList<EtapaProcesso> etapas, Dictionary<string, FaseCronograma> fasesPorCodigo)
    {
        foreach (EtapaProcesso semFase in etapas)
        {
            if (semFase.FaseCodigo is { } codigo && !fasesPorCodigo.ContainsKey(codigo))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaSemFaseNoCronograma",
                    $"A etapa \"{semFase.Nome}\" declara a fase {codigo}, que não está no cronograma deste processo.");
            }
        }

        return null;
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
    internal static DomainError? ValidarEtapaDeNotaDoEnem(
        IEnumerable<EtapaProcesso> etapas,
        ConfiguracaoClassificacao? classificacao)
    {
        List<EtapaProcesso> etapasDoEnem = [.. etapas.Where(static e => e.DeclaraNotaDoEnem)];
        if (etapasDoEnem.Count == 0)
        {
            return null;
        }

        // A coerência com a classificação sai primeiro porque é ela que decide se a etapa pode
        // existir no processo: recusar antes por outra causa levaria o operador a corrigir uma
        // etapa que, sob esta classificação, ele teria de remover de todo modo.
        bool classificacaoAdmiteNotaDoEnem = classificacao is null
            || classificacao.CalculaPelosPesosPorAreaDoEnem;
        if (!classificacaoAdmiteNotaDoEnem)
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem",
                $"A etapa \"{etapasDoEnem[0].Nome}\" tem a nota vinda do ENEM e exige classificação baseada em ENEM, calculada pela média ponderada.");
        }

        if (etapasDoEnem.Count > 1)
        {
            string nomes = string.Join(", ", etapasDoEnem.Select(static e => $"\"{e.Nome}\""));
            return new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemDuplicada",
                $"O processo admite uma única etapa de nota do ENEM, e declara {etapasDoEnem.Count}: {nomes}.");
        }

        EtapaProcesso etapaDoEnem = etapasDoEnem[0];
        if (!etapaDoEnem.ComponeNota)
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e precisa compor a média: declare caráter classificatória ou ambas e um peso.");
        }

        if (etapaDoEnem.Bancas.Count > 0)
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemComBanca",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e não admite banca: ninguém atribui a nota, e quem julga o recurso é declarado na fase de recursos.");
        }

        if (etapaDoEnem.Produtos.Count > 0 || etapaDoEnem.Recursos.Any(static r => r.Ancora == AncoraDoRecurso.AtoPublicado))
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e não declara produto nem recurso ancorado em ato: o resultado é publicado pela fase, e o recurso coletivo sobre a lista geral é o da regra de recurso da fase. Na etapa cabe só o recurso individual, ancorado na ciência do candidato.");
        }

        return null;
    }

    /// <summary>
    /// A etapa é referenciada por configuração que não pertence à fase dela — critério de
    /// desempate por nota de etapa ou regra de eliminação que a cita.
    /// </summary>
    internal static bool EtapaReferenciadaPorClassificacaoOuDesempate(EstadoDaConfiguracao estado, Guid etapaId) =>
        EtapaReferenciadaPorDesempate(estado, etapaId)
        || (estado.Classificacao?.RegrasEliminacao ?? []).Any(regra => regra.Args.EtapaReferenciada() == etapaId);

    internal static bool EtapaReferenciadaPorDesempate(EstadoDaConfiguracao estado, Guid etapaId) =>
        estado.CriteriosDesempate.Any(c => c.Args is ArgsDesempateMaiorNotaEtapa args && args.EtapaRef == etapaId);

    /// <summary>As regras de eliminação por nota mínima que citam a etapa.</summary>
    internal static IEnumerable<ArgsElimNotaMinimaEtapa> EliminacoesPorNotaMinimaDaEtapa(EstadoDaConfiguracao estado, Guid etapaId) =>
        (estado.Classificacao?.RegrasEliminacao ?? [])
            .Select(static regra => regra.Args)
            .OfType<ArgsElimNotaMinimaEtapa>()
            .Where(args => args.EtapaRef == etapaId);
}
