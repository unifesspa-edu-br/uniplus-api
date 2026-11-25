namespace Unifesspa.UniPlus.Kernel.EnumsDefeituososDeTeste;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum ComCodigoRepetidoNoExecutavel
{
    [CodigoDeContrato("IGUAL", "Primeiro")]
    Primeiro = 1,

    [CodigoDeContrato("IGUAL", "Segundo")]
    Segundo = 2,
}

public enum ComCodigoAusenteNoExecutavel
{
    [CodigoDeContrato("DECLARADO", "Declarado")]
    Declarado = 1,

    Sem = 2,
}
