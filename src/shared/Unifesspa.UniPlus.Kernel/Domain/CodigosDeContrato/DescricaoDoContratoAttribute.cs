namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Explica o enum como um todo no contrato publicado: o que vale para todos os códigos e não cabe na descrição de um
/// só deles.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DescricaoDoContratoAttribute : Attribute
{
    /// <summary>Declara a descrição do enum.</summary>
    /// <param name="descricao">O texto que o contrato publica na descrição do enum, em pt-BR.</param>
    public DescricaoDoContratoAttribute(string descricao)
    {
        Descricao = descricao;
    }

    /// <summary>O texto que o contrato publica na descrição do enum.</summary>
    public string Descricao { get; }
}
