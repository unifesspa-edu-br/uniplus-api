namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Unifesspa.UniPlus.Regras.Formularios;

using ValueObjects;

/// <summary>
/// Compõe a seção de documentos do comprovante (UNI-REQ-0029) a partir da avaliação da inscrição e dos
/// arquivos confirmados: cada exigência da inscrição que se aplica, ou pode se aplicar, ao candidato,
/// com a consequência escrita por extenso. A alternativa de um grupo "um ou outro" que o candidato não
/// precisou enviar porque o grupo já está atendido sai como dispensada, e sem consequência: listá-la como
/// pendente, com a consequência de eliminação, contradiria a inscrição aceita.
/// </summary>
public static class DocumentosDoComprovante
{
    /// <summary>A situação da alternativa de um grupo "um ou outro" que o grupo atendido dispensa.</summary>
    public const string SituacaoDispensada = "DISPENSADA";

    public static IReadOnlyList<DocumentoDoComprovante> Compor(
        DocumentosDaInscricao documentos, IReadOnlyList<ArquivoConfirmado> arquivos)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(arquivos);

        DocumentoAvaliado[] daInscricao = [.. documentos.Itens
            .Where(static d => d.Situacao != SituacaoDoDocumento.NaoExigido && d.Documento.Finalidade == FinalidadeFormulario.Inscricao)];

        return [.. daInscricao.Select(d =>
        {
            bool dispensada = d.DispensadoPeloGrupo;
            DocumentoExigido exigencia = d.Documento;
            return new DocumentoDoComprovante(
                exigencia.Id,
                exigencia.TipoDocumentoCodigo,
                exigencia.TipoDocumentoNome,
                exigencia.Obrigatorio,
                dispensada ? SituacaoDispensada : d.Situacao.ToCodigo(),
                d.OcorrenciaId,
                dispensada ? null : ConsequenciaDe(d, daInscricao),
                [.. arquivos
                    .Where(a => a.ExigenciaId == exigencia.Id && string.Equals(a.OcorrenciaId, d.OcorrenciaId, StringComparison.Ordinal))
                    .OrderBy(static a => a.ConfirmadoEm)
                    .ThenBy(static a => a.ArquivoId)
                    .Select(static a => new ArquivoDoComprovante(a.HashSha256, a.TamanhoBytes, a.Formato.ToCodigo()))]);
        })];
    }

    /// <summary>A consequência de não atender a exigência, em linguagem do candidato; nula sem consequência declarada.</summary>
    /// <param name="referencia">O que falta, no meio da frase, como "o documento RG".</param>
    public static string? ConsequenciaPorEscrito(string? consequencia, string referencia) => consequencia switch
    {
        ConsequenciaIndeferimentoCodigo.Elimina => $"Sem {referencia}, você será eliminado do processo seletivo.",
        ConsequenciaIndeferimentoCodigo.ReclassificaAc => $"Sem {referencia}, você perderá a vaga reservada e passará a concorrer na ampla concorrência.",
        ConsequenciaIndeferimentoCodigo.RemoveVantagem => $"Sem {referencia}, a vantagem que você pleiteia não será concedida.",
        ConsequenciaIndeferimentoCodigo.PendenciaReenvio => $"Sem {referencia}, você será notificado para reenviá-lo no prazo da fase.",
        _ => null,
    };

    /// <summary>
    /// O grupo "um ou outro" é a unidade da consequência (UNI-REQ-0070): dentro dele, vale a do grupo mais
    /// externo, escrita sobre os documentos do grupo, e as alternativas só orientam o que falta — a
    /// consequência própria delas não vale, e o grupo sem consequência não tem texto. Fora de grupo "um ou
    /// outro", vale a da própria exigência; o grupo "E" não carrega consequência.
    /// </summary>
    private static string? ConsequenciaDe(DocumentoAvaliado avaliado, IReadOnlyList<DocumentoAvaliado> todos)
    {
        GrupoDaArvore? ou = avaliado.Grupos.FirstOrDefault(static g => g.Tipo == TipoNo.GrupoOu);
        if (ou is null)
        {
            return ConsequenciaPorEscrito(avaliado.Documento.ConsequenciaIndeferimento, $"o documento {avaliado.Documento.TipoDocumentoNome}");
        }

        string nomes = string.Join(", ", NoGrupo(ou, todos).Select(static d => d.Documento.TipoDocumentoNome).Distinct(StringComparer.Ordinal));
        return ConsequenciaPorEscrito(ou.Consequencia, $"os documentos exigidos ({nomes})");
    }

    private static IEnumerable<DocumentoAvaliado> NoGrupo(GrupoDaArvore grupo, IReadOnlyList<DocumentoAvaliado> todos) =>
        todos.Where(d => d.Grupos.Any(g => g.GrupoId == grupo.GrupoId));
}
