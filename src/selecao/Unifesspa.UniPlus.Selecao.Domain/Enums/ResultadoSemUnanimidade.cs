namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que vale quando o critério é a unanimidade e ela não é alcançada.</summary>
public enum ResultadoSemUnanimidade
{
    [CodigoDeContrato("FAVORAVEL", "Favorável")]
    Favoravel = 1,
    [CodigoDeContrato("DESFAVORAVEL", "Desfavorável")]
    Desfavoravel = 2,
    [CodigoDeContrato("NOVA_VOTACAO", "Nova votação")]
    NovaVotacao = 3,
}
