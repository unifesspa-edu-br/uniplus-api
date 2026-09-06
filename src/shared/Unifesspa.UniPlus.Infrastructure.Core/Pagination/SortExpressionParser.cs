namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Por que uma expressão de ordenação foi recusada.</summary>
public enum SortExpressionError
{
    /// <summary>Aceita.</summary>
    Nenhum = 0,

    /// <summary>Um dos campos veio vazio — vírgulas seguidas, ou só o sinal de decrescente.</summary>
    CampoVazio = 1,

    /// <summary>O mesmo campo aparece mais de uma vez.</summary>
    CampoRepetido = 2,
}

/// <summary>
/// Lê a expressão de ordenação do parâmetro de consulta: campos separados por
/// vírgula, na ordem de prioridade, cada um opcionalmente prefixado por <c>-</c>
/// para decrescente — <c>nome,-grau</c> ordena por nome crescente e desempata por
/// grau decrescente.
/// </summary>
/// <remarks>
/// <para>É o formato do JSON:API, e a razão de a ordenação caber num parâmetro só
/// é a prioridade: ela é a posição dentro do valor. Espalhar a ordenação por
/// vários parâmetros faria a prioridade depender da ordem em que eles aparecem na
/// URL, que HTTP não garante — e aqui isso teria consequência, porque a ordenação
/// é assinada dentro do cursor.</para>
/// <para>O parser não conhece os campos de recurso nenhum: ele só entende a
/// forma. Saber se <c>grau</c> existe e é ordenável é do catálogo do recurso.</para>
/// </remarks>
public static class SortExpressionParser
{
    private const char SeparadorDeCampos = ',';
    private const char MarcaDeDecrescente = '-';

    /// <summary>
    /// Lê a expressão. Devolve <see langword="false"/> com o motivo em
    /// <paramref name="erro"/> quando a forma não é válida; expressão ausente ou
    /// em branco é aceita como "sem ordenação pedida", com lista vazia.
    /// </summary>
    public static bool TentarLer(
        string? expressao,
        out IReadOnlyList<SortField> campos,
        out SortExpressionError erro)
    {
        campos = [];
        erro = SortExpressionError.Nenhum;

        if (string.IsNullOrWhiteSpace(expressao))
        {
            return true;
        }

        List<SortField> lidos = [];
        HashSet<string> vistos = new(StringComparer.OrdinalIgnoreCase);

        foreach (string bruto in expressao.Split(SeparadorDeCampos))
        {
            string item = bruto.Trim();

            bool descendente = item.StartsWith(MarcaDeDecrescente);
            string campo = descendente ? item[1..].Trim() : item;

            if (campo.Length == 0)
            {
                erro = SortExpressionError.CampoVazio;
                campos = [];
                return false;
            }

            if (!vistos.Add(campo))
            {
                erro = SortExpressionError.CampoRepetido;
                campos = [];
                return false;
            }

            lidos.Add(new SortField(
                campo,
                descendente ? SortDirection.Descending : SortDirection.Ascending));
        }

        campos = lidos;
        return true;
    }
}
