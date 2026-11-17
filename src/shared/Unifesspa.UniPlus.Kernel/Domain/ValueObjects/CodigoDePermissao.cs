namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

/// <summary>
/// O código de uma permissão, <c>{módulo}:{resource}:{ação}</c>: três segmentos em kebab-case separados por dois-pontos,
/// sem espaço. O formato vive aqui; quem confere que o módulo bate com o do enum e que o código é único é o catálogo.
/// </summary>
public sealed partial record CodigoDePermissao
{
    private CodigoDePermissao(string valor, string[] segmentos)
    {
        Valor = valor;
        Modulo = segmentos[0];
        Resource = segmentos[1];
        Acao = segmentos[2];
    }

    /// <summary>O código inteiro.</summary>
    public string Valor { get; }

    /// <summary>O módulo dono da permissão: o primeiro segmento.</summary>
    public string Modulo { get; }

    /// <summary>O resource sobre o qual a permissão age: o segundo segmento.</summary>
    public string Resource { get; }

    /// <summary>A ação que a permissão autoriza: o terceiro segmento.</summary>
    public string Acao { get; }

    /// <summary>Cria o código se o texto, como veio e sem nenhuma correção, segue o formato.</summary>
    public static bool TentarCriar(string? texto, [NotNullWhen(true)] out CodigoDePermissao? codigo)
    {
        if (texto is null || !Formato().IsMatch(texto))
        {
            codigo = null;
            return false;
        }

        codigo = new CodigoDePermissao(texto, texto.Split(':'));

        return true;
    }

    /// <summary>O código de um texto que já se sabe no formato, como o gravado no banco; fora do formato, falha alto.</summary>
    /// <exception cref="ArgumentException">O texto não segue o formato do código.</exception>
    public static CodigoDePermissao De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return TentarCriar(texto, out CodigoDePermissao? codigo)
            ? codigo
            : throw new ArgumentException("O código deve seguir {módulo}:{resource}:{ação} em minúsculas, dígitos e hífen.", nameof(texto));
    }

    public override string ToString() => Valor;

    [GeneratedRegex("^" + SegmentoKebab.Padrao + ":" + SegmentoKebab.Padrao + ":" + SegmentoKebab.Padrao + @"\z", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
