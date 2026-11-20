namespace Unifesspa.UniPlus.Application.Abstractions.Respostas;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como a resposta da API expõe um valor de enum quando o campo precisa também da explicação do que o valor significa: o
/// código, o rótulo e a descrição que o membro declarou em <see cref="CodigoDeContratoAttribute.Descricao"/>, e não a
/// explicação do enum inteiro, que é <see cref="DescricaoDoContratoAttribute"/>. Só serve ao enum em que todo membro
/// declara descrição; o enum que não declara usa <see cref="CodigoComRotulo{TEnum}"/>.
/// </summary>
/// <typeparam name="TEnum">Enum cujos membros declaram o código e a descrição.</typeparam>
public sealed record CodigoComDescricao<TEnum>
    where TEnum : struct, Enum
{
    [JsonConstructor]
    private CodigoComDescricao(string codigo, string rotulo, string descricao)
    {
        Codigo = codigo;
        Rotulo = rotulo;
        Descricao = descricao;
    }

    /// <summary>O código do valor.</summary>
    public string Codigo { get; }

    /// <summary>O texto que a pessoa lê.</summary>
    public string Rotulo { get; }

    /// <summary>O que o valor significa.</summary>
    public string Descricao { get; }

    /// <summary>O código, o rótulo e a descrição que o valor declarou.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    /// <exception cref="InvalidOperationException">O membro não declarou descrição.</exception>
    public static CodigoComDescricao<TEnum> De(TEnum valor) => new(
        valor.Codigo(),
        valor.Rotulo(),
        valor.Descricao() ?? throw new InvalidOperationException(
            $"O membro {valor} de {typeof(TEnum).Name} não declara descrição, que o formato com descrição exige."));
}
