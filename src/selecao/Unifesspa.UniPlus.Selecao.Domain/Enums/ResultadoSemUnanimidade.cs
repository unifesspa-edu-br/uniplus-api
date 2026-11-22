namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que vale quando o critério é a unanimidade e ela não é alcançada.</summary>
public enum ResultadoSemUnanimidade
{
    [CodigoDeContrato("favoravel", "Favorável")]
    Favoravel = 1,
    [CodigoDeContrato("desfavoravel", "Desfavorável")]
    Desfavoravel = 2,
    [CodigoDeContrato("novaVotacao", "Nova votação")]
    NovaVotacao = 3,
}
