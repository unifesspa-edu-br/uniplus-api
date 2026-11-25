namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que o revisor decidiu sobre a concessão de um item da revisão.</summary>
public enum ResultadoDoItemDaRevisao
{
    /// <summary>A concessão segue ativa.</summary>
    [CodigoDeContrato("CONFIRMADA", "Confirmada")]
    Confirmada = 1,

    /// <summary>A concessão foi revogada pela revisão.</summary>
    [CodigoDeContrato("REVOGADA", "Revogada")]
    Revogada = 2,
}
