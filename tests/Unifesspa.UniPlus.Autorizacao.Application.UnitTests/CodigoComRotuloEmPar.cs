namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

internal static class CodigoComRotuloEmPar
{
    /// <summary>O código e o rótulo como um par de texto, para o teste comparar com os valores escritos por extenso.</summary>
    internal static (string Codigo, string Rotulo) EmPar<TEnum>(this CodigoComRotulo<TEnum> valor)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(valor);

        return (valor.Codigo, valor.Rotulo);
    }
}
