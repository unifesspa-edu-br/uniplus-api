namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Quem praticou o evento da contenção: uma pessoa ou o próprio sistema, que fecha a suspensão sozinho.</summary>
public enum AtorDoEventoDaContencao
{
    /// <summary>Uma pessoa identificada pelo token.</summary>
    [CodigoDeContrato("HUMANO", "Humano")]
    Humano = 1,

    /// <summary>A retomada decorre de uma decisão de etapa, de uma troca de vigência ou de uma reclassificação.</summary>
    [CodigoDeContrato("SISTEMA", "Sistema")]
    Sistema = 2,
}
