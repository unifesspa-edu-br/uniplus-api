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

        DomainError? recusa = CronogramaVazio(fases) ?? OrdemRepetida(fases) ?? FaseCanonicaRepetida(fases);
        if (recusa is not null)
        {
            return Result<PlanoDoCronograma>.Failure(recusa);
        }

        Dictionary<Guid, FaseCronograma> antigasPorOrigem = estado.CronogramaFases.ToDictionary(f => f.FaseCanonicaOrigemId);
        Dictionary<Guid, FaseCronograma> novasPorOrigem = fases.ToDictionary(f => f.FaseCanonicaOrigemId);

        // O documento exigido numa fase que também sai vai junto com ela, na poda; contá-lo como
        // referência viva recusaria uma remoção que o próprio comando resolve. Só quem fica reclama.
        HashSet<Guid> fasesQueFicam = [.. antigasPorOrigem.Values
            .Where(f => novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))
            .Select(f => f.Id)];
        List<DocumentoExigido> documentosQueFicam =
            [.. estado.DocumentosExigidos.Where(d => fasesQueFicam.Contains(d.ExigidoNaFaseId))];

        recusa = RecusaDaUnidadeDeFaseIniciada(fases, iniciadas)
            ?? RecusaDasFasesQueSaemOuMudam(estado, antigasPorOrigem, novasPorOrigem, documentosQueFicam)
            ?? RecusaDaAncoraNoFimDaInscricao(fases, documentosQueFicam)
            ?? PermutacaoCiclicaDeOrdem.Recusa(antigasPorOrigem, novasPorOrigem)
            ?? RecusaDePrecedencia(fases, precedencias);
        if (recusa is not null)
        {
            return Result<PlanoDoCronograma>.Failure(recusa);
        }

        // A conclusão do ciclo recursal roda sobre o conjunto candidato, não sobre o agregado
        // corrente, e acumula as violações: duas fases mal declaradas saem na mesma resposta.
        List<FieldError> violacoesDaConclusao = CicloRecursalDoCronograma.ConferirConclusaoDoCicloRecursal(fases);
        if (violacoesDaConclusao.Count > 0)
        {
            return Result<PlanoDoCronograma>.ValidationFailure(violacoesDaConclusao);
        }

        // A etapa acontece dentro da fase que a abriga, e a fase é quem se move depois. Contra o
        // conjunto candidato: a etapa cuja fase está saindo sai junto na poda e não entra na conta.
        if (JanelaDaEtapaNaFase.ViolacaoDaJanelaDaEtapaNaFase(estado.Etapas, JanelaDaEtapaNaFase.FasesPorCodigo(fases)) is { } etapaForaDaFase)
        {
            return Result<PlanoDoCronograma>.Failure(etapaForaDaFase);
        }

        return Result<PlanoDoCronograma>.Success(MontarPlano(estado, fases, antigasPorOrigem, novasPorOrigem));
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

    private static PlanoDoCronograma MontarPlano(
        EstadoDaConfiguracao estado,
        IReadOnlyList<FaseCronograma> fases,
        Dictionary<Guid, FaseCronograma> antigasPorOrigem,
        Dictionary<Guid, FaseCronograma> novasPorOrigem)
    {
        List<(FaseCronograma Existente, FaseCronograma Nova)> retidas =
            [.. fases.Where(f => antigasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId)).Select(f => (antigasPorOrigem[f.FaseCanonicaOrigemId], f))];
        List<FaseCronograma> resultantes = [.. fases.Select(f => antigasPorOrigem.GetValueOrDefault(f.FaseCanonicaOrigemId) ?? f)];
        List<FaseCronograma> removidas = [.. antigasPorOrigem.Values.Where(f => !novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))];
        MudancaDaConfiguracao poda = PodaDoCronograma.Planejar(estado, removidas);
        return new PlanoDoCronograma(retidas, poda with { CronogramaFases = Troca.Substituindo(estado.CronogramaFases, resultantes) });
    }

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

    // A unidade que conduz a fase decide as ações dela, e a fase que já começou tem ato e registro feitos sob a
    // unidade publicada: trocá-la deixaria o que já aconteceu sem a unidade que o conduziu. A comparação é com a
    // versão publicada, e não com o rascunho anterior, para que retirar e reinserir a fase não contorne a regra.
    internal static DomainError? RecusaDaUnidadeDeFaseIniciada(IEnumerable<FaseCronograma> fases, FasesIniciadas iniciadas) =>
        fases.FirstOrDefault(iniciadas.UnidadeTrocada) is { } fase
            ? new DomainError(
                "FaseCronograma.UnidadeDeFaseIniciadaNaoMuda",
                $"A fase '{fase.Codigo}' (ordem {fase.Ordem}) já começou, e a unidade responsável por ela não muda.")
            : null;

    // A exigência declarada na fase removida sai junto, na poda. A âncora de idade máxima é outra
    // coisa: quem a declara é uma exigência de outra fase, que só usa esta como marco temporal, e
    // apagá-la por tabela seria remover configuração alheia. A etapa e o formulário da fase também só
    // saem se ninguém de fora depende deles.
    private static DomainError? RecusaDaFaseRemovida(EstadoDaConfiguracao estado, FaseCronograma antiga, List<DocumentoExigido> documentosQueFicam)
    {
        DocumentoExigido? ancoraDeOutraFase = documentosQueFicam.Find(d => d.IdadeMaximaEmissao?.ReferenciaFaseId == antiga.Id);
        if (ancoraDeOutraFase is not null)
        {
            return new DomainError(
                "FaseCronograma.ReferenciadaPorExigenciaViva",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida do cronograma, mas o documento '{ancoraDeOutraFase.TipoDocumentoNome}', exigido em outra fase, usa a janela dela como âncora de idade máxima de emissão.");
        }

        EtapaProcesso? etapaReferenciada = estado.Etapas.FirstOrDefault(e =>
            string.Equals(e.FaseCodigo, antiga.Codigo, StringComparison.Ordinal)
            && RegrasDasEtapas.EtapaReferenciadaPorClassificacaoOuDesempate(estado, e.Id));
        if (etapaReferenciada is not null)
        {
            return new DomainError(
                "FaseCronograma.EtapaDaFaseReferenciada",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida, mas a etapa '{etapaReferenciada.Nome}' que acontece nela é referenciada por um critério de desempate ou por uma regra de eliminação — reconfigure a classificação antes.");
        }

        return estado.Formularios.FirstOrDefault(f => f.FaseId == antiga.Id) is { } formularioDaFase
            ? new DomainError(
                FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario,
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida, mas o formulário de {EstruturaFormulario.ParaToken(formularioDaFase.Finalidade)} é respondido nela — troque a fase do formulário antes.")
            : null;
    }

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
