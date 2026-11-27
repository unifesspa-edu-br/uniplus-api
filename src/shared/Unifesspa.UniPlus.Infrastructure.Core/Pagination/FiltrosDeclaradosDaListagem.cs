namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Os parâmetros de consulta que uma listagem declara além da paginação: são os únicos que o link de navegação
/// repete, os mesmos que o contrato publicado documenta (ADR-0026). Cada filtro simples volta com um valor, o que a
/// listagem recebe; o filtro que aceita vários valores volta com todos, sem repetição.
/// </summary>
internal sealed class FiltrosDeclaradosDaListagem
{
    internal static readonly string[] ParametrosDePaginacao = ["cursor", "limit", "direction"];

    private readonly IReadOnlyList<FiltroDeclarado> _filtros;

    private FiltrosDeclaradosDaListagem(IReadOnlyList<FiltroDeclarado> filtros) => _filtros = filtros;

    internal static FiltrosDeclaradosDaListagem De(ApiDescription descricao)
    {
        ArgumentNullException.ThrowIfNull(descricao);

        return new([.. descricao.ParameterDescriptions
            .Where(static parametro => EhFiltro(parametro))
            .Select(static parametro => new FiltroDeclarado(parametro.Name, parametro.ModelMetadata?.IsEnumerableType == true))
            .DistinctBy(static filtro => filtro.Nome, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>
    /// Os pares <c>nome=valor</c> já escapados, na ordem da declaração. Valor maior que
    /// <paramref name="comprimentoMaximoDoValor"/>, medido sem os espaços das pontas, não é repetido.
    /// </summary>
    internal IReadOnlyList<string> PartesDoLink(IQueryCollection consulta, int comprimentoMaximoDoValor)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return [.. _filtros.SelectMany(filtro => ValoresDoLink(filtro, consulta)
            .Where(valor => valor.Length <= comprimentoMaximoDoValor)
            .Select(valor => $"{Uri.EscapeDataString(filtro.Nome)}={Uri.EscapeDataString(valor)}"))];
    }

    private static bool EhFiltro(ApiParameterDescription parametro) =>
        parametro.Source == BindingSource.Query
        && parametro.ParameterDescriptor?.ParameterType != typeof(PageRequest)
        && !ParametrosDePaginacao.Contains(parametro.Name, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> ValoresDoLink(FiltroDeclarado filtro, IQueryCollection consulta)
    {
        IEnumerable<string> pedidos = consulta
            .Where(parametro => string.Equals(parametro.Key, filtro.Nome, StringComparison.OrdinalIgnoreCase))
            .SelectMany(static parametro => parametro.Value.OfType<string>())
            .Select(valor => ComoALeituraDaListagem(filtro.Nome, valor));

        return filtro.AceitaVariosValores ? pedidos.Distinct(StringComparer.Ordinal) : pedidos.Take(1);
    }

    // Só o texto pesquisado volta sem os espaços das pontas, como a busca o aceita: o limite de comprimento
    // não os conta e o link não os repete. Os demais filtros voltam como vieram, porque alguns leem o valor
    // bruto e aparar mudaria a coleção.
    private static string ComoALeituraDaListagem(string nome, string valor) =>
        BuscaPedida.NomesDosParametros.Contains(nome, StringComparer.OrdinalIgnoreCase) ? valor.Trim() : valor;

    private sealed record FiltroDeclarado(string Nome, bool AceitaVariosValores);
}
