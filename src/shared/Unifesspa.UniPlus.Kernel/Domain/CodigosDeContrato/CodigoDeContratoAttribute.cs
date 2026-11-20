namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Declara o código, o rótulo e, se for útil, a descrição de um membro de enum que circula fora do processo: na
/// resposta da API, no banco ou no conteúdo congelado.
/// </summary>
/// <remarks>
/// O código é escrito à mão e nunca sai do nome do membro: renomear o membro não pode mudá-lo. Trocar o código é
/// mudar o contrato e o dado gravado.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class CodigoDeContratoAttribute : Attribute
{
    /// <summary>Declara o código e o rótulo de um membro.</summary>
    /// <param name="codigo">O código que circula fora do processo.</param>
    /// <param name="rotulo">O texto que a pessoa lê, em pt-BR.</param>
    public CodigoDeContratoAttribute(string codigo, string rotulo)
    {
        Codigo = codigo;
        Rotulo = rotulo;
    }

    /// <summary>O código que circula fora do processo.</summary>
    public string Codigo { get; }

    /// <summary>O texto que a pessoa lê.</summary>
    public string Rotulo { get; }

    /// <summary>Explica o que o valor significa; ausente quando o rótulo basta.</summary>
    public string? Descricao { get; init; }
}
