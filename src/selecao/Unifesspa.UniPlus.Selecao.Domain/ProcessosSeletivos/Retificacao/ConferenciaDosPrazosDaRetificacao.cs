namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;

/// <summary>
/// O que a retificação não muda nos prazos do cronograma publicado (UNI-REQ-0211, UNI-REQ-0106). A fase concluída
/// fica integral no prazo. Na fase que já começou e não concluiu, o início não muda e o término não é antecipado;
/// a etapa que já começou também mantém o início. O término das inscrições nunca é antecipado, mas pode ser
/// prorrogado, mesmo vencido. A janela de isenção não se reduz e, encerrada, não reabre. Na fase que ainda não
/// começou, o início e o término mudam livremente, e o que a janela de isenção precisa cumprir é conferido pelas
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

        return andamento.Da(basePublicada) switch
        {
            AndamentoDaFase.Concluida => RecusaDaFaseConcluida(basePublicada, nova, caminho),
            AndamentoDaFase.EmAndamento => [.. RecusaDoInicio(basePublicada, nova, caminho), .. RecusaDoTermino(basePublicada, nova, caminho, andamento)],
            _ => RecusaDoTermino(basePublicada, nova, caminho, andamento),
        };
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
            && (RecusaDaJanelaDeIsencao(basePublicada, fimPublicado, fimNovo, andamento.Fatos.Instante)
                ?? RecusaDoTerminoAntecipado(basePublicada, fimPublicado, fimNovo, andamento)) is { } recusa
            ? [new FieldError($"{caminho}.fim", recusa)]
            : [];

    private static DomainError? RecusaDaJanelaDeIsencao(FaseCronograma basePublicada, DateTimeOffset fimPublicado, DateTimeOffset fimNovo, DateTimeOffset instante)
    {
        if (!basePublicada.ColetaSolicitacaoIsencao)
        {
            return null;
        }

        if (fimPublicado <= instante && fimNovo != fimPublicado)
        {
            return new DomainError(RetificacaoErrorCodes.JanelaDeIsencaoEncerradaReaberta, $"O período de isenção da fase '{basePublicada.Codigo}' já encerrou, e ela não reabre.");
        }

        return fimNovo < fimPublicado
            ? new DomainError(RetificacaoErrorCodes.JanelaDeIsencaoReduzida, $"O período de isenção da fase '{basePublicada.Codigo}' não se reduz.")
            : null;
    }

    private static DomainError? RecusaDoTerminoAntecipado(FaseCronograma basePublicada, DateTimeOffset fimPublicado, DateTimeOffset fimNovo, AndamentoDasFases andamento)
    {
        if (fimNovo >= fimPublicado)
        {
            return null;
        }

        if (basePublicada.ColetaInscricao)
        {
            return new DomainError(RetificacaoErrorCodes.TerminoDaInscricaoAntecipado, $"O término das inscrições da fase '{basePublicada.Codigo}' não é antecipado.");
        }

        return andamento.Da(basePublicada) == AndamentoDaFase.EmAndamento
            ? new DomainError(RetificacaoErrorCodes.TerminoDeFaseEmAndamentoAntecipado, $"A fase '{basePublicada.Codigo}' está em andamento, e o término dela não é antecipado.")
            : null;
    }

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
