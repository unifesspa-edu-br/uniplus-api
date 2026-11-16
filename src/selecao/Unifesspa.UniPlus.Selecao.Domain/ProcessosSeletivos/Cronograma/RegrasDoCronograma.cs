namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
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

        CronogramaDoProcesso candidato = CronogramaDoProcesso.Do(fases);
        DomainError? recusa = CausasDaRecusa.Primeira(candidato.ConferirEstrutura())
            ?? RecusaDaMudanca(estado, fases, iniciadas)
            ?? CausasDaRecusa.Primeira(candidato.ConferirPrecedencia(precedencias));

        return recusa is not null
            ? Result<PlanoDoCronograma>.Failure(recusa)
            : RecusaDoCicloEDaJanelaDaEtapa(estado, candidato) ?? Result<PlanoDoCronograma>.Success(MontarPlano(estado, fases));
    }

    private static Dictionary<Guid, FaseCronograma> PorOrigem(IEnumerable<FaseCronograma> fases) =>
        fases.ToDictionary(static f => f.FaseCanonicaOrigemId);

    // As recusas do que muda em relação ao cronograma que o processo já tem, na ordem em que orientam; a precedência do cadastro vem depois.
    private static DomainError? RecusaDaMudanca(
        EstadoDaConfiguracao estado,
        IReadOnlyList<FaseCronograma> fases,
        FasesIniciadas iniciadas)
    {
        Dictionary<Guid, FaseCronograma> antigasPorOrigem = PorOrigem(estado.CronogramaFases);
        Dictionary<Guid, FaseCronograma> novasPorOrigem = PorOrigem(fases);
        List<DocumentoExigido> documentosQueFicam = DocumentosQueFicam(estado, antigasPorOrigem, novasPorOrigem);

        return RecusaDaFaseOuEtapaIniciada(iniciadas, estado, fases, antigasPorOrigem, novasPorOrigem)
            ?? RecusaDasFasesQueSaemOuMudam(estado, antigasPorOrigem, novasPorOrigem, documentosQueFicam)
            ?? RecusaDaAncoraNoFimDaInscricao(fases, documentosQueFicam)
            ?? PermutacaoCiclicaDeOrdem.Recusa(antigasPorOrigem, novasPorOrigem);
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
    private static DomainError? RecusaDaFaseOuEtapaIniciada(
        FasesIniciadas iniciadas,
        EstadoDaConfiguracao estado,
        IReadOnlyList<FaseCronograma> fases,
        Dictionary<Guid, FaseCronograma> antigasPorOrigem,
        Dictionary<Guid, FaseCronograma> novasPorOrigem)
    {
        HashSet<string> codigosRemovidos = [.. antigasPorOrigem.Values
            .Where(f => !novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))
            .Select(static f => f.Codigo)];
        List<EtapaProcesso> etapasQueFicam = [.. estado.Etapas.Where(e => e.FaseCodigo is not { } codigo || !codigosRemovidos.Contains(codigo))];

        return ConferenciaDasFasesIniciadas.Conferir(iniciadas, fases, etapasQueFicam) is [{ } primeira, ..] ? primeira.Error : null;
    }

    // O ciclo recursal e a janela da etapa são conferidos contra o conjunto candidato, não contra o agregado corrente:
    // duas fases mal declaradas saem na mesma resposta, e a etapa cuja fase está saindo sai junto na poda e não entra na conta.
    private static Result<PlanoDoCronograma>? RecusaDoCicloEDaJanelaDaEtapa(EstadoDaConfiguracao estado, CronogramaDoProcesso candidato)
    {
        IReadOnlyList<FieldError> violacoesDaConclusao = candidato.ConferirConclusaoDoCiclo();
        if (violacoesDaConclusao.Count > 0)
        {
            return Result<PlanoDoCronograma>.ValidationFailure(violacoesDaConclusao);
        }

        return CausasDaRecusa.Primeira(EtapasNoCronograma.ConferirJanelaDasEtapas(candidato, EtapasDoProcesso.Das(estado.Etapas))) is { } etapaForaDaFase
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
        JanelaDaFaseUsadaPorOutraExigencia(antiga, documentosQueFicam)
        ?? EtapaDaFaseReferenciadaPelaClassificacao(estado, antiga)
        ?? FormularioRespondidoNaFase(estado, antiga);

    private static DomainError? JanelaDaFaseUsadaPorOutraExigencia(FaseCronograma antiga, List<DocumentoExigido> documentosQueFicam) =>
        documentosQueFicam.Find(d => d.IdadeMaximaEmissao?.ReferenciaFaseId == antiga.Id) is { } ancoraDeOutraFase
            ? new DomainError(
                "FaseCronograma.ReferenciadaPorExigenciaViva",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida do cronograma, mas o documento '{ancoraDeOutraFase.TipoDocumentoNome}', exigido em outra fase, usa a janela dela como âncora de idade máxima de emissão.")
            : null;

    private static DomainError? EtapaDaFaseReferenciadaPelaClassificacao(EstadoDaConfiguracao estado, FaseCronograma antiga) =>
        estado.Etapas.FirstOrDefault(e =>
            string.Equals(e.FaseCodigo, antiga.Codigo, StringComparison.Ordinal)
            && RegrasDasEtapas.EtapaReferenciadaPorClassificacaoOuDesempate(estado, e.Id)) is { } etapaReferenciada
            ? new DomainError(
                "FaseCronograma.EtapaDaFaseReferenciada",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida, mas a etapa '{etapaReferenciada.Nome}' que acontece nela é referenciada por um critério de desempate ou por uma regra de eliminação — reconfigure a classificação antes.")
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
        EstadoDaConfiguracao estado, FaseCronograma antiga, FaseCronograma nova, List<DocumentoExigido> documentosQueFicam) =>
        ComplementacaoQueAPendenciaDeReenvioExige(estado, antiga, nova) ?? ExtremoQueAAncoraDeIdadeUsa(antiga, nova, documentosQueFicam);

    private static DomainError? ComplementacaoQueAPendenciaDeReenvioExige(EstadoDaConfiguracao estado, FaseCronograma antiga, FaseCronograma nova) =>
        antiga.PermiteComplementacao && !nova.PermiteComplementacao
        && RegrasDasExigencias.ExisteConsequenciaPendenciaReenvioNaFase(estado, antiga.Id)
            ? new DomainError(
                "FaseCronograma.PendenciaReenvioExigeComplementacao",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder PermiteComplementacao — é referenciada por uma exigência (folha ou grupo) com consequência PENDENCIA_REENVIO.")
            : null;

    private static DomainError? ExtremoQueAAncoraDeIdadeUsa(FaseCronograma antiga, FaseCronograma nova, List<DocumentoExigido> documentosQueFicam)
    {
        ReferenciaTipoIdadeEmissao[] extremosPerdidos = [.. ExtremosPerdidos(antiga, nova)];

        return documentosQueFicam.Exists(d => UsaUmDosExtremos(d, antiga.Id, extremosPerdidos))
            ? new DomainError(
                "IdadeMaximaEmissao.FaseExtremoAusente",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder o extremo usado como âncora de idade máxima de emissão por um documento exigido configurado.")
            : null;
    }

    private static IEnumerable<ReferenciaTipoIdadeEmissao> ExtremosPerdidos(FaseCronograma antiga, FaseCronograma nova)
    {
        if (antiga.Inicio is not null && nova.Inicio is null)
        {
            yield return ReferenciaTipoIdadeEmissao.InicioFase;
        }

        if (antiga.Fim is not null && nova.Fim is null)
        {
            yield return ReferenciaTipoIdadeEmissao.FimFase;
        }
    }

    private static bool UsaUmDosExtremos(DocumentoExigido documento, Guid faseId, ReferenciaTipoIdadeEmissao[] extremos) =>
        documento.IdadeMaximaEmissao is { ReferenciaFaseId: { } refFaseId } idade
        && refFaseId == faseId
        && extremos.Contains(idade.ReferenciaTipo);

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
}
