namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using ValueObjects;

/// <summary>Apura os documentos obrigatórios da inscrição ainda não comprovados, inclusive por ocorrência (UNI-REQ-0028).</summary>
public static class DocumentosAusentesDaInscricao
{
    /// <param name="formulariosDaInscricao">Os códigos dos formulários da inscrição: só as exigências ligadas a eles são da inscrição.</param>
    public static IReadOnlyList<DocumentoAusente> Apurar(
        RegrasDeComprovacaoDaVersao regras, ResultadoDaComprovacao resultado, IReadOnlyCollection<CodigoDoFormulario> formulariosDaInscricao)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(formulariosDaInscricao);
        Dictionary<(Guid RegraId, string OcorrenciaId), SituacaoDaComprovacao> porOcorrencia = resultado.SituacoesDeRegraPorOcorrencia
            .ToDictionary(static e => (e.RegraDeComprovacaoId, e.OcorrenciaId), static e => e.Situacao);
        List<DocumentoAusente> ausentes = [];
        foreach (RegraDeComprovacao regraSemGrupo in regras.RegrasSemGrupo.OrderBy(static r => r.Ordem))
        {
            Percorrer(regraSemGrupo, null, resultado, porOcorrencia, ausentes);
        }
        return SomenteDosFormularios(ausentes, formulariosDaInscricao);
    }

    private static List<DocumentoAusente> SomenteDosFormularios(List<DocumentoAusente> ausentes, IReadOnlyCollection<CodigoDoFormulario> formularios) =>
        [.. ausentes
            .Select(ausente => ausente with { Alternativas = [.. ausente.Alternativas.Where(d => d.FormularioCodigo is { } formulario && formularios.Contains(formulario))] })
            .Where(static ausente => ausente.Alternativas.Count > 0)];

    private static void Percorrer(RegraDeComprovacao regra, string? ocorrenciaId, ResultadoDaComprovacao resultado,
        Dictionary<(Guid RegraId, string OcorrenciaId), SituacaoDaComprovacao> porOcorrencia, List<DocumentoAusente> ausentes)
    {
        if (regra.GrupoDoFormularioParaRepeticao is not null && ocorrenciaId is null)
        {
            foreach (string ocorrencia in porOcorrencia.Keys.Where(k => k.RegraId == regra.Id).Select(static k => k.OcorrenciaId).Order(StringComparer.Ordinal))
            {
                Percorrer(regra, ocorrencia, resultado, porOcorrencia, ausentes);
            }
            return;
        }
        PercorrerNoEscopo(regra, ocorrenciaId, resultado, porOcorrencia, ausentes);
    }

    private static void PercorrerNoEscopo(RegraDeComprovacao regra, string? ocorrenciaId, ResultadoDaComprovacao resultado,
        Dictionary<(Guid RegraId, string OcorrenciaId), SituacaoDaComprovacao> porOcorrencia, List<DocumentoAusente> ausentes)
    {
        if (regra.Forma == FormaDeComprovacao.TodosOsItens)
        {
            PercorrerItensObrigatorios(regra, ocorrenciaId, resultado, porOcorrencia, ausentes);
            return;
        }
        if (SituacaoDe(regra, ocorrenciaId, resultado, porOcorrencia) == SituacaoDaComprovacao.PendenteDeComprovacao)
        {
            RegistrarAusentes(regra, ocorrenciaId, resultado, porOcorrencia, ausentes);
        }
    }

    private static void PercorrerItensObrigatorios(RegraDeComprovacao regra, string? ocorrenciaId, ResultadoDaComprovacao resultado,
        Dictionary<(Guid RegraId, string OcorrenciaId), SituacaoDaComprovacao> porOcorrencia, List<DocumentoAusente> ausentes)
    {
        if (SituacaoDe(regra, ocorrenciaId, resultado, porOcorrencia) is not (SituacaoDaComprovacao.PendenteDeComprovacao or SituacaoDaComprovacao.SemPossibilidadeDeCumprimento))
        {
            return;
        }
        foreach (RegraDeComprovacao item in regra.Itens.OrderBy(static f => f.Ordem))
        {
            Percorrer(item, ocorrenciaId, resultado, porOcorrencia, ausentes);
        }
    }

    private static void RegistrarAusentes(RegraDeComprovacao regra, string? ocorrenciaId, ResultadoDaComprovacao resultado,
        Dictionary<(Guid RegraId, string OcorrenciaId), SituacaoDaComprovacao> porOcorrencia, List<DocumentoAusente> ausentes)
    {
        DocumentoExigido[] faltantes = [.. regra.EnumerarRegraEItens()
            .Where(static n => n.Forma == FormaDeComprovacao.Documento && (n.Distincao is null || n.Distincao.ConferivelPelaContagem))
            .Where(n => SituacaoDe(n, ocorrenciaId, resultado, porOcorrencia) == SituacaoDaComprovacao.PendenteDeComprovacao)
            .Select(static n => n.DocumentoExigido!)
            .Where(static d => d.Obrigatorio)];
        if (faltantes.Length > 0)
        {
            ausentes.Add(new DocumentoAusente(faltantes, ocorrenciaId));
        }
    }

    private static SituacaoDaComprovacao SituacaoDe(RegraDeComprovacao regra, string? ocorrenciaId, ResultadoDaComprovacao resultado,
        Dictionary<(Guid RegraId, string OcorrenciaId), SituacaoDaComprovacao> porOcorrencia) => ocorrenciaId is null
        ? resultado.SituacoesPorRegra.GetValueOrDefault(regra.Id, SituacaoDaComprovacao.AguardandoDados)
        : porOcorrencia.GetValueOrDefault((regra.Id, ocorrenciaId), SituacaoDaComprovacao.AguardandoDados);
}
