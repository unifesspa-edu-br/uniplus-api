namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;

/// <summary>
/// O que os membros de um enum declararam em <see cref="CodigoDeContratoAttribute"/>: escreve o código, o rótulo e a
/// descrição de um valor e lê o valor a partir do código.
/// </summary>
/// <typeparam name="TEnum">Enum cujos membros declaram o código.</typeparam>
/// <remarks>
/// A leitura dos atributos acontece uma vez. Se o enum é inválido, toda consulta repete a mesma
/// <see cref="CodigosDeContratoInvalidosException"/>, inclusive a que traz código nulo, de modo que o defeito derruba a
/// subida. O catálogo não escolhe status HTTP, código de erro nem valor substituto: quem lê decide o que fazer com um
/// código desconhecido.
/// </remarks>
public static class CatalogoDe<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Lazy<Tabela> Carregada = new(Carregar, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Os códigos declarados, na ordem em que o enum declara os membros.</summary>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static IReadOnlyList<CodigoDeclarado> Todos() => Carregada.Value.Todos;

    /// <summary>
    /// Procura o valor pelo código, por comparação exata: sem aparar espaços nem ignorar maiúsculas. Código nulo, vazio
    /// ou desconhecido devolve <see langword="false"/>.
    /// </summary>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static bool TentarObter(string? codigo, out TEnum valor)
    {
        FrozenDictionary<string, TEnum> porCodigo = Carregada.Value.PorCodigo;
        valor = default;

        return codigo is not null && porCodigo.TryGetValue(codigo, out valor);
    }

    /// <summary>O valor do código, que precisa existir.</summary>
    /// <exception cref="ArgumentException">O código não existe no enum.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static TEnum Obter(string codigo) =>
        TentarObter(codigo, out TEnum valor)
            ? valor
            : throw new ArgumentException($"O código '{codigo}' não existe em {typeof(TEnum).Name}.", nameof(codigo));

    internal static CodigoDeclarado Declarado(TEnum valor) =>
        Carregada.Value.PorValor.TryGetValue(valor, out CodigoDeclarado? declarado)
            ? declarado
            : throw new ArgumentOutOfRangeException(
                nameof(valor),
                valor,
                $"O valor '{valor}' ({valor:D}) não é membro de {typeof(TEnum).Name}.");

    private static Tabela Carregar()
    {
        ConferenciaDosCodigos conferencia = new(typeof(TEnum));
        List<(TEnum Valor, CodigoDeclarado Declarado)> membros = ConferirMembros(conferencia);
        conferencia.ExigirCoerente();

        return new Tabela(
            [.. membros.Select(static m => m.Declarado)],
            membros.ToFrozenDictionary(static m => m.Valor, static m => m.Declarado),
            membros.ToFrozenDictionary(static m => m.Declarado.Codigo, static m => m.Valor, StringComparer.Ordinal));
    }

    private static List<(TEnum Valor, CodigoDeclarado Declarado)> ConferirMembros(ConferenciaDosCodigos conferencia)
    {
        List<(TEnum Valor, CodigoDeclarado Declarado)> membros = [];

        foreach (FieldInfo campo in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (conferencia.Conferir(campo) is { } declarado)
            {
                membros.Add((Enum.Parse<TEnum>(campo.Name), declarado));
            }
        }

        return membros;
    }

    private sealed record Tabela(
        ImmutableArray<CodigoDeclarado> Todos,
        FrozenDictionary<TEnum, CodigoDeclarado> PorValor,
        FrozenDictionary<string, TEnum> PorCodigo);
}
