namespace Unifesspa.UniPlus.Configuracao.Application.Consultas;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Códigos de recusa dos parâmetros de consulta das listagens.</summary>
public static class ConsultaErrorCodes
{
    /// <summary>A ordenação pediu um campo que o recurso não sabe ordenar.</summary>
    public const string CampoDeOrdenacaoInvalido = "Consulta.CampoDeOrdenacaoInvalido";

    /// <summary>A expressão de ordenação não tem a forma esperada.</summary>
    public const string OrdenacaoMalFormada = "Consulta.OrdenacaoMalFormada";

    /// <summary>O texto pesquisado excede o comprimento aceito.</summary>
    public const string BuscaMuitoLonga = "Consulta.BuscaMuitoLonga";
}

/// <summary>Limites do texto pesquisado nas listagens.</summary>
/// <remarks>
/// O termo não fica só na requisição: ele entra na assinatura que viaja dentro do
/// cursor e é reanexado aos links de navegação. Sem teto, uma busca de alguns
/// quilobytes é aceita na primeira página e produz um link de continuação que
/// estoura o limite de linha de requisição do servidor ou de um proxy no caminho —
/// a listagem responde, mas paginar deixa de ser possível. O teto acompanha o
/// maior campo pesquisável, que é o nome do curso.
/// </remarks>
public static class BuscaPedida
{
    /// <summary>Comprimento máximo do texto pesquisado.</summary>
    public const int ComprimentoMaximo = 200;

    /// <summary>Recusa o termo que passa do teto; devolve-o inalterado caso caiba.</summary>
    public static Result<string?> Validar(string? termo)
    {
        if (termo is null || termo.Length <= ComprimentoMaximo)
        {
            return Result<string?>.Success(termo);
        }

        return Result<string?>.Failure(new DomainError(
            ConsultaErrorCodes.BuscaMuitoLonga,
            $"O texto pesquisado excede {ComprimentoMaximo} caracteres."));
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
            : Result<IReadOnlyList<SortField>>.Failure(new DomainError(
                ConsultaErrorCodes.CampoDeOrdenacaoInvalido,
                $"Não é possível ordenar por '{recusado.Campo}'. "
                + $"Campos aceitos: {string.Join(", ", aceitos)}."));
    }
}
