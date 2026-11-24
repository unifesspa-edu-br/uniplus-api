namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que basta para a votação da banca produzir decisão.</summary>
public enum CriterioDeVotacao
{
    [CodigoDeContrato("MAIORIA_SIMPLES", "Maioria simples")]
    MaioriaSimples = 1,
    [CodigoDeContrato("MAIORIA_ABSOLUTA", "Maioria absoluta")]
    MaioriaAbsoluta = 2,
    [CodigoDeContrato("UNANIMIDADE", "Unanimidade")]
    Unanimidade = 3,
}
