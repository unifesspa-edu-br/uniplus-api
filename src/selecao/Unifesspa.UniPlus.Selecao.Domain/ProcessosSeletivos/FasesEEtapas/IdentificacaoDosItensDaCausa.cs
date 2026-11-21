namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O identificador estável de cada item de lista que o campo de uma causa atravessa. A causa aponta a fase, a etapa e o recurso pela
/// posição canônica deles, e entre a recusa e a nova leitura outra pessoa pode gravar o processo e mudar as posições; o identificador
/// deixa o cliente achar o item certo mesmo assim. A fase se identifica pelo código do tipo de fase, que é único no cronograma; a
/// etapa e o recurso, pelo identificador deles. O item que não existe mais não aponta outro: o identificador que não acha item
/// nenhum deixa a causa na lista que o contém. Roda antes de o campo virar ponteiro do documento, porque o índice que ele lê é o canônico.
/// </summary>
public static class IdentificacaoDosItensDaCausa
{
    private const string ListaDasEtapas = "etapas";
    private const string ListaDosRecursos = "recursos";

    /// <summary>As causas com os identificadores dos itens que o campo atravessa; a causa que não aponta item de lista fica como está.</summary>
    public static IReadOnlyList<FieldError> Identificar(IEnumerable<FieldError> causas, ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(causas);
        ArgumentNullException.ThrowIfNull(configuracao);

        return [.. causas.Select(causa => Identificar(causa, configuracao))];
    }

    private static FieldError Identificar(FieldError causa, ConfiguracaoDeclarada configuracao)
    {
        IdentificadorDoItem[] itens = [.. DoCaminho(causa.Field, configuracao)];

        return itens.Length == 0 ? causa : causa with { Identificadores = itens };
    }

    private static IEnumerable<IdentificadorDoItem> DoCaminho(string? campo, ConfiguracaoDeclarada configuracao)
    {
        if (Ler(campo, CronogramaDoProcesso.ListaDasFases) is { } fase)
        {
            return DaFase(fase, configuracao);
        }

        return Ler(campo, ListaDasEtapas) is { } etapa ? DaEtapa(etapa, configuracao) : [];
    }

    private static IEnumerable<IdentificadorDoItem> DaFase(ItemDoCaminho fase, ConfiguracaoDeclarada configuracao) =>
        configuracao.Cronograma.CronogramaFases.ElementAtOrDefault(fase.Posicao) is { } encontrada
            ? [new(CronogramaDoProcesso.ListaDasFases, encontrada.Codigo)]
            : [];

    private static IEnumerable<IdentificadorDoItem> DaEtapa(ItemDoCaminho caminho, ConfiguracaoDeclarada configuracao)
    {
        if (configuracao.Avaliacao.Etapas.ElementAtOrDefault(caminho.Posicao) is not { } etapa)
        {
            return [];
        }

        IdentificadorDoItem daEtapa = new(ListaDasEtapas, Texto(etapa.Id));

        return Ler(caminho.Resto, ListaDosRecursos) is { } recurso && etapa.Recursos.ElementAtOrDefault(recurso.Posicao) is { } encontrado
            ? [daEtapa, new(ListaDosRecursos, Texto(encontrado.Id))]
            : [daEtapa];
    }

    private static string Texto(Guid identificador) => identificador.ToString("D", CultureInfo.InvariantCulture);

    // O trecho do caminho que começa pela lista e pela posição entre colchetes; o que vem depois fica para o item de dentro.
    private static ItemDoCaminho? Ler(string? caminho, string lista)
    {
        string prefixo = $"{lista}[";
        if (caminho is null || !caminho.StartsWith(prefixo, StringComparison.Ordinal))
        {
            return null;
        }

        int fecha = caminho.IndexOf(']', prefixo.Length);

        return fecha > prefixo.Length && int.TryParse(caminho.AsSpan(prefixo.Length, fecha - prefixo.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int posicao)
            ? new ItemDoCaminho(posicao, caminho[(fecha + 1)..].TrimStart('.'))
            : null;
    }

    private sealed record ItemDoCaminho(int Posicao, string Resto);
}
