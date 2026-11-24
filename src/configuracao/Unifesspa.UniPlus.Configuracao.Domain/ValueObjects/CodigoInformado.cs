namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Um código ausente, desconhecido ou reconhecido no catálogo de um enum.</summary>
public readonly record struct CodigoInformado<TEnum>
    where TEnum : struct, Enum
{
    private CodigoInformado(bool informado, TEnum? valor)
    {
        Informado = informado;
        Valor = valor;
    }

    public bool Informado { get; }

    public bool Desconhecido => Informado && Valor is null;

    public TEnum? Valor { get; }

    public static CodigoInformado<TEnum> Ler(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo)
            ? default
            : CatalogoDe<TEnum>.TentarObter(codigo, out TEnum valor)
                ? De(valor)
                : new CodigoInformado<TEnum>(true, null);

    public static CodigoInformado<TEnum> De(TEnum valor)
    {
        if (!Enum.IsDefined(valor))
        {
            throw new ArgumentOutOfRangeException(nameof(valor));
        }

        return new CodigoInformado<TEnum>(true, valor);
    }

    public static implicit operator CodigoInformado<TEnum>(TEnum valor) => De(valor);

    public static implicit operator CodigoInformado<TEnum>(TEnum? valor) => valor is { } conhecido ? De(conhecido) : default;
}
