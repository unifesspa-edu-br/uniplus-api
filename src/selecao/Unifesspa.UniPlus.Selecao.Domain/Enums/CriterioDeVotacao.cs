namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que basta para a votação da banca produzir decisão.</summary>
public enum CriterioDeVotacao
{
    [CodigoDeContrato("maioriaSimples", "Maioria simples")]
    MaioriaSimples = 1,
    [CodigoDeContrato("maioriaAbsoluta", "Maioria absoluta")]
    MaioriaAbsoluta = 2,
    [CodigoDeContrato("unanimidade", "Unanimidade")]
    Unanimidade = 3,
}
