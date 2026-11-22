namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Confere se um decimal cabe na precisão declarada.</summary>
public static class PrecisaoDecimal
{
    public static bool Cabe(decimal? valor, int digitos, int casas)
    {
        if (valor is null)
        {
            return true;
        }

        decimal limite = 1m;
        for (int i = 0; i < digitos - casas; i++)
        {
            limite *= 10m;
        }

        return (decimal.GetBits(valor.Value)[3] >> 16 & 0xFF) <= casas
            && valor.Value > -limite && valor.Value < limite;
    }

    public static FieldError? Recusa(decimal? valor, int digitos, int casas, string campo, string mensagem) =>
        Cabe(valor, digitos, casas) ? null : new(campo, new DomainError(CodigosDeValidacao.Formato, mensagem));
}
