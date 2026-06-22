namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Referência de reserva demográfica (UNI-REQ-0065, módulo Configuração) —
/// registra, por Censo, os percentuais demográficos do estado que dimensionam
/// as sub-reservas internas da reserva de vagas da Lei 12.711/2012 (red. Lei
/// 14.723/2023, art. 10, III): pretos/pardos/indígenas (alínea "a"),
/// quilombolas (alínea "b") e pessoas com deficiência (alínea "c", p.u.).
/// </summary>
/// <remarks>
/// <para>Cadastro <b>flat</b>: sem FK intra-banco nem auto-referência. O
/// <c>CensoReferencia</c> é a chave de negócio, única entre referências vivas
/// (não soft-deleted) — a unicidade é validada pelo handler e reforçada por
/// índice único parcial de banco (<c>WHERE is_deleted = false</c>).</para>
/// <para>São agregados públicos do IBGE — nenhum dado pessoal (LGPD inaplicável).
/// O congelamento por valor (snapshot RN08) no bloco de distribuição é
/// responsabilidade do Processo Seletivo (módulo Selecao, ADR-0061); não há
/// colunas de snapshot aqui, e a remoção lógica nunca é bloqueada por cópias
/// congeladas em outro banco.</para>
/// </remarks>
public sealed class ReferenciaReservaDemografica : SoftDeletableEntity, IAuditableEntity
{
    private const int CensoReferenciaMinLength = 1;
    private const int CensoReferenciaMaxLength = 20;
    private const int BaseLegalMaxLength = 500;

    public string CensoReferencia { get; private set; } = string.Empty;
    public Percentual PpiPercentual { get; private set; } = null!;
    public Percentual QuilombolaPercentual { get; private set; } = null!;
    public Percentual PcdPercentual { get; private set; } = null!;
    public string BaseLegal { get; private set; } = string.Empty;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private ReferenciaReservaDemografica()
    {
    }

    /// <summary>
    /// Cria uma nova Referência de reserva demográfica. Valida o Censo, os três
    /// percentuais (intervalo fechado 0–100) e a base legal. A unicidade de
    /// <paramref name="censoReferencia"/> entre referências vivas é
    /// responsabilidade do handler.
    /// </summary>
    public static Result<ReferenciaReservaDemografica> Criar(
        string censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string baseLegal)
    {
        ArgumentNullException.ThrowIfNull(censoReferencia);
        ArgumentNullException.ThrowIfNull(baseLegal);

        Result<(Percentual Ppi, Percentual Quilombola, Percentual Pcd)> validacao =
            ValidarCampos(censoReferencia, ppiPercentual, quilombolaPercentual, pcdPercentual, baseLegal);
        if (validacao.IsFailure)
        {
            return Result<ReferenciaReservaDemografica>.Failure(validacao.Error!);
        }

        var referencia = new ReferenciaReservaDemografica();
        referencia.AplicarCampos(censoReferencia, validacao.Value, baseLegal);

        return Result<ReferenciaReservaDemografica>.Success(referencia);
    }

    /// <summary>
    /// Atualiza os percentuais e a base legal da referência. Nunca altera o
    /// <c>Id</c>. Revalida o intervalo de cada percentual e a presença da base
    /// legal. A unicidade de <paramref name="censoReferencia"/> (quando
    /// alterada) é responsabilidade do handler.
    /// </summary>
    public Result Atualizar(
        string censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string baseLegal)
    {
        ArgumentNullException.ThrowIfNull(censoReferencia);
        ArgumentNullException.ThrowIfNull(baseLegal);

        Result<(Percentual Ppi, Percentual Quilombola, Percentual Pcd)> validacao =
            ValidarCampos(censoReferencia, ppiPercentual, quilombolaPercentual, pcdPercentual, baseLegal);
        if (validacao.IsFailure)
        {
            return Result.Failure(validacao.Error!);
        }

        AplicarCampos(censoReferencia, validacao.Value, baseLegal);

        return Result.Success();
    }

    private void AplicarCampos(
        string censoReferencia,
        (Percentual Ppi, Percentual Quilombola, Percentual Pcd) percentuais,
        string baseLegal)
    {
        CensoReferencia = censoReferencia.Trim();
        PpiPercentual = percentuais.Ppi;
        QuilombolaPercentual = percentuais.Quilombola;
        PcdPercentual = percentuais.Pcd;
        BaseLegal = baseLegal.Trim();
    }

    private static Result<(Percentual Ppi, Percentual Quilombola, Percentual Pcd)> ValidarCampos(
        string censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string baseLegal)
    {
        if (string.IsNullOrWhiteSpace(censoReferencia))
        {
            return Falha(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.CensoObrigatorio,
                "Censo de referência é obrigatório."));
        }

        if (censoReferencia.Trim().Length is < CensoReferenciaMinLength or > CensoReferenciaMaxLength)
        {
            return Falha(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.CensoTamanho,
                $"Censo de referência deve ter entre {CensoReferenciaMinLength} e {CensoReferenciaMaxLength} caracteres."));
        }

        Result<Percentual> ppi = Percentual.Criar(ppiPercentual);
        if (ppi.IsFailure)
        {
            return Falha(ComCodigoForaDeFaixa(ppi.Error!));
        }

        Result<Percentual> quilombola = Percentual.Criar(quilombolaPercentual);
        if (quilombola.IsFailure)
        {
            return Falha(ComCodigoForaDeFaixa(quilombola.Error!));
        }

        Result<Percentual> pcd = Percentual.Criar(pcdPercentual);
        if (pcd.IsFailure)
        {
            return Falha(ComCodigoForaDeFaixa(pcd.Error!));
        }

        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            return Falha(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.BaseLegalObrigatoria,
                "Base legal é obrigatória."));
        }

        if (baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            return Falha(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.BaseLegalTamanho,
                $"Base legal deve ter no máximo {BaseLegalMaxLength} caracteres."));
        }

        return Result<(Percentual, Percentual, Percentual)>.Success((ppi.Value!, quilombola.Value!, pcd.Value!));
    }

    // Reetiqueta o erro genérico do value object com o código de domínio do cadastro.
    private static DomainError ComCodigoForaDeFaixa(DomainError erro) =>
        new(ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa, erro.Message);

    private static Result<(Percentual Ppi, Percentual Quilombola, Percentual Pcd)> Falha(DomainError erro) =>
        Result<(Percentual, Percentual, Percentual)>.Failure(erro);
}
