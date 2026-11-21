namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Base legal de um <see cref="NoExigencia"/> do tipo <see cref="TipoNo.GrupoOu"/> com
/// <see cref="NoExigencia.Consequencia"/> declarada (Story #920) — mesmo shape/validação de
/// <see cref="DocumentoExigidoBaseLegal"/>, mas o grupo opaco é exigência de 1ª classe: carrega
/// sua PRÓPRIA base legal 1:N, não derivada das folhas. Duas classes concretas separadas
/// (sem herança EF/TPH) — decisão deliberada, ver <see cref="NoExigencia"/>.
/// </summary>
public sealed class BaseLegalDaComprovacao : EntityBase
{
    public Guid NoExigenciaId { get; private set; }

    /// <summary>Referência textual institucional (ex.: "Lei 12.711/2012, art. 3º") — não é PII.</summary>
    public string Referencia { get; private set; } = string.Empty;

    public TipoAbrangencia Abrangencia { get; private set; }

    public StatusBaseLegal Status { get; private set; }

    public string? Observacao { get; private set; }

    private BaseLegalDaComprovacao() { }

    /// <summary>
    /// Acumula (ADR-0125) as três checagens — mesmo shape/validação de
    /// <see cref="DocumentoExigidoBaseLegal.ValidarFormaBasica"/>, ver ali.
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? referencia, TipoAbrangencia abrangencia, StatusBaseLegal status)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(referencia))
        {
            erros.Add(new("referencia", new DomainError("BaseLegalDaComprovacao.ReferenciaObrigatoria", "A referência da base legal é obrigatória.")));
        }
        ValidarAbrangenciaEStatus(erros, abrangencia, status);
        return erros;
    }

    private static void ValidarAbrangenciaEStatus(List<FieldError> erros, TipoAbrangencia abrangencia, StatusBaseLegal status)
    {
        if (abrangencia == TipoAbrangencia.Nenhuma)
        {
            erros.Add(new("abrangencia", new DomainError("BaseLegalDaComprovacao.AbrangenciaObrigatoria", "A abrangência da base legal é obrigatória.")));
        }
        if (status == StatusBaseLegal.Nenhuma)
        {
            erros.Add(new("status", new DomainError("BaseLegalDaComprovacao.StatusObrigatorio", "O status da base legal é obrigatório.")));
        }
    }

    public static Result<BaseLegalDaComprovacao> Criar(
        string referencia, TipoAbrangencia abrangencia, StatusBaseLegal status, string? observacao)
    {
        List<FieldError> erros = ValidarFormaBasica(referencia, abrangencia, status);
        if (erros.Count > 0)
        {
            return Result<BaseLegalDaComprovacao>.ValidationFailure(erros);
        }

        return Result<BaseLegalDaComprovacao>.Success(new BaseLegalDaComprovacao
        {
            Referencia = referencia.Trim(),
            Abrangencia = abrangencia,
            Status = status,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
        });
    }

    internal void VincularNoExigencia(Guid noExigenciaId) =>
        NoExigenciaId = noExigenciaId;
}
