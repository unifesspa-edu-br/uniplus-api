namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Quem tomou a decisão da revisão: uma pessoa ou o sistema.</summary>
public enum AtorDaMigracao
{
    [CodigoDeContrato("HUMANO", "Humano")]
    Humano = 1,
    [CodigoDeContrato("SISTEMA", "Sistema")]
    Sistema = 2,
}
