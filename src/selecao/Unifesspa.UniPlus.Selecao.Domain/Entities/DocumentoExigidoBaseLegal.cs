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

    /// <summary>
    /// Acumula (ADR-0125) as três checagens — todas primitivas do payload cru, sem
    /// qualquer resolução de cadastro. Exposta para o handler poder confirmar a forma de
    /// TODAS as bases legais do payload numa primeira passada, antes de qualquer I/O (mesmo
    /// padrão de <c>EtapaProcesso.ValidarFormaBasica</c>, PR #1218).
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? referencia, TipoAbrangencia abrangencia, StatusBaseLegal status)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(referencia))
        {
            erros.Add(new("referencia", new DomainError(
                "DocumentoExigidoBaseLegal.ReferenciaObrigatoria",
                "A referência da base legal é obrigatória.")));
        }

        if (abrangencia == TipoAbrangencia.Nenhuma)
        {
            erros.Add(new("abrangencia", new DomainError(
                "DocumentoExigidoBaseLegal.AbrangenciaObrigatoria",
                "A abrangência da base legal é obrigatória.")));
        }

        if (status == StatusBaseLegal.Nenhuma)
        {
            erros.Add(new("status", new DomainError(
                "DocumentoExigidoBaseLegal.StatusObrigatorio",
                "O status da base legal é obrigatório.")));
        }

        return erros;
    }

    public static Result<DocumentoExigidoBaseLegal> Criar(
        string referencia, TipoAbrangencia abrangencia, StatusBaseLegal status, string? observacao)
    {
        List<FieldError> erros = ValidarFormaBasica(referencia, abrangencia, status);
        if (erros.Count > 0)
        {
            return Result<DocumentoExigidoBaseLegal>.ValidationFailure(erros);
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
