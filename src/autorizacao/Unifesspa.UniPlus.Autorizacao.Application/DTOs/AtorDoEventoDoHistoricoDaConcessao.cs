namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Quem praticou o ato que a trilha da concessão registra: uma pessoa ou o sistema.</summary>
public enum AtorDoEventoDoHistoricoDaConcessao
{
    [CodigoDeContrato("HUMANO", "Humano")]
    Humano = 1,

    [CodigoDeContrato("SISTEMA", "Sistema")]
    Sistema = 2,
}
