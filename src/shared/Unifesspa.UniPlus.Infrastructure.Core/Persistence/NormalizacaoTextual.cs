namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// O que a normalização para comparação pede do banco e do <c>LIKE</c>: o nome da função que a
/// aplica, a chamada dela sobre uma coluna e o escape dos curingas do termo pesquisado. A regra
/// da normalização em si é a <see cref="FormaParaComparacao"/>, que a função do banco
/// (script <c>Scripts/Pre/002_normalizar_para_comparacao.sql</c>) repete e que serve a todos os
/// módulos.
/// </summary>
public static class NormalizacaoTextual
{
    /// <summary>Nome da função do banco que aplica esta normalização.</summary>
    public const string FuncaoSql = "plataforma.normalizar_para_comparacao";

    /// <summary>Chamada da função sobre a coluna informada.</summary>
    public static string ExpressaoSql(string coluna) => $"{FuncaoSql}({coluna})";

    /// <summary>
    /// Escapa os curingas do <c>LIKE</c> para que o que a pessoa digitou seja
    /// comparado como texto. Sem isso, um <c>%</c> digitado casaria com quase tudo.
    /// A barra invertida vai primeiro, para não escapar as que a própria função
    /// insere em seguida — e é ela mesma o caractere de escape que o Postgres
    /// assume no <c>LIKE</c> quando nenhum outro é declarado.
    /// </summary>
    public static string EscaparCuringas(string termo)
    {
        ArgumentNullException.ThrowIfNull(termo);

        return termo
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    /// <summary>Escapa os curingas do termo validado para uso no <c>LIKE</c>.</summary>
    public static string? PrepararTermoDeBusca(TermoDeBusca? termo) =>
        termo is null ? null : EscaparCuringas(termo.Normalizado);
}
