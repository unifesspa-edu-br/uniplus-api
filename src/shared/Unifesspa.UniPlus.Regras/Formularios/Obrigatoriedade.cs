namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Quando um item ou um termo do formulário é obrigatório: sempre, nunca, ou quando um predicado
/// sobre os fatos de que ele pode depender é verdadeiro (UNI-REQ-0145). A obrigatoriedade é do
/// item, nunca do fato: o mesmo fato pode ser obrigatório num formulário e opcional em outro, e
/// obrigatório para um candidato e opcional para outro.
/// </summary>
public sealed record Obrigatoriedade
{
    private Obrigatoriedade(TipoObrigatoriedade tipo, PredicadoDnf? predicado)
    {
        Tipo = tipo;
        Predicado = predicado;
    }

    public TipoObrigatoriedade Tipo { get; }

    /// <summary>O predicado que decide a obrigatoriedade — presente só em <see cref="TipoObrigatoriedade.Quando"/>.</summary>
    public PredicadoDnf? Predicado { get; }

    public static Obrigatoriedade Sempre { get; } = new(TipoObrigatoriedade.Sempre, predicado: null);

    public static Obrigatoriedade Nunca { get; } = new(TipoObrigatoriedade.Nunca, predicado: null);

    public static Obrigatoriedade Quando(PredicadoDnf predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        return new(TipoObrigatoriedade.Quando, predicado);
    }

    /// <summary>Os fatos citados pelo predicado — vazio em <see cref="Sempre"/> e <see cref="Nunca"/>.</summary>
    public IReadOnlyCollection<string> FatosCitados => Predicado?.FatosCitados ?? [];

    public Ternario Avaliar(IReadOnlyDictionary<string, FatoResolvido> fatos) => Tipo switch
    {
        TipoObrigatoriedade.Sempre => Ternario.Verdadeiro,
        TipoObrigatoriedade.Nunca => Ternario.Falso,
        TipoObrigatoriedade.Quando => Predicado!.Avaliar(fatos),
        _ => throw new InvalidOperationException($"Obrigatoriedade desconhecida: {Tipo}."),
    };
}

/// <summary>As três formas de obrigatoriedade de um item ou termo.</summary>
public enum TipoObrigatoriedade
{
    Nenhuma = 0,
    Sempre = 1,
    Nunca = 2,
    Quando = 3,
}
