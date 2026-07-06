namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot-copy por valor (ADR-0061) da <c>ReferenciaReservaDemografica</c>
/// viva do módulo Configuração (<c>IReferenciaReservaDemograficaReader</c>,
/// ADR-0056) no momento em que a distribuição de vagas pela Lei 12.711 é
/// configurada — os percentuais do Censo ficam congelados independentemente
/// de atualizações futuras do cadastro de origem.
/// </summary>
public sealed record ReferenciaReservaDemograficaSnapshot
{
    private ReferenciaReservaDemograficaSnapshot(
        Guid origemId,
        string censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string baseLegal)
    {
        OrigemId = origemId;
        CensoReferencia = censoReferencia;
        PpiPercentual = ppiPercentual;
        QuilombolaPercentual = quilombolaPercentual;
        PcdPercentual = pcdPercentual;
        BaseLegal = baseLegal;
    }

    public Guid OrigemId { get; }
    public string CensoReferencia { get; }
    public decimal PpiPercentual { get; }
    public decimal QuilombolaPercentual { get; }
    public decimal PcdPercentual { get; }
    public string BaseLegal { get; }

    public static Result<ReferenciaReservaDemograficaSnapshot> Criar(
        Guid origemId,
        string censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string baseLegal)
    {
        if (string.IsNullOrWhiteSpace(censoReferencia))
        {
            return Result<ReferenciaReservaDemograficaSnapshot>.Failure(new DomainError(
                "ReferenciaReservaDemograficaSnapshot.CensoObrigatorio", "Censo de referência é obrigatório."));
        }

        if (ppiPercentual is < 0 or > 100 || quilombolaPercentual is < 0 or > 100 || pcdPercentual is < 0 or > 100)
        {
            return Result<ReferenciaReservaDemograficaSnapshot>.Failure(new DomainError(
                "ReferenciaReservaDemograficaSnapshot.PercentualInvalido",
                "Os percentuais demográficos devem estar entre 0 e 100 (INV-5)."));
        }

        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            return Result<ReferenciaReservaDemograficaSnapshot>.Failure(new DomainError(
                "ReferenciaReservaDemograficaSnapshot.BaseLegalObrigatoria", "Base legal é obrigatória."));
        }

        return Result<ReferenciaReservaDemograficaSnapshot>.Success(new ReferenciaReservaDemograficaSnapshot(
            origemId, censoReferencia.Trim(), ppiPercentual, quilombolaPercentual, pcdPercentual, baseLegal.Trim()));
    }
}
