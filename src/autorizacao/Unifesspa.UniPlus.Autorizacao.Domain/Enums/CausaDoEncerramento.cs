namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que encerrou uma concessão, derivado na leitura.</summary>
public enum CausaDoEncerramento
{
    /// <summary>A concessão foi revogada, por uma pessoa ou pela revisão das concessões.</summary>
    [CodigoDeContrato("REVOGADA", "Revogada")]
    Revogada = 1,

    /// <summary>A vigência da concessão chegou ao fim, sem ato gravado.</summary>
    [CodigoDeContrato("VENCIDA", "Vencida")]
    Vencida = 2,
}
