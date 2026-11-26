namespace Unifesspa.UniPlus.Application.Abstractions.Consultas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Códigos de recusa dos parâmetros de consulta das listagens.
/// </summary>
/// <remarks>
/// Compartilhados entre os módulos porque recusar um campo de ordenação inexistente ou um texto
/// pesquisado longo demais não tem nada de específico de um domínio — é a forma do parâmetro. O que
/// cada módulo declara é o código que trafega, no seu próprio catálogo de erros.
/// </remarks>
public static class ConsultaErrorCodes
{
    /// <summary>A ordenação pediu um campo que o recurso não sabe ordenar.</summary>
    public const string CampoDeOrdenacaoInvalido = "Consulta.CampoDeOrdenacaoInvalido";

    /// <summary>A expressão de ordenação não tem a forma esperada.</summary>
    public const string OrdenacaoMalFormada = "Consulta.OrdenacaoMalFormada";

    /// <summary>O texto pesquisado excede o comprimento aceito.</summary>
    public const string BuscaMuitoLonga = TermoDeBusca.CodigoDeBuscaMuitoLonga;
}

/// <summary>Limites do texto pesquisado nas listagens, os de <see cref="TermoDeBusca"/>.</summary>
public static class BuscaPedida
{
    /// <summary>Nome do parâmetro de query que carrega o texto pesquisado, na maioria das listagens.</summary>
    public const string NomeDoParametro = "q";

    /// <summary>Nome do parâmetro do texto pesquisado na listagem das decisões da proteção do catálogo.</summary>
    public const string NomePorExtensoDoParametro = "busca";

    /// <summary>Todos os nomes de parâmetro que carregam o texto pesquisado.</summary>
    public static IReadOnlyList<string> NomesDosParametros { get; } = [NomeDoParametro, NomePorExtensoDoParametro];

    /// <summary>Recusa o termo longo demais; devolve-o inalterado caso caiba.</summary>
    public static Result<string?> Validar(string? termo) =>
        TermoDeBusca.ConferirComprimento(termo) is { } recusa
            ? Result<string?>.Failure(recusa)
            : Result<string?>.Success(termo);
}

/// <summary>Traduz o código exato de um filtro opcional, usando a recusa escolhida pela consulta.</summary>
public static class FiltroPorCodigoPedido
{
    /// <summary>Uma entrada nula ou vazia significa ausência de filtro; espaços não são ignorados.</summary>
    public static Result<TEnum?> Ler<TEnum>(string? codigo, DomainError recusa)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(codigo))
        {
            return Result<TEnum?>.Success(null);
        }

        return CatalogoDe<TEnum>.TentarObter(codigo, out TEnum valor)
            ? Result<TEnum?>.Success(valor)
            : Result<TEnum?>.Failure(recusa);
    }
}

/// <summary>
/// Resolve a ordenação que a consulta pediu contra os campos que o recurso aceita.
/// </summary>
/// <remarks>
/// A recusa é do cliente, não do domínio: pedir um campo que a listagem não ordena
/// é um 422 que precisa dizer <b>qual</b> campo e <b>quais</b> valem, senão quem
/// integra fica adivinhando. Por isso a mensagem nomeia os dois.
/// </remarks>
public static class OrdenacaoPedida
{
    /// <summary>
    /// Devolve a ordenação a aplicar: os campos pedidos, quando todos são aceitos,
    /// ou <paramref name="padrao"/> quando a consulta não pediu ordenação alguma.
    /// </summary>
    public static Result<IReadOnlyList<SortField>> Resolver(
        IReadOnlyList<SortField>? pedidos,
        IReadOnlyList<string> aceitos,
        IReadOnlyList<SortField> padrao)
    {
        ArgumentNullException.ThrowIfNull(aceitos);
        ArgumentNullException.ThrowIfNull(padrao);

        if (pedidos is null || pedidos.Count == 0)
        {
            return Result<IReadOnlyList<SortField>>.Success(padrao);
        }

        SortField? recusado = pedidos
            .FirstOrDefault(pedido => !aceitos.Contains(pedido.Campo, StringComparer.Ordinal));

        return recusado is null
            ? Result<IReadOnlyList<SortField>>.Success(pedidos)
            : Result<IReadOnlyList<SortField>>.Failure(CampoNaoAceito(recusado, aceitos));
    }

    private static DomainError CampoNaoAceito(SortField recusado, IReadOnlyList<string> aceitos) => new(
        ConsultaErrorCodes.CampoDeOrdenacaoInvalido,
        $"Não é possível ordenar por '{recusado.Campo}'. Campos aceitos: {string.Join(", ", aceitos)}.");
}
