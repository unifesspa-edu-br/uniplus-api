namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

internal static class DocumentosAvaliadosDaInscricao
{
    /// <summary>
    /// Por fase, o que falta responder para saber se as exigências dela que podem ser exigidas se
    /// aplicam: os campos e os grupos visíveis da inscrição de que os gatilhos delas dependem e que
    /// continuam indeterminados. Cada fase tem as suas, para quem consulta uma fase só não receber o que
    /// não muda nada nela.
    /// </summary>
    internal static Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>> PendenciasDosDocumentos(
        ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario, IReadOnlyList<DocumentoAvaliado> documentos) =>
        documentos
            .Where(static d => d.Situacao == SituacaoDoDocumento.PodeSerExigido)
            .GroupBy(static d => d.Documento.ExigidoNaFaseId)
            .Select(fase =>
            {
                HashSet<Guid> daFase = [.. fase.Select(static d => d.Documento.Id)];
                DependenciasDaInscricao.CitacoesDosGatilhos citacoes = new ColetorDeCitacoes(configuracao.Definicao, daFase.Contains).Coletar(configuracao.Arvore.Raizes);
                return (Fase: fase.Key, Pendencias: new PendenciasDaInscricao(configuracao, formulario, DependenciasDaInscricao.DependenciasDe(configuracao.Definicao, citacoes)).Apurar());
            })
            .Where(static f => f.Pendencias.Count > 0)
            .ToDictionary(static f => f.Fase, static f => (IReadOnlyList<PendenciaDaAvaliacao>)f.Pendencias);

    /// <summary>
    /// A situação da folha pelo status que o resolvedor deu a ela: a que depende de fato de fase
    /// posterior chega bloqueada, e por isso indeterminada, e não se confunde com a que espera uma
    /// resposta do candidato.
    /// </summary>
    internal static SituacaoDoDocumento Situacao(StatusResolucaoExigencia status, bool deFasePosterior) => status switch
    {
        StatusResolucaoExigencia.NaoAplicavel => SituacaoDoDocumento.NaoExigido,
        _ when deFasePosterior => SituacaoDoDocumento.DependeDeFasePosterior,
        StatusResolucaoExigencia.Pendente => SituacaoDoDocumento.Pendente,
        StatusResolucaoExigencia.Satisfeita => SituacaoDoDocumento.Satisfeita,
        _ => SituacaoDoDocumento.PodeSerExigido,
    };
}
