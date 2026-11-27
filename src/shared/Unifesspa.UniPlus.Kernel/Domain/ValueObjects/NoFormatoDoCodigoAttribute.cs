namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Declara que o campo de texto do pedido é um código no <see cref="FormatoDoCodigo"/>, com o comprimento máximo informado, para
/// que o contrato publique o padrão e o limite. Só descreve o contrato: quem recusa o valor é a regra de domínio, com a recusa de
/// validação acumulada, e não a leitura do corpo, que responderia 400.
/// </summary>
/// <remarks>
/// Num registro posicional, vale na propriedade: <c>[property: NoFormatoDoCodigo(60)]</c>. Não é um atributo de validação do MVC.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NoFormatoDoCodigoAttribute : Attribute
{
    public NoFormatoDoCodigoAttribute(int maximo)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximo, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximo, FormatoDoCodigo.ComprimentoMaximo);
        Maximo = maximo;
    }

    public int Maximo { get; }
}
