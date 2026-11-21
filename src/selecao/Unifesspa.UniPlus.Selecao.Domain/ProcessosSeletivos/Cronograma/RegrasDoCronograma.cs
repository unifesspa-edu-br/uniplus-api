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
        ConfiguracaoDeclarada configuracao,
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        IReadOnlyList<FieldError> causas = CausasDaSubstituicao(configuracao, fases, precedencias);

        return causas.Count == 0
            ? Result<PlanoDoCronograma>.Success(MontarPlano(configuracao, fases))
            : Result<PlanoDoCronograma>.ValidationFailure(causas);
    }

    // O período da etapa é conferido contra o conjunto candidato, não contra o agregado corrente: a etapa
    // cuja fase está saindo sai junto na poda e não entra na conta. A mudança casa as fases por origem, e o casamento lança com
    // origem repetida, que a estrutura já recusou: só ela deixa de ser avaliada. A precedência casa por código e não lê a origem
    // (a aresta de código ambíguo já fica suspensa na estrutura), e a ordem repetida é recusa do conjunto: as duas seguem avaliadas.
    private static IReadOnlyList<FieldError> CausasDaSubstituicao(
        ConfiguracaoDeclarada configuracao,
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias)
    {
        CronogramaDoProcesso candidato = CronogramaDoProcesso.Do(fases);

        return
        [
            .. candidato.ConferirEstrutura(),
            .. candidato.OrigensUnicas ? CausasDaRecusa.Da(RecusaDaMudanca(configuracao, fases)) : [],
            .. candidato.ConferirPrecedencia(precedencias),
            .. EtapasNoCronograma.ConferirJanelaDasEtapas(candidato, EtapasDoProcesso.Das(configuracao.Avaliacao.Etapas)),
        ];
    }

    private static Dictionary<Guid, FaseCronograma> PorOrigem(IEnumerable<FaseCronograma> fases) =>
        fases.ToDictionary(static f => f.FaseCanonicaOrigemId);

    // As recusas do que muda em relação ao cronograma que o processo já tem, na ordem em que orientam; a precedência do cadastro vem depois.
    private static DomainError? RecusaDaMudanca(ConfiguracaoDeclarada configuracao, IReadOnlyList<FaseCronograma> fases)
    {
        Dictionary<Guid, FaseCronograma> antigasPorOrigem = PorOrigem(configuracao.Cronograma.CronogramaFases);
        Dictionary<Guid, FaseCronograma> novasPorOrigem = PorOrigem(fases);
        List<DocumentoExigido> documentosQueFicam = DocumentosQueFicam(configuracao, antigasPorOrigem, novasPorOrigem);

        return RecusaDasFasesQueSaemOuMudam(configuracao, antigasPorOrigem, novasPorOrigem, documentosQueFicam)
            ?? RecusaDaAncoraNoFimDaInscricao(fases, documentosQueFicam)
            ?? PermutacaoCiclicaDeOrdem.Recusa(antigasPorOrigem, novasPorOrigem);
    }

    // O documento exigido numa fase que também sai vai junto com ela, na poda; contá-lo como
    // referência viva recusaria uma remoção que o próprio comando resolve. Só quem fica reclama.
    private static List<DocumentoExigido> DocumentosQueFicam(
        ConfiguracaoDeclarada configuracao,
        Dictionary<Guid, FaseCronograma> antigasPorOrigem,
        Dictionary<Guid, FaseCronograma> novasPorOrigem)
    {
        HashSet<Guid> fasesQueFicam = [.. antigasPorOrigem.Values
            .Where(f => novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))
            .Select(f => f.Id)];

        return [.. configuracao.Exigencias.DocumentosExigidos.Where(d => fasesQueFicam.Contains(d.ExigidoNaFaseId))];
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

    private static PlanoDoCronograma MontarPlano(ConfiguracaoDeclarada configuracao, IReadOnlyList<FaseCronograma> fases)
    {
        Dictionary<Guid, FaseCronograma> antigasPorOrigem = PorOrigem(configuracao.Cronograma.CronogramaFases);
        Dictionary<Guid, FaseCronograma> novasPorOrigem = PorOrigem(fases);
        List<(FaseCronograma Existente, FaseCronograma Nova)> retidas =
            [.. fases.Where(f => antigasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId)).Select(f => (antigasPorOrigem[f.FaseCanonicaOrigemId], f))];
        List<FaseCronograma> resultantes = [.. fases.Select(f => antigasPorOrigem.GetValueOrDefault(f.FaseCanonicaOrigemId) ?? f)];
        List<FaseCronograma> removidas = [.. antigasPorOrigem.Values.Where(f => !novasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId))];
        MudancaDaConfiguracao poda = PodaDoCronograma.Planejar(configuracao, removidas);
        return new PlanoDoCronograma(retidas, poda with { CronogramaFases = Troca.Substituindo(configuracao.Cronograma.CronogramaFases, resultantes) });
    }

    private static DomainError? RecusaDasFasesQueSaemOuMudam(
        ConfiguracaoDeclarada configuracao,
        Dictionary<Guid, FaseCronograma> antigasPorOrigem,
        Dictionary<Guid, FaseCronograma> novasPorOrigem,
        List<DocumentoExigido> documentosQueFicam)
    {
        foreach (FaseCronograma antiga in antigasPorOrigem.Values)
        {
            DomainError? recusa = novasPorOrigem.TryGetValue(antiga.FaseCanonicaOrigemId, out FaseCronograma? nova)
                ? RecusaDaFaseRetida(configuracao, antiga, nova, documentosQueFicam)
                : RecusaDaFaseRemovida(configuracao, antiga, documentosQueFicam);
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
    private static DomainError? RecusaDaFaseRemovida(ConfiguracaoDeclarada configuracao, FaseCronograma antiga, List<DocumentoExigido> documentosQueFicam) =>
        JanelaDaFaseUsadaPorOutraExigencia(antiga, documentosQueFicam)
        ?? EtapaDaFaseReferenciadaPelaClassificacao(configuracao, antiga)
        ?? FormularioRespondidoNaFase(configuracao, antiga);

    private static DomainError? JanelaDaFaseUsadaPorOutraExigencia(FaseCronograma antiga, List<DocumentoExigido> documentosQueFicam) =>
        documentosQueFicam.Find(d => d.IdadeMaximaEmissao?.ReferenciaFaseId == antiga.Id) is { } ancoraDeOutraFase
            ? new DomainError(
                "FaseCronograma.ReferenciadaPorExigenciaViva",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida do cronograma, mas o documento '{ancoraDeOutraFase.TipoDocumentoNome}', exigido em outra fase, usa o período dela para iniciar a contagem da idade máxima de emissão.")
            : null;

    private static DomainError? EtapaDaFaseReferenciadaPelaClassificacao(ConfiguracaoDeclarada configuracao, FaseCronograma antiga) =>
        configuracao.Avaliacao.Etapas.FirstOrDefault(e =>
            string.Equals(e.FaseCodigo, antiga.Codigo, StringComparison.Ordinal)
            && RegrasDasEtapas.EtapaReferenciadaPorClassificacaoOuDesempate(configuracao, e.Id)) is { } etapaReferenciada
            ? new DomainError(
                "FaseCronograma.EtapaDaFaseReferenciada",
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida, mas a etapa '{etapaReferenciada.Nome}' que acontece nela é referenciada por um critério de desempate ou por uma regra de eliminação — reconfigure a classificação antes.")
            : null;

    private static DomainError? FormularioRespondidoNaFase(ConfiguracaoDeclarada configuracao, FaseCronograma antiga) =>
        configuracao.Coleta.Formularios.FirstOrDefault(f => f.FaseId == antiga.Id) is { } formularioDaFase
            ? new DomainError(
                FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario,
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida, mas o formulário de {EstruturaFormulario.ParaToken(formularioDaFase.Finalidade)} é respondido nela — troque a fase do formulário antes.")
            : null;

    // Um extremo âncora que deixa de existir numa fase que continua: a conferência da exigência só
    // prova a coerência no instante da escrita dela, e sem esta recusa uma redefinição posterior do
    // cronograma deixaria a idade máxima apontando para uma âncora irresolvível.
    private static DomainError? RecusaDaFaseRetida(
        ConfiguracaoDeclarada configuracao, FaseCronograma antiga, FaseCronograma nova, List<DocumentoExigido> documentosQueFicam) =>
        ComplementacaoQueAPendenciaDeReenvioExige(configuracao, antiga, nova) ?? ExtremoQueAAncoraDeIdadeUsa(antiga, nova, documentosQueFicam);

    private static DomainError? ComplementacaoQueAPendenciaDeReenvioExige(ConfiguracaoDeclarada configuracao, FaseCronograma antiga, FaseCronograma nova) =>
        antiga.PermiteComplementacao && !nova.PermiteComplementacao
        && RegrasDasExigencias.ExisteConsequenciaPendenciaReenvioNaFase(configuracao, antiga.Id)
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
                $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder a data usada para iniciar a contagem da idade máxima de emissão por um documento exigido configurado.")
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
                "A redefinição do cronograma deixaria de ter uma fase que coleta inscrição com Fim definido, mas há documento exigido configurado com idade máxima de emissão contada a partir de FIM_INSCRICAO.")
            : null;
}
