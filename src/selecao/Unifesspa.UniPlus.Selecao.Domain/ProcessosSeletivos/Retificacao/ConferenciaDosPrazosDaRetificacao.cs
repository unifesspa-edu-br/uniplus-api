namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a retificação não muda nos prazos do cronograma publicado (UNI-REQ-0211, UNI-REQ-0106). A fase concluída
/// pelo definitivo publicado fica integral no prazo. Na fase que já começou, o início não muda e o término não é
/// antecipado; a etapa que já começou também mantém o início. O término das inscrições nunca é antecipado, mas pode ser
/// prorrogado, mesmo vencido, desde que nenhuma fase posterior tenha começado: a fase sem definitivo que já concluiu
/// pelo fim do período não é prorrogada depois que uma fase posterior começou, e o período que ainda corre ou a fase que
/// nunca começou se prorrogam sempre. O período de isenção não se reduz e, iniciada e encerrada, não reabre. Na fase que
/// ainda não começou, o início e o término mudam livremente, e o que o período de isenção precisa cumprir é conferido pelas
/// regras de publicação sobre o cronograma novo. A fase retirada é recusa de <see cref="ConferenciaDasFasesIniciadas"/>.
/// A fase se identifica pela fase canônica de origem e a etapa pelo <c>Id</c>. As recusas se acumulam, cada uma no
/// campo do prazo (ADR-0125), e o campo aponta a fase ou a etapa pela posição dela na ordem canônica do documento novo, e não
/// na lista como veio; no término vale a primeira, do mais específico ao mais geral.
/// </summary>
public static class ConferenciaDosPrazosDaRetificacao
{
    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyCollection<FaseCronograma> fasesNovas, IReadOnlyCollection<EtapaProcesso> etapasNovas, AndamentoDasFases andamento)
    {
        ArgumentNullException.ThrowIfNull(fasesNovas);
        ArgumentNullException.ThrowIfNull(etapasNovas);
        ArgumentNullException.ThrowIfNull(andamento);

        Dictionary<Guid, (FaseCronograma Fase, int Posicao)> novasPorOrigem = FasesNoDocumentoPorOrigem(fasesNovas);

        return
        [
            .. andamento.FasesDaBase
                .Where(basePublicada => novasPorOrigem.ContainsKey(basePublicada.FaseCanonicaOrigemId))
                .SelectMany(basePublicada => RecusasDaFase(basePublicada, novasPorOrigem[basePublicada.FaseCanonicaOrigemId], andamento)),
            .. RecusasDasEtapasIniciadas(etapasNovas, andamento),
        ];
    }

    private static Dictionary<Guid, (FaseCronograma Fase, int Posicao)> FasesNoDocumentoPorOrigem(IEnumerable<FaseCronograma> fasesNovas) =>
        OrdemCanonicaDaConfiguracao.Fases(fasesNovas)
            .Select(static (fase, posicao) => (Fase: fase, Posicao: posicao))
            .ToDictionary(static par => par.Fase.FaseCanonicaOrigemId);

    private static IEnumerable<FieldError> RecusasDaFase(FaseCronograma basePublicada, (FaseCronograma Fase, int Posicao) noDocumento, AndamentoDasFases andamento)
    {
        FaseCronograma nova = noDocumento.Fase;
        string caminho = $"fases[{noDocumento.Posicao}]";

        AndamentoDaFase situacao = andamento.Da(basePublicada);
        if (situacao == AndamentoDaFase.Concluida && basePublicada.PublicaResultadoDefinitivo)
        {
            return RecusaDaFaseConcluida(basePublicada, nova, caminho);
        }

        return situacao == AndamentoDaFase.PorComecar
            ? RecusaDoTermino(basePublicada, nova, caminho, andamento)
            : [.. RecusaDoInicio(basePublicada, nova, caminho), .. RecusaDoTermino(basePublicada, nova, caminho, andamento)];
    }

    private static IEnumerable<FieldError> RecusaDaFaseConcluida(FaseCronograma basePublicada, FaseCronograma nova, string caminho)
    {
        if (basePublicada.Inicio != nova.Inicio)
        {
            yield return Recusa($"{caminho}.inicio", RetificacaoErrorCodes.FaseConcluidaAlterada, $"A fase '{basePublicada.Codigo}' já concluiu, e o início dela não muda.");
        }

        if (basePublicada.Fim != nova.Fim)
        {
            yield return Recusa($"{caminho}.fim", RetificacaoErrorCodes.FaseConcluidaAlterada, $"A fase '{basePublicada.Codigo}' já concluiu, e o término dela não muda.");
        }
    }

    private static IEnumerable<FieldError> RecusaDoInicio(FaseCronograma basePublicada, FaseCronograma nova, string caminho) =>
        basePublicada.Inicio == nova.Inicio
            ? []
            : [Recusa($"{caminho}.inicio", RetificacaoErrorCodes.InicioDeFaseIniciadaAlterado, $"A fase '{basePublicada.Codigo}' já começou, e o início dela não muda.")];

    // O término é conferido do mais específico ao mais geral, e só a primeira recusa vale no campo.
    private static IEnumerable<FieldError> RecusaDoTermino(FaseCronograma basePublicada, FaseCronograma nova, string caminho, AndamentoDasFases andamento) =>
        basePublicada.Fim is { } fimPublicado && nova.Fim is { } fimNovo
            && (RecusaDaJanelaDeIsencao(basePublicada, fimPublicado, fimNovo, andamento)
                ?? RecusaDaProrrogacaoComFasePosteriorIniciada(basePublicada, fimPublicado, fimNovo, andamento)
                ?? RecusaDoTerminoAntecipado(basePublicada, fimPublicado, fimNovo, andamento)) is { } recusa
            ? [new FieldError($"{caminho}.fim", recusa)]
            : [];

    // A isenção que nunca começou não foi encerrada e se prorroga; a que começou e passou do fim não reabre.
    private static DomainError? RecusaDaJanelaDeIsencao(FaseCronograma basePublicada, DateTimeOffset fimPublicado, DateTimeOffset fimNovo, AndamentoDasFases andamento)
    {
        if (!basePublicada.ColetaSolicitacaoIsencao)
        {
            return null;
        }

        if (fimPublicado < andamento.Fatos.Instante && fimNovo != fimPublicado && andamento.Da(basePublicada) != AndamentoDaFase.PorComecar)
        {
            return new DomainError(RetificacaoErrorCodes.JanelaDeIsencaoEncerradaReaberta, $"O período de isenção da fase '{basePublicada.Codigo}' já encerrou, e ela não reabre.");
        }

        return fimNovo < fimPublicado
            ? new DomainError(RetificacaoErrorCodes.JanelaDeIsencaoReduzida, $"O período de isenção da fase '{basePublicada.Codigo}' não se reduz.")
            : null;
    }

    // Só a fase sem definitivo que já concluiu pelo fim do período: a que ainda corre, a que nunca começou e a que espera o
    // definitivo se prorrogam mesmo com fase posterior iniciada por sobreposição.
    private static DomainError? RecusaDaProrrogacaoComFasePosteriorIniciada(FaseCronograma basePublicada, DateTimeOffset fimPublicado, DateTimeOffset fimNovo, AndamentoDasFases andamento)
    {
        if (fimNovo <= fimPublicado || !ConcluidaSemDefinitivo(basePublicada, andamento))
        {
            return null;
        }

        return PrimeiraFasePosteriorIniciada(basePublicada, andamento) is { } posterior
            ? new DomainError(
                RetificacaoErrorCodes.ProrrogacaoComFasePosteriorIniciada,
                $"A fase '{basePublicada.Codigo}' já concluiu e a fase '{posterior.Codigo}' já começou: a data final da fase '{basePublicada.Codigo}' não pode mais ser prorrogada.")
            : null;
    }

    private static bool ConcluidaSemDefinitivo(FaseCronograma fase, AndamentoDasFases andamento) =>
        !fase.PublicaResultadoDefinitivo && andamento.Da(fase) == AndamentoDaFase.Concluida;

    // Posterior é qualquer fase de momento maior na ordem do cronograma, a próxima ou a mais distante; a de mesmo momento não é.
    private static FaseCronograma? PrimeiraFasePosteriorIniciada(FaseCronograma fase, AndamentoDasFases andamento)
    {
        OrdemDoCronograma ordem = OrdemDoCronograma.Do(andamento.FasesDaBase);

        return andamento.FasesDaBase
            .Where(outra => ordem.Posterior(outra, fase) && andamento.Fatos.FasesIniciadas.Contem(outra.FaseCanonicaOrigemId))
            .OrderBy(static outra => outra.Ordem)
            .ThenBy(static outra => outra.Codigo, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static DomainError? RecusaDoTerminoAntecipado(FaseCronograma basePublicada, DateTimeOffset fimPublicado, DateTimeOffset fimNovo, AndamentoDasFases andamento)
    {
        if (fimNovo >= fimPublicado)
        {
            return null;
        }

        return basePublicada.ColetaInscricao
            ? new DomainError(RetificacaoErrorCodes.TerminoDaInscricaoAntecipado, $"O término das inscrições da fase '{basePublicada.Codigo}' não é antecipado.")
            : RecusaDoTerminoDaFaseIniciada(basePublicada, andamento.Da(basePublicada));
    }

    private static DomainError? RecusaDoTerminoDaFaseIniciada(FaseCronograma basePublicada, AndamentoDaFase situacao) => situacao switch
    {
        AndamentoDaFase.EmAndamento => new DomainError(RetificacaoErrorCodes.TerminoDeFaseEmAndamentoAntecipado, $"A fase '{basePublicada.Codigo}' está em andamento, e o término dela não é antecipado."),
        AndamentoDaFase.Concluida => new DomainError(RetificacaoErrorCodes.FaseConcluidaAlterada, $"A fase '{basePublicada.Codigo}' já concluiu, e o término dela não é antecipado."),
        _ => null,
    };

    private static IEnumerable<FieldError> RecusasDasEtapasIniciadas(IReadOnlyCollection<EtapaProcesso> etapasNovas, AndamentoDasFases andamento)
    {
        Dictionary<Guid, (EtapaProcesso Etapa, int Posicao)> novasPorId = OrdemCanonicaDaConfiguracao.Etapas(etapasNovas)
            .Select(static (etapa, posicao) => (Etapa: etapa, Posicao: posicao))
            .ToDictionary(static par => par.Etapa.Id);

        return andamento.EtapasDaBase
            .Where(etapa => andamento.Fatos.FasesIniciadas.ContemEtapa(etapa.Id)
                && novasPorId.TryGetValue(etapa.Id, out (EtapaProcesso Etapa, int Posicao) nova) && nova.Etapa.Inicio != etapa.Inicio)
            .Select(etapa => Recusa(
                $"etapas[{novasPorId[etapa.Id].Posicao}].inicio",
                RetificacaoErrorCodes.InicioDeEtapaIniciadaAlterado,
                $"A etapa '{etapa.Nome}' já começou, e o início dela não muda."));
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
