namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

using ValueObjects;

/// <summary>
/// Compõe a seção de documentos do comprovante (UNI-REQ-0029) a partir da avaliação da inscrição e dos
/// arquivos confirmados: cada exigência da inscrição que se aplica, ou pode se aplicar, ao candidato,
/// com a consequência escrita por extenso. O item de um grupo de quantidade mínima que o candidato não
/// precisou enviar porque o grupo já está atendido sai como dispensada, e sem consequência: listá-la como
/// pendente, com a consequência de eliminação, contradiria a inscrição aceita.
/// </summary>
public static class DocumentosDoComprovante
{
    /// <summary>A situação do item que um grupo de quantidade mínima atendido dispensa.</summary>
    public const string SituacaoDispensada = "DISPENSADA";

    public static IReadOnlyList<DocumentoDoComprovante> Compor(
        DocumentosDaInscricao documentos, IReadOnlyList<ArquivoConfirmado> arquivos)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(arquivos);

        DocumentoAvaliado[] daInscricao = [.. documentos.DaInscricao.Where(static d => d.Situacao != SituacaoDoDocumento.NaoExigido)];

        return [.. daInscricao.Select(d => ComporItem(d, daInscricao, arquivos))];
    }

    private static DocumentoDoComprovante ComporItem(
        DocumentoAvaliado avaliado, DocumentoAvaliado[] todos, IReadOnlyList<ArquivoConfirmado> arquivos)
    {
        bool dispensada = avaliado.DispensadoPeloGrupo;
        DocumentoExigido exigencia = avaliado.Documento;
        return new DocumentoDoComprovante(
            exigencia.Id, exigencia.TipoDocumentoCodigo, exigencia.TipoDocumentoNome, exigencia.Obrigatorio,
            dispensada ? SituacaoDispensada : avaliado.Situacao.Codigo(), avaliado.OcorrenciaId,
            dispensada ? null : ConsequenciaDe(avaliado, todos), ArquivosDoItem(avaliado, arquivos));
    }

    private static IReadOnlyList<ArquivoDoComprovante> ArquivosDoItem(DocumentoAvaliado avaliado, IReadOnlyList<ArquivoConfirmado> arquivos) =>
        [.. arquivos.Where(a => a.ExigenciaId == avaliado.Documento.Id && string.Equals(a.OcorrenciaId, avaliado.OcorrenciaId, StringComparison.Ordinal))
            .OrderBy(static a => a.ConfirmadoEm).ThenBy(static a => a.ArquivoId)
            .Select(static a => new ArquivoDoComprovante(a.HashSha256, a.TamanhoBytes, a.Formato.Codigo()))];

    /// <summary>A consequência de não atender a exigência, em linguagem do candidato; nula sem consequência declarada.</summary>
    /// <param name="referencia">O que falta, no meio da frase, como "o documento RG".</param>
    public static string? ConsequenciaPorEscrito(string? consequenciaDoDescumprimento, string referencia) => (CatalogoDe<ConsequenciaDoDescumprimento>.TentarObter(consequenciaDoDescumprimento, out ConsequenciaDoDescumprimento valor) ? valor : (ConsequenciaDoDescumprimento?)null) switch
    {
        ConsequenciaDoDescumprimento.EliminaDoProcesso => $"Sem {referencia}, você será eliminado do processo seletivo.",
        ConsequenciaDoDescumprimento.RetiraAModalidade => $"Sem {referencia}, você perderá a modalidade e continuará nas demais em que concorre.",
        ConsequenciaDoDescumprimento.RetiraOAcrescimo => $"Sem {referencia}, o acréscimo do Fator de Atenuação das Desigualdades de Acesso não será concedido.",
        _ => null,
    };

    /// <summary>
    /// O grupo de quantidade mínima é a unidade da consequência (UNI-REQ-0070): dentro dele, vale a do grupo mais
    /// externo, escrita sobre os documentos do grupo, e as alternativas só orientam o que falta — a
    /// consequência própria delas não vale, e o grupo sem consequência não tem texto. Fora de um grupo de
    /// quantidade mínima, vale a da própria exigência; o grupo que exige todos os itens não carrega consequência.
    /// </summary>
    private static string? ConsequenciaDe(DocumentoAvaliado avaliado, IReadOnlyList<DocumentoAvaliado> todos)
    {
        GrupoDeComprovacao? grupo = avaliado.Grupos.FirstOrDefault(static g => g.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens);
        if (grupo is null)
        {
            return ConsequenciaPorEscrito(avaliado.Documento.ConsequenciaDoDescumprimento?.Codigo(), $"o documento {avaliado.Documento.TipoDocumentoNome}");
        }

        string nomes = string.Join(", ", NoGrupo(grupo, todos).Select(static d => d.Documento.TipoDocumentoNome).Distinct(StringComparer.Ordinal));
        return ConsequenciaPorEscrito(grupo.ConsequenciaDoDescumprimento, $"os documentos exigidos ({nomes})");
    }

    private static IEnumerable<DocumentoAvaliado> NoGrupo(GrupoDeComprovacao grupo, IReadOnlyList<DocumentoAvaliado> todos) =>
        todos.Where(d => d.Grupos.Any(g => g.GrupoId == grupo.GrupoId));
}
