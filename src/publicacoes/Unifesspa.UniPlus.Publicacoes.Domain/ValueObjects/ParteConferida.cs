namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O resultado da conferência de uma parte do ato: o valor conferido (ausente quando a parte é
/// opcional e não veio, ou quando foi recusada) e todas as recusas da parte, na ordem em que o corpo do
/// pedido as declara.
/// </summary>
/// <typeparam name="T">O valor da parte, já normalizado.</typeparam>
public sealed record ParteConferida<T>
{
    private ParteConferida(T? valor, IReadOnlyList<FieldError> recusas)
    {
        Valor = valor;
        Recusas = recusas;
    }

    public T? Valor { get; }

    public IReadOnlyList<FieldError> Recusas { get; }

    public bool EstaAceita => Recusas.Count == 0;

    public static ParteConferida<T> Aceita(T? valor) => new(valor, []);

    public static ParteConferida<T> Recusada(IReadOnlyList<FieldError> recusas)
    {
        ArgumentNullException.ThrowIfNull(recusas);
        return new(default, recusas);
    }

    internal static ParteConferida<T> Decidida(T? valor, FieldError? recusa) =>
        recusa is null ? Aceita(valor) : Recusada([recusa]);

    /// <summary>Junta as recusas das partes; só monta o valor quando nenhuma recusou.</summary>
    internal static ParteConferida<T> Compor(IReadOnlyList<FieldError> recusas, Func<T> montar) =>
        recusas.Count == 0 ? Aceita(montar()) : Recusada(recusas);

    /// <summary>
    /// Última linha das invariantes: o valor conferido, ou a exceção com o campo e a mensagem da
    /// primeira recusa.
    /// </summary>
    internal T? ValorOuLancar() =>
        Recusas.Count == 0
            ? Valor
            : throw new ArgumentException(Recusas[0].Error.Message, Recusas[0].Field);
}
