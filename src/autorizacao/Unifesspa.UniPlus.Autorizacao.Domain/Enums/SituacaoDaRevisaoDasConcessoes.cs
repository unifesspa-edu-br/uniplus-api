namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação apresentada para a revisão das concessões.</summary>
public enum SituacaoDaRevisaoDasConcessoes
{
    [CodigoDeContrato("ABERTA", "Aberta")]
    Aberta = 1,
    [CodigoDeContrato("ENCERRANDO", "Em encerramento")]
    Encerrando = 2,
    [CodigoDeContrato("ENCERRADA", "Encerrada")]
    Encerrada = 3,
}
