namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As recusas de trocar o cronograma, na ordem em que orientam, e o plano. As fases
/// são casadas por <c>FaseCanonicaOrigemId</c>, a identidade estável de uma fase: <c>Ordem</c> é só
/// um atributo mutável dela, e casar por ela confundiria reordenar com trocar de fase.
/// </summary>
internal static class RegrasDoCronograma
{
    internal static Result<PlanoDoCronograma> Planejar(
        EstadoDaConfiguracao estado,
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias,
        FasesIniciadas iniciadas)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(iniciadas);

        DomainError? recusa = RecusaDaColecao(fases) ?? RecusaDaMudanca(estado, fases, precedencias, iniciadas);

        return recusa is not null
            ? Result<PlanoDoCronograma>.Failure(recusa)
            : RecusaDoCicloEDaJanelaDaEtapa(estado, fases) ?? Result<PlanoDoCronograma>.Success(MontarPlano(estado, fases));
    }

    private static Dictionary<Guid, FaseCronograma> PorOrigem(IEnumerable<FaseCronograma> fases) =>
        fases.ToDictionary(static f => f.FaseCanonicaOrigemId);

    // As recusas do que muda em relação ao cronograma que o processo já tem, na ordem em que orientam.
    private static DomainError? RecusaDaMudanca(
        EstadoDaConfiguracao estado,
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias,
        FasesIniciadas iniciadas)
    {
        Dictionary<Guid, FaseCronograma> antigasPorOrigem = PorOrigem(estado.CronogramaFases);
        Dictionary<Guid, FaseCronograma> novasPorOrigem = PorOrigem(fases);
        List<DocumentoExigido> documentosQueFicam = DocumentosQueFicam(estado, antigasPorOrigem, novasPorOrigem);

        return iniciadas.RecusaDaAlteracao(fases)
            ?? RecusaDaEtapaPodada(iniciadas, estado, antigasPorOrigem, novasPorOrigem)
            ?? RecusaDasFasesQueSaemOuMudam(estado, antigasPorOrigem, novasPorOrigem, documentosQueFicam)
            ?? RecusaDaAncoraNoFimDaInscricao(fases, documentosQueFicam)
            ?? PermutacaoCiclicaDeOrdem.Recusa(antigasPorOrigem, novasPorOrigem)
            ?? RecusaDePrecedencia(fases, precedencias);
    }

    // O documento exigido numa fase que também sai vai junto com ela, na poda; contá-lo como
    // referência viva recusaria uma remoção que o próprio comando resolve. Só quem fica reclama.
    private static List<DocumentoExigido> DocumentosQueFicam(
        EstadoDaConfiguracao estado,
        Dictionary<Guid, FaseCronograma> antigasPorOrigem,
        Dictionary<Guid, FaseCronograma> novasPorOrigem)
    {
        HashSet<Guid> fasesQueFicam = [.. antigasPorOrigem.Values
            .Where(f => novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))
            .Select(f => f.Id)];

        return [.. estado.DocumentosExigidos.Where(d => fasesQueFicam.Contains(d.ExigidoNaFaseId))];
    }

    // A etapa da fase que sai vai junto na poda: se ela já começou, a remoção da fase a levaria sem que ninguém a conferisse.
    private static DomainError? RecusaDaEtapaPodada(
        FasesIniciadas iniciadas,
        EstadoDaConfiguracao estado,
        Dictionary<Guid, FaseCronograma> antigasPorOrigem,
        Dictionary<Guid, FaseCronograma> novasPorOrigem)
    {
        HashSet<string> codigosRemovidos = [.. antigasPorOrigem.Values
            .Where(f => !novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))
            .Select(static f => f.Codigo)];

        return iniciadas.RecusaDaRemocaoDaEtapa(estado.Etapas.Where(e => e.FaseCodigo is not { } codigo || !codigosRemovidos.Contains(codigo)));
    }

    // A conclusão do ciclo recursal roda sobre o conjunto candidato, não sobre o agregado
    // corrente, e acumula as violações: duas fases mal declaradas saem na mesma resposta.
    // A etapa acontece dentro da fase que a abriga, e a fase é quem se move depois: a janela da etapa é conferida
    // contra o conjunto candidato, porque a etapa cuja fase está saindo sai junto na poda e não entra na conta.
    private static Result<PlanoDoCronograma>? RecusaDoCicloEDaJanelaDaEtapa(EstadoDaConfiguracao estado, IReadOnlyList<FaseCronograma> fases)
    {
        List<FieldError> violacoesDaConclusao = CicloRecursalDoCronograma.ConferirConclusaoDoCicloRecursal(fases);
        if (violacoesDaConclusao.Count > 0)
        {
            return Result<PlanoDoCronograma>.ValidationFailure(violacoesDaConclusao);
        }

        return JanelaDaEtapaNaFase.ViolacaoDaJanelaDaEtapaNaFase(estado.Etapas, JanelaDaEtapaNaFase.FasesPorCodigo(fases)) is { } etapaForaDaFase
            ? Result<PlanoDoCronograma>.Failure(etapaForaDaFase)
            : null;
    }

    internal static void AplicarUnidadesVivas(IEnumerable<FaseCronograma> fases, IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot> vivas)
    {
        ArgumentNullException.ThrowIfNull(vivas);

        foreach (FaseCronograma fase in fases)
        {
            if (fase.UnidadeResponsavelOrigemId is { } origemId && vivas.TryGetValue(origemId, out UnidadeResponsavelSnapshot? viva))
            {
                fase.RecopiarUnidadeResponsavel(viva);
            }
        }
    }

    private static PlanoDoCronograma MontarPlano(EstadoDaConfiguracao estado, IReadOnlyList<FaseCronograma> fases)
    {
        Dictionary<Guid, FaseCronograma> antigasPorOrigem = PorOrigem(estado.CronogramaFases);
        Dictionary<Guid, FaseCronograma> novasPorOrigem = PorOrigem(fases);
        List<(FaseCronograma Existente, FaseCronograma Nova)> retidas =
            [.. fases.Where(f => antigasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId)).Select(f => (antigasPorOrigem[f.FaseCanonicaOrigemId], f))];
        List<FaseCronograma> resultantes = [.. fases.Select(f => antigasPorOrigem.GetValueOrDefault(f.FaseCanonicaOrigemId) ?? f)];
        List<FaseCronograma> removidas = [.. antigasPorOrigem.Values.Where(f => !novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))];
        MudancaDaConfiguracao poda = PodaDoCronograma.Planejar(estado, removidas);
        return new PlanoDoCronograma(retidas, poda with { CronogramaFases = Troca.Substituindo(estado.CronogramaFases, resultantes) });
    }

    /// <summary>
    /// As recusas que só dependem da própria lista de fases, na ordem em que orientam: cronograma
    /// vazio, ordem repetida e fase canônica repetida.
    /// </summary>
    internal static DomainError? RecusaDaColecao(IReadOnlyList<FaseCronograma> fases) =>
        CronogramaVazio(fases) ?? OrdemRepetida(fases) ?? FaseCanonicaRepetida(fases);

    private static DomainError? CronogramaVazio(IReadOnlyList<FaseCronograma> fases) =>
        fases.Count == 0
            ? new DomainError(
                "ProcessoSeletivo.CronogramaFasesVazio",
                "O processo deve ter ao menos uma fase no cronograma.")
            : null;

    private static DomainError? OrdemRepetida(IReadOnlyList<FaseCronograma> fases)
    {
        List<int> ordens = [.. fases.Select(f => f.Ordem)];
        return ordens.Distinct().Count() != ordens.Count
            ? new DomainError(
                "ProcessoSeletivo.OrdemFaseDuplicada",
                "Cada fase deve ter uma ordem única dentro do cronograma.")
            : null;
    }

    private static DomainError? FaseCanonicaRepetida(IReadOnlyList<FaseCronograma> fases)
    {
        List<Guid> origens = [.. fases.Select(f => f.FaseCanonicaOrigemId)];
        return origens.Distinct().Count() != origens.Count
            ? new DomainError(
                "ProcessoSeletivo.FaseCanonicaDuplicada",
                "A mesma fase canônica não pode aparecer duas vezes no cronograma.")
            : null;
    }

    private static DomainError? RecusaDasFasesQueSaemOuMudam(
        EstadoDaConfiguracao estado,
        Dictionary<Guid, FaseCronograma> antigasPorOrigem,
        Dictionary<Guid, FaseCronograma> novasPorOrigem,
        List<DocumentoExigido> documentosQueFicam)
    {
        foreach (FaseCronograma antiga in antigasPorOrigem.Values)
        {
            DomainError? recusa = novasPorOrigem.TryGetValue(antiga.FaseCanonicaOrigemId, out FaseCronograma? nova)
                ? RecusaDaFaseRetida(estado, antiga, nova, documentosQueFicam)
                : RecusaDaFaseRemovida(estado, antiga, documentosQueFicam);
            if (recusa is not null)
            {
                return recusa;
            }
        }

        return null;
    }

    // A exigência declarada na fase removida sai junto, na poda. A âncora de idade máxima é outra
    // coisa: quem a declara é uma exigência de outra fase, que só usa esta como marco temporal, e
    // apagá-la por tabela seria remover configuração alheia. A etapa e o formulário da fase também só
    // saem se ninguém de fora depende deles.
    private static DomainError? RecusaDaFaseRemovida(EstadoDaConfiguracao estado, FaseCronograma antiga, List<DocumentoExigido> documentosQueFicam) =>
        FaseUsadaComoAncoraDeOutraFase(antiga, documentosQueFicam)
        ?? EtapaDaFaseCitadaPelaClassificacao(estado, antiga)
        ?? FormularioRespondidoNaFase(estado, antiga);

    private static DomainError? FaseUsadaComoAncoraDeOutraFase(FaseCronograma antiga, List<DocumentoExigido> documentosQueFicam) =>
        documentosQueFicam.Find(d => d.IdadeMaximaEmissao?.ReferenciaFaseId == antiga.Id) is { } ancora
            ? new DomainError(
                "FaseCronograma.ReferenciadaPorExigenciaViva",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida do cronograma, mas o documento '{ancora.TipoDocumentoNome}', exigido em outra fase, usa a janela dela como âncora de idade máxima de emissão.")
            : null;

    private static DomainError? EtapaDaFaseCitadaPelaClassificacao(EstadoDaConfiguracao estado, FaseCronograma antiga) =>
        estado.Etapas.FirstOrDefault(e =>
            string.Equals(e.FaseCodigo, antiga.Codigo, StringComparison.Ordinal)
            && CitacoesDasEtapas.EtapaCitadaPorDesempateOuClassificacao(estado, e.Id)) is { } etapa
            ? new DomainError(
                "FaseCronograma.EtapaDaFaseReferenciada",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida, mas a etapa '{etapa.Nome}' que acontece nela é referenciada por um critério de desempate ou por uma regra de eliminação — reconfigure a classificação antes.")
            : null;

    private static DomainError? FormularioRespondidoNaFase(EstadoDaConfiguracao estado, FaseCronograma antiga) =>
        estado.Formularios.FirstOrDefault(f => f.FaseId == antiga.Id) is { } formularioDaFase
            ? new DomainError(
                FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario,
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida, mas o formulário de {EstruturaFormulario.ParaToken(formularioDaFase.Finalidade)} é respondido nela — troque a fase do formulário antes.")
            : null;

    // Um extremo âncora que deixa de existir numa fase que continua: a conferência da exigência só
    // prova a coerência no instante da escrita dela, e sem esta recusa uma redefinição posterior do
    // cronograma deixaria a idade máxima apontando para uma âncora irresolvível.
    private static DomainError? RecusaDaFaseRetida(
        EstadoDaConfiguracao estado, FaseCronograma antiga, FaseCronograma nova, List<DocumentoExigido> documentosQueFicam)
    {
        if (antiga.PermiteComplementacao && !nova.PermiteComplementacao
            && RegrasDasExigencias.ExisteConsequenciaPendenciaReenvioNaFase(estado, antiga.Id))
        {
            return new DomainError(
                "FaseCronograma.PendenciaReenvioExigeComplementacao",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder PermiteComplementacao — é referenciada por uma exigência (folha ou grupo) com consequência PENDENCIA_REENVIO.");
        }

        bool perdeuInicio = antiga.Inicio is not null && nova.Inicio is null;
        bool perdeuFim = antiga.Fim is not null && nova.Fim is null;
        return (perdeuInicio || perdeuFim) && documentosQueFicam.Exists(d =>
            d.IdadeMaximaEmissao is { ReferenciaFaseId: { } refFaseId } idade
            && refFaseId == antiga.Id
            && ((perdeuInicio && idade.ReferenciaTipo == ReferenciaTipoIdadeEmissao.InicioFase)
                || (perdeuFim && idade.ReferenciaTipo == ReferenciaTipoIdadeEmissao.FimFase)))
            ? new DomainError(
                "IdadeMaximaEmissao.FaseExtremoAusente",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder o extremo usado como âncora de idade máxima de emissão por um documento exigido configurado.")
            : null;
    }

    // O fim da inscrição não usa ReferenciaFaseId (a âncora é implícita: a fase que coleta
    // inscrição), então as recusas por fase nunca pegam uma redefinição que deixa o cronograma sem
    // nenhuma fase de coleta com fim definido. A checagem é global: importa só se ainda sobra alguma.
    private static DomainError? RecusaDaAncoraNoFimDaInscricao(IReadOnlyList<FaseCronograma> fases, List<DocumentoExigido> documentosQueFicam) =>
        documentosQueFicam.Exists(d => d.IdadeMaximaEmissao?.ReferenciaTipo == ReferenciaTipoIdadeEmissao.FimInscricao)
        && !fases.Any(f => f.ColetaInscricao && f.Fim is not null)
            ? new DomainError(
                "IdadeMaximaEmissao.FaseExtremoAusente",
                "A redefinição do cronograma deixaria de ter uma fase que coleta inscrição com Fim definido, mas há documento exigido configurado com idade máxima de emissão ancorada em FIM_INSCRICAO.")
            : null;

    // A precedência é dado de cadastro: para toda aresta cujas duas fases estão no cronograma, a
    // antecessora vem antes e, se a aresta não permite sobreposição e ambas têm janela, termina antes
    // de a sucessora começar. A ausência de uma das duas fases não é violação.
    private static DomainError? RecusaDePrecedencia(IReadOnlyList<FaseCronograma> fases, IReadOnlyList<ArestaPrecedencia> precedencias)
    {
        Dictionary<string, FaseCronograma> porCodigo = new(StringComparer.Ordinal);
        foreach (FaseCronograma fase in fases)
        {
            porCodigo[fase.Codigo] = fase;
        }

        foreach (ArestaPrecedencia aresta in precedencias)
        {
            if (!porCodigo.TryGetValue(aresta.AntecessoraCodigo, out FaseCronograma? antecessora)
                || !porCodigo.TryGetValue(aresta.SucessoraCodigo, out FaseCronograma? sucessora))
            {
                continue;
            }

            if (antecessora.Ordem >= sucessora.Ordem)
            {
                return new DomainError(
                    "ProcessoSeletivo.PrecedenciaFaseViolada",
                    $"A fase '{aresta.AntecessoraCodigo}' (ordem {antecessora.Ordem}) precede '{aresta.SucessoraCodigo}' (ordem {sucessora.Ordem}) — a ordem declarada viola a precedência do cadastro.");
            }

            if (!aresta.PermiteSobreposicao
                && antecessora.Fim is { } fimAntecessora
                && sucessora.Inicio is { } inicioSucessora
                && fimAntecessora > inicioSucessora)
            {
                return new DomainError(
                    "ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida",
                    $"A janela da fase '{aresta.SucessoraCodigo}' ({sucessora.Inicio:O}–{sucessora.Fim:O}) se sobrepõe à da fase '{aresta.AntecessoraCodigo}' ({antecessora.Inicio:O}–{antecessora.Fim:O}), e o cadastro não permite sobreposição entre elas.");
            }
        }

        return null;
    }
}
