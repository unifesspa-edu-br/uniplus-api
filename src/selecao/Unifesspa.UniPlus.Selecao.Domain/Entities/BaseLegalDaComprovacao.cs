namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Base legal da consequência de uma regra de comprovação.</summary>
public sealed class BaseLegalDaComprovacao : EntityBase
{
    public Guid RegraDeComprovacaoId { get; private set; }

    /// <summary>Referência textual institucional (ex.: "Lei 12.711/2012, art. 3º") — não é PII.</summary>
    public string Referencia { get; private set; } = string.Empty;

    public TipoAbrangencia Abrangencia { get; private set; }

    public StatusBaseLegal Situacao { get; private set; }

    public string? Observacao { get; private set; }

    private BaseLegalDaComprovacao() { }

    /// <summary>
    /// Acumula (ADR-0125) as três checagens — mesmo shape/validação de
    /// <see cref="DocumentoExigidoBaseLegal.ValidarFormaBasica"/>, ver ali.
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? referencia, TipoAbrangencia abrangencia, StatusBaseLegal situacao)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(referencia))
        {
            erros.Add(new("referencia", new DomainError("BaseLegalDaComprovacao.ReferenciaObrigatoria", "A referência da base legal é obrigatória.")));
        }
        ValidarAbrangenciaESituacao(erros, abrangencia, situacao);
        return erros;
    }

    private static void ValidarAbrangenciaESituacao(List<FieldError> erros, TipoAbrangencia abrangencia, StatusBaseLegal situacao)
    {
        if (abrangencia == TipoAbrangencia.Nenhuma)
        {
            erros.Add(new("abrangencia", new DomainError("BaseLegalDaComprovacao.AbrangenciaObrigatoria", "A abrangência da base legal é obrigatória.")));
        }
        if (situacao == StatusBaseLegal.Nenhuma)
        {
            erros.Add(new("situacao", new DomainError("BaseLegalDaComprovacao.SituacaoObrigatoria", "A situação da base legal é obrigatória.")));
        }
    }

    public static Result<BaseLegalDaComprovacao> Criar(
        string referencia, TipoAbrangencia abrangencia, StatusBaseLegal situacao, string? observacao)
    {
        List<FieldError> erros = ValidarFormaBasica(referencia, abrangencia, situacao);
        if (erros.Count > 0)
        {
            return Result<BaseLegalDaComprovacao>.ValidationFailure(erros);
        }

        return Result<BaseLegalDaComprovacao>.Success(new BaseLegalDaComprovacao
        {
            Referencia = referencia.Trim(),
            Abrangencia = abrangencia,
            Situacao = situacao,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
        });
    }

    internal void VincularRegraDeComprovacao(Guid regraDeComprovacaoId) =>
        RegraDeComprovacaoId = regraDeComprovacaoId;
}
