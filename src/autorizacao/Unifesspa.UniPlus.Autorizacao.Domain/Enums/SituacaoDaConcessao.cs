namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação da concessão pedida como filtro da lista.</summary>
public enum SituacaoDaConcessao
{
    [CodigoDeContrato("ATIVA", "Ativa")]
    Ativa = 1,
    [CodigoDeContrato("REVOGADA", "Revogada")]
    Revogada = 2,

    /// <summary>Não revogada, com a vigência já vencida.</summary>
    [CodigoDeContrato("VENCIDA", "Vencida")]
    Vencida = 3,
}

