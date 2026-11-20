namespace Unifesspa.UniPlus.Application.Abstractions.Respostas;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como a resposta da API expõe um valor de enum que declara <see cref="CodigoDeContratoAttribute"/>: o código, que é
/// o contrato, e o rótulo, que a tela mostra. O enum nunca sai cru na resposta.
/// </summary>
/// <typeparam name="TEnum">Enum cujos membros declaram o código.</typeparam>
public sealed record CodigoComRotulo<TEnum>
    where TEnum : struct, Enum
{
    [JsonConstructor]
    private CodigoComRotulo(string codigo, string rotulo)
    {
        Codigo = codigo;
        Rotulo = rotulo;
    }

    /// <summary>O código do valor.</summary>
    public string Codigo { get; }

    /// <summary>O texto que a pessoa lê.</summary>
    public string Rotulo { get; }

    /// <summary>O código e o rótulo que o valor declarou.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    public static CodigoComRotulo<TEnum> De(TEnum valor) => new(valor.Codigo(), valor.Rotulo());
}
