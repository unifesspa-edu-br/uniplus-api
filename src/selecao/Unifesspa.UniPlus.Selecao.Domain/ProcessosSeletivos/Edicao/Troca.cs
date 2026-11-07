namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>O que sai e o que entra numa coleção da raiz. É dado: quem aplica é a raiz, que conhece a lista.</summary>
internal readonly struct Troca<T>
    where T : class, IParteDoProcesso
{
    private readonly HashSet<T>? _saem;
    private readonly List<T>? _entram;

    internal Troca(IEnumerable<T> saem, IEnumerable<T> entram)
    {
        _saem = new HashSet<T>(saem, ReferenceEqualityComparer.Instance);
        _entram = [.. entram];
    }

    internal IReadOnlySet<T> Saem => _saem ?? new HashSet<T>();

    /// <summary>Na ordem em que chegam à coleção.</summary>
    internal IReadOnlyList<T> Entram => _entram ?? [];
}

internal static class Troca
{
    internal static Troca<T> Substituindo<T>(IEnumerable<T> saem, IEnumerable<T> entram)
        where T : class, IParteDoProcesso =>
        new(saem, entram);

    internal static Troca<T> Retirando<T>(IEnumerable<T> saem)
        where T : class, IParteDoProcesso =>
        new(saem, []);

    internal static Troca<T> Reunindo<T>(IReadOnlyCollection<Troca<T>> trocas)
        where T : class, IParteDoProcesso =>
        new(trocas.SelectMany(static t => t.Saem), trocas.SelectMany(static t => t.Entram));

    internal static Troca<T> Acrescentando<T>(IEnumerable<T> entram)
        where T : class, IParteDoProcesso =>
        new([], entram);
}
