namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O código de catálogo do valor que o documento trouxe; o valor ausente ou fora do enum fica sem código, e quem o confere é a regra de domínio.</summary>
internal static class CodigoDoValorLido
{
    internal static string? De<TEnum>(TEnum? valor)
        where TEnum : struct, Enum =>
        valor is { } definido && Enum.IsDefined(definido) ? definido.Codigo() : null;
}
