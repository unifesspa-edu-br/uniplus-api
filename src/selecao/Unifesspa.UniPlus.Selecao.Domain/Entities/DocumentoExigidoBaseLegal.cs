namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Base legal de um <see cref="DocumentoExigido"/> (ADR-0074) — relação 1:N: uma exigência pode ter mais de uma fonte de embasamento ao
/// mesmo tempo (ex.: lei federal + cláusula do próprio edital). <c>EntityBase</c> puro
/// (sem soft-delete) — filha de <see cref="DocumentoExigido"/>, substituível por inteiro
/// junto com o mesmo <c>PUT {id}/documentos-exigidos</c>; não há
/// <c>Resolver()</c>/<c>Rebaixar()</c> próprios — "rebaixar" ou "remover" uma base é
/// reenviar o payload da exigência sem aquele item, ou com <see cref="Situacao"/> alterado.
/// </summary>
public sealed class DocumentoExigidoBaseLegal : EntityBase
{
    public Guid DocumentoExigidoId { get; private set; }

    /// <summary>Referência textual institucional (ex.: "Lei 12.711/2012, art. 3º") — não é PII.</summary>
    public string Referencia { get; private set; } = string.Empty;

    public TipoAbrangencia Abrangencia { get; private set; }

    public StatusBaseLegal Situacao { get; private set; }

    public string? Observacao { get; private set; }

    private DocumentoExigidoBaseLegal() { }

    /// <summary>Confere os dados informados da base legal e acumula as recusas.</summary>
    public static List<FieldError> ValidarFormaBasica(string? referencia, TipoAbrangencia? abrangencia, StatusBaseLegal? situacao) =>
        [.. new[] { RecusaDaReferencia(referencia), RecusaDaAbrangencia(abrangencia), RecusaDaSituacao(situacao) }.OfType<FieldError>()];

    private static FieldError? RecusaDaReferencia(string? referencia) => string.IsNullOrWhiteSpace(referencia)
        ? new("referencia", new DomainError("DocumentoExigidoBaseLegal.ReferenciaObrigatoria", "A referência da base legal é obrigatória."))
        : null;

    private static FieldError? RecusaDaAbrangencia(TipoAbrangencia? abrangencia) => abrangencia is null
        ? new("abrangencia", new DomainError("DocumentoExigidoBaseLegal.AbrangenciaObrigatoria", "A abrangência da base legal é obrigatória."))
        : null;

    private static FieldError? RecusaDaSituacao(StatusBaseLegal? situacao) => situacao is null
        ? new("situacao", new DomainError("DocumentoExigidoBaseLegal.SituacaoObrigatoria", "A situação da base legal é obrigatória."))
        : null;

    public static Result<DocumentoExigidoBaseLegal> Criar(
        string referencia, TipoAbrangencia? abrangencia, StatusBaseLegal? situacao, string? observacao)
    {
        List<FieldError> erros = ValidarFormaBasica(referencia, abrangencia, situacao);
        if (erros.Count > 0 || referencia is null || abrangencia is null || situacao is null)
        {
            return Result<DocumentoExigidoBaseLegal>.ValidationFailure(erros);
        }

        return Result<DocumentoExigidoBaseLegal>.Success(new DocumentoExigidoBaseLegal
        {
            Referencia = referencia.Trim(),
            Abrangencia = abrangencia.Value,
            Situacao = situacao.Value,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
        });
    }

    internal void VincularDocumentoExigido(Guid documentoExigidoId) =>
        DocumentoExigidoId = documentoExigidoId;
}
