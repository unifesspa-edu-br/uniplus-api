namespace Unifesspa.UniPlus.Authorization.Catalogo;

using System.Collections.Frozen;
using System.Reflection;

/// <summary>
/// Catálogo de um enum de permissões: lê o <see cref="PermissaoAttribute"/> de cada
/// membro, valida e entrega a <see cref="DefinicaoDePermissao"/> (ADR-0151).
/// </summary>
/// <typeparam name="TPermissao">Enum de permissões de um módulo.</typeparam>
/// <remarks>
/// A leitura acontece uma vez. Se o enum é inválido, toda consulta repete a mesma
/// <see cref="CatalogoDePermissoesInvalidoException"/>, de modo que o defeito derruba
/// a subida em vez de aparecer como um 403 plausível em execução.
/// </remarks>
public static class CatalogoDePermissoes<TPermissao>
    where TPermissao : struct, Enum
{
    private static readonly Lazy<FrozenDictionary<TPermissao, DefinicaoDePermissao>> Carregado =
        new(Carregar, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Módulo declarado no enum.</summary>
    public static string Modulo => ModuloDoEnum() ?? throw new CatalogoDePermissoesInvalidoException(
        [$"{typeof(TPermissao).Name}: falta [{nameof(ModuloDePermissoesAttribute)}] no enum."]);

    /// <summary>Confere o enum e devolve suas definições, em ordem de código.</summary>
    /// <exception cref="CatalogoDePermissoesInvalidoException">O enum é inválido.</exception>
    public static IReadOnlyList<DefinicaoDePermissao> Todas()
        => [.. Carregado.Value.Values.OrderBy(static d => d.Codigo, StringComparer.Ordinal)];

    /// <summary>Definição de um membro.</summary>
    /// <exception cref="CatalogoDePermissoesInvalidoException">O enum é inválido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    public static DefinicaoDePermissao Obter(TPermissao permissao)
        => Carregado.Value.TryGetValue(permissao, out DefinicaoDePermissao? definicao)
            ? definicao
            : throw new ArgumentOutOfRangeException(
                nameof(permissao),
                permissao,
                $"O valor '{permissao}' ({Convert.ToInt64(permissao, System.Globalization.CultureInfo.InvariantCulture)}) não é membro de {typeof(TPermissao).Name}.");

    private static string? ModuloDoEnum()
        => typeof(TPermissao).GetCustomAttribute<ModuloDePermissoesAttribute>()?.Modulo;

    private static FrozenDictionary<TPermissao, DefinicaoDePermissao> Carregar()
    {
        ConferenciaDoEnum conferencia = new(typeof(TPermissao), ModuloDoEnum());
        Dictionary<TPermissao, DefinicaoDePermissao> definicoes = [];

        foreach (FieldInfo campo in typeof(TPermissao).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (conferencia.Conferir(campo) is { } definicao)
            {
                definicoes[(TPermissao)campo.GetValue(null)!] = definicao;
            }
        }

        conferencia.ExigirCoerente(definicoes.Values);

        return definicoes.ToFrozenDictionary();
    }
}
