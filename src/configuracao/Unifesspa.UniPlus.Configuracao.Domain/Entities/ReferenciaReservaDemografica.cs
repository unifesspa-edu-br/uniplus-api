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
    /// Cria uma nova Referência de reserva demográfica, acumulando toda violação
    /// independente em vez de parar na primeira. Valida o Censo, os três
    /// percentuais (intervalo fechado 0–100) e a base legal. A unicidade de
    /// <paramref name="censoReferencia"/> entre referências vivas é
    /// responsabilidade do handler.
    /// </summary>
    public static Result<ReferenciaReservaDemografica> Criar(
        string? censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string? baseLegal)
    {
        Result<(Percentual Ppi, Percentual Quilombola, Percentual Pcd)> validacao =
            ValidarCampos(censoReferencia, ppiPercentual, quilombolaPercentual, pcdPercentual, baseLegal);
        if (validacao.IsFailure)
        {
            return Result<ReferenciaReservaDemografica>.ValidationFailure(validacao.Errors);
        }

        var referencia = new ReferenciaReservaDemografica();
        referencia.AplicarCampos(censoReferencia!, validacao.Value, baseLegal!);

        return Result<ReferenciaReservaDemografica>.Success(referencia);
    }

    /// <summary>
    /// Atualiza os percentuais e a base legal da referência, acumulando toda
    /// violação independente. Nunca altera o <c>Id</c>. A unicidade de
    /// <paramref name="censoReferencia"/> (quando alterada) é responsabilidade
    /// do handler.
    /// </summary>
    public Result Atualizar(
        string? censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string? baseLegal)
    {
        Result<(Percentual Ppi, Percentual Quilombola, Percentual Pcd)> validacao =
            ValidarCampos(censoReferencia, ppiPercentual, quilombolaPercentual, pcdPercentual, baseLegal);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        AplicarCampos(censoReferencia!, validacao.Value, baseLegal!);

        return Result.Success();
    }

    /// <summary>
    /// Valida Censo, os três percentuais e a base legal, sem I/O e sem mutar
    /// nada — para os handlers de criação e atualização falharem rápido antes
    /// de qualquer busca no banco (validação sempre vence I/O).
    /// </summary>
    public static Result ValidarCamposDoPayload(
        string? censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string? baseLegal)
    {
        Result<(Percentual Ppi, Percentual Quilombola, Percentual Pcd)> resultado =
            ValidarCampos(censoReferencia, ppiPercentual, quilombolaPercentual, pcdPercentual, baseLegal);

        return resultado.IsFailure ? Result.ValidationFailure(resultado.Errors) : Result.Success();
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
        string? censoReferencia,
        decimal ppiPercentual,
        decimal quilombolaPercentual,
        decimal pcdPercentual,
        string? baseLegal)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(censoReferencia))
        {
            erros.Add(new("censoReferencia", new DomainError(
                ReferenciaReservaDemograficaErrorCodes.CensoObrigatorio, "Censo de referência é obrigatório.")));
        }
        else if (censoReferencia.Trim().Length is < CensoReferenciaMinLength or > CensoReferenciaMaxLength)
        {
            erros.Add(new("censoReferencia", new DomainError(
                ReferenciaReservaDemograficaErrorCodes.CensoTamanho,
                $"Censo de referência deve ter entre {CensoReferenciaMinLength} e {CensoReferenciaMaxLength} caracteres.")));
        }

        Result<Percentual> ppi = Percentual.Criar(ppiPercentual);
        if (ppi.IsFailure)
        {
            erros.Add(new("ppiPercentual", ComCodigoForaDeFaixa(ppi.Error!)));
        }

        Result<Percentual> quilombola = Percentual.Criar(quilombolaPercentual);
        if (quilombola.IsFailure)
        {
            erros.Add(new("quilombolaPercentual", ComCodigoForaDeFaixa(quilombola.Error!)));
        }

        Result<Percentual> pcd = Percentual.Criar(pcdPercentual);
        if (pcd.IsFailure)
        {
            erros.Add(new("pcdPercentual", ComCodigoForaDeFaixa(pcd.Error!)));
        }

        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            erros.Add(new("baseLegal", new DomainError(
                ReferenciaReservaDemograficaErrorCodes.BaseLegalObrigatoria, "Base legal é obrigatória.")));
        }
        else if (baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            erros.Add(new("baseLegal", new DomainError(
                ReferenciaReservaDemograficaErrorCodes.BaseLegalTamanho,
                $"Base legal deve ter no máximo {BaseLegalMaxLength} caracteres.")));
        }

        if (erros.Count > 0)
        {
            return Result<(Percentual, Percentual, Percentual)>.ValidationFailure(erros);
        }

        return Result<(Percentual, Percentual, Percentual)>.Success((ppi.Value!, quilombola.Value!, pcd.Value!));
    }

    // Reetiqueta o erro genérico do value object com o código de domínio do cadastro.
    private static DomainError ComCodigoForaDeFaixa(DomainError erro) =>
        new(ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa, erro.Message);
}
