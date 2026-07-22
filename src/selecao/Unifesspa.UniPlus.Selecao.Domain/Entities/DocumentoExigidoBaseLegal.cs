namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Base legal de um <see cref="DocumentoExigido"/> (Story #554, PR #898, issue #549,
/// ADR-0074) — relação 1:N: uma exigência pode ter mais de uma fonte de embasamento ao
/// mesmo tempo (ex.: lei federal + cláusula do próprio edital). <c>EntityBase</c> puro
/// (sem soft-delete) — filha de <see cref="DocumentoExigido"/>, substituível por inteiro
/// junto com o mesmo <c>PUT {id}/documentos-exigidos</c> da PR #895; não há
/// <c>Resolver()</c>/<c>Rebaixar()</c> próprios — "rebaixar" ou "remover" uma base é
/// reenviar o payload da exigência sem aquele item, ou com <see cref="Status"/> alterado.
/// </summary>
public sealed class DocumentoExigidoBaseLegal : EntityBase
{
    public Guid DocumentoExigidoId { get; private set; }

    /// <summary>Referência textual institucional (ex.: "Lei 12.711/2012, art. 3º") — não é PII.</summary>
    public string Referencia { get; private set; } = string.Empty;

    public TipoAbrangencia Abrangencia { get; private set; }

    public StatusBaseLegal Status { get; private set; }

    public string? Observacao { get; private set; }

    private DocumentoExigidoBaseLegal() { }

    public static Result<DocumentoExigidoBaseLegal> Criar(
        string referencia, TipoAbrangencia abrangencia, StatusBaseLegal status, string? observacao)
    {
        if (string.IsNullOrWhiteSpace(referencia))
        {
            return Result<DocumentoExigidoBaseLegal>.Failure(new DomainError(
                "DocumentoExigidoBaseLegal.ReferenciaObrigatoria",
                "A referência da base legal é obrigatória."));
        }

        if (abrangencia == TipoAbrangencia.Nenhuma)
        {
            return Result<DocumentoExigidoBaseLegal>.Failure(new DomainError(
                "DocumentoExigidoBaseLegal.AbrangenciaObrigatoria",
                "A abrangência da base legal é obrigatória."));
        }

        if (status == StatusBaseLegal.Nenhuma)
        {
            return Result<DocumentoExigidoBaseLegal>.Failure(new DomainError(
                "DocumentoExigidoBaseLegal.StatusObrigatorio",
                "O status da base legal é obrigatório."));
        }

        return Result<DocumentoExigidoBaseLegal>.Success(new DocumentoExigidoBaseLegal
        {
            Referencia = referencia.Trim(),
            Abrangencia = abrangencia,
            Status = status,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
        });
    }

    internal void VincularDocumentoExigido(Guid documentoExigidoId) =>
        DocumentoExigidoId = documentoExigidoId;
}
