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
