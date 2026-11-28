namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using System.Text.RegularExpressions;

/// <summary>
/// A forma do código do leiaute: a mesma do código estável de catálogo do documento da configuração, de até 50 caracteres ASCII, e
/// nunca um identificador único. Só caracteres ASCII, então dois códigos aceitos não diferem só pela normalização do texto.
/// </summary>
public static partial class FormaDoCodigoDoLeiaute
{
    public static bool TemFormaValida(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return codigo.Length <= RelatorioDeclarado.LeiauteCodigoComprimentoMaximo && !Identificador().IsMatch(codigo) && Catalogo().IsMatch(codigo);
    }

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Catalogo();

    [GeneratedRegex(@"\A[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Identificador();
}
