namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O estado de um fato do candidato no instante da avaliação (Story #926): o
/// discriminador <see cref="Estado"/> mais o valor, quando há um.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que <c>valor = null</c> deixe de ser ambíguo. Um fato sem valor
/// pode ser <see cref="EstadoFato.NaoAplicavel"/> — o campo nem foi apresentado —
/// ou <see cref="EstadoFato.Indeterminado"/> — o campo se aplica e ainda não foi
/// respondido. Os dois casos levam a decisões opostas sobre a exigência que
/// depende do fato, então o estado é declarado, nunca inferido da ausência.
/// </para>
/// <para>
/// As três factories são o único caminho de construção, e cada uma fixa a
/// combinação válida: só <see cref="Resolvido"/> carrega valor, e ele nunca é
/// <see cref="JsonValueKind.Null"/> nem <see cref="JsonValueKind.Undefined"/> —
/// aceitar um desses reintroduziria pela porta dos fundos a ambiguidade que este
/// tipo fecha.
/// </para>
/// </remarks>
public sealed record FatoResolvido
{
    private static readonly FatoResolvido NaoAplicavelInstancia = new(EstadoFato.NaoAplicavel, valor: null);
    private static readonly FatoResolvido IndeterminadoInstancia = new(EstadoFato.Indeterminado, valor: null);

    private FatoResolvido(EstadoFato estado, JsonElement? valor)
    {
        Estado = estado;
        Valor = valor;
    }

    public EstadoFato Estado { get; }

    /// <summary>
    /// O valor do fato — preenchido se, e somente se, <see cref="Estado"/> é
    /// <see cref="EstadoFato.Resolvido"/>.
    /// </summary>
    public JsonElement? Valor { get; }

    /// <summary>
    /// O fato não se aplica ao candidato (pré-condição falsa). Estado resolvido e
    /// definitivo: quem depende dele é dispensado, não fica pendente.
    /// </summary>
    public static FatoResolvido NaoAplicavel() => NaoAplicavelInstancia;

    /// <summary>
    /// O fato se aplica, mas o valor ainda não é conhecido. Quem depende dele fica
    /// pendente (fail-closed).
    /// </summary>
    public static FatoResolvido Indeterminado() => IndeterminadoInstancia;

    /// <summary>
    /// O fato se aplica e tem valor. Um <paramref name="valor"/> nulo ou indefinido
    /// é recusado: essa forma significa "sem valor", e sem valor o fato é
    /// <see cref="NaoAplicavel"/> ou <see cref="Indeterminado"/> — quem chama tem de
    /// dizer qual.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Quando <paramref name="valor"/> é <see cref="JsonValueKind.Null"/> ou
    /// <see cref="JsonValueKind.Undefined"/>.
    /// </exception>
    public static FatoResolvido Resolvido(JsonElement valor)
    {
        if (valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new ArgumentException(
                "Um fato resolvido exige valor. Para a ausência de valor, use NaoAplicavel() ou Indeterminado() — "
                + "o estado é declarado, nunca inferido do nulo.",
                nameof(valor));
        }

        // Clone desacopla o valor do JsonDocument de origem: sem isso, um fato
        // sobreviveria ao descarte do documento que o produziu e a leitura posterior
        // lançaria ObjectDisposedException no meio da avaliação.
        return new FatoResolvido(EstadoFato.Resolvido, valor.Clone());
    }
}
