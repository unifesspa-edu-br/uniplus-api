namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que uma etapa decidiu sobre a permissão reclassificada numa concessão.</summary>
public enum ResultadoDaLiberacao
{
    /// <summary>A etapa libera a permissão.</summary>
    [CodigoDeContrato("LIBERADA", "Liberada")]
    Liberada = 1,

    /// <summary>A etapa mantém a permissão suspensa, com motivo.</summary>
    [CodigoDeContrato("MANTIDA", "Mantida")]
    Mantida = 2,
}
