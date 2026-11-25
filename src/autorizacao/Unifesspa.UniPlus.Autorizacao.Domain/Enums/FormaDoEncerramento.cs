namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Como a revisão das concessões terminou.</summary>
public enum FormaDoEncerramento
{
    /// <summary>Não restou concessão pendente.</summary>
    [CodigoDeContrato("CONCLUIDA", "Concluída")]
    Concluida = 1,

    /// <summary>As 72 horas acabaram.</summary>
    [CodigoDeContrato("PRAZO", "Prazo")]
    Prazo = 2,

    /// <summary>A gestão de acessos encerrou a versão anterior, com motivo.</summary>
    [CodigoDeContrato("MANUAL", "Manual")]
    Manual = 3,
}
