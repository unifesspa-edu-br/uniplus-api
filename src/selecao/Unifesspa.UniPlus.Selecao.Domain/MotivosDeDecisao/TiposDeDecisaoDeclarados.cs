namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Os tipos de decisão que uma fase ou etapa declara produzir: zero, um ou vários, cada um do
/// vocabulário de <see cref="TipoMotivoDeDecisao"/>. Fase e etapa declaram o seu conjunto sem
/// herança entre elas; só a repetição dentro da mesma declaração é recusada.
/// </summary>
public static class TiposDeDecisaoDeclarados
{
    public const string Campo = "tiposDeDecisao";

    /// <summary>
    /// Confere a declaração como chegou e acumula todas as recusas, por posição de entrada
    /// (<c>{campo}[j]</c>): o elemento nulo, em branco ou fora do vocabulário é tipo inválido; o
    /// tipo válido repetido é recusado a partir da segunda ocorrência. Não altera nada.
    /// </summary>
    public static IReadOnlyList<FieldError> Validar(IReadOnlyList<string?>? bruto, string campo)
    {
        IReadOnlyList<string?> elementos = bruto ?? [];
        List<FieldError> erros = [];
        HashSet<TipoMotivoDeDecisao> jaDeclarados = [];

        for (int posicao = 0; posicao < elementos.Count; posicao++)
        {
            if (ErroDoElemento(elementos[posicao], jaDeclarados) is { } erro)
            {
                erros.Add(new FieldError($"{campo}[{posicao}]", erro));
            }
        }

        return erros;
    }

    /// <summary>Se alguma das recusas da declaração é de um tipo de decisão: o lugar não sabe quais tipos produz.</summary>
    public static bool TemRecusa(IEnumerable<FieldError> recusas)
    {
        ArgumentNullException.ThrowIfNull(recusas);

        return recusas.Any(static recusa => recusa.Field?.StartsWith(Campo, StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Os tipos da declaração quando <see cref="Validar"/> a aceita; nulo quando recusa, para quem depende
    /// do dado não o ler recusado. A declaração ausente é a declaração vazia. Não emite recusa.
    /// </summary>
    public static IReadOnlySet<TipoMotivoDeDecisao>? Aceitos(IReadOnlyList<string?>? bruto) =>
        Validar(bruto, Campo).Count > 0
            ? null
            : Reconhecidos(bruto).ToHashSet();

    /// <summary>
    /// Os tipos em ordem alfabética ordinal do código, sem repetição. Pressupõe a declaração já
    /// conferida por <see cref="Validar"/>: o que não pertence ao vocabulário fica de fora.
    /// </summary>
    public static IReadOnlyList<TipoMotivoDeDecisao> Canonicos(IReadOnlyList<string?>? bruto) =>
        [.. Reconhecidos(bruto).Distinct().OrderBy(static tipo => tipo.Codigo(), StringComparer.Ordinal)];

    private static IEnumerable<TipoMotivoDeDecisao> Reconhecidos(IReadOnlyList<string?>? bruto)
    {
        foreach (string? codigo in bruto ?? [])
        {
            if (CatalogoDe<TipoMotivoDeDecisao>.TentarObter(codigo, out TipoMotivoDeDecisao tipo))
            {
                yield return tipo;
            }
        }
    }

    private static DomainError? ErroDoElemento(string? codigo, HashSet<TipoMotivoDeDecisao> jaDeclarados)
    {
        if (!CatalogoDe<TipoMotivoDeDecisao>.TentarObter(codigo, out TipoMotivoDeDecisao tipo))
        {
            return TipoDoMotivoInformado.TipoForaDoVocabulario();
        }

        return jaDeclarados.Add(tipo)
            ? null
            : new DomainError(
                MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado,
                $"O tipo de decisão '{tipo.Rotulo()}' é declarado mais de uma vez; cada tipo é declarado uma única vez.");
    }
}
