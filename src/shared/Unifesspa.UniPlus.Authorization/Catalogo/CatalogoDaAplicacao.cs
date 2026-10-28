namespace Unifesspa.UniPlus.Authorization.Catalogo;

using System.Collections.Frozen;

/// <summary>
/// Catálogo de permissões da aplicação inteira: a reunião dos enums que cada módulo
/// registrou com <c>AddCatalogoDePermissoes</c>. A construção confere o que só se vê
/// no conjunto — código ou nome repetido entre módulos — e derruba a subida se algo
/// estiver errado (ADR-0151).
/// </summary>
public sealed class CatalogoDaAplicacao
{
    private readonly FrozenDictionary<string, DefinicaoDePermissao> _porCodigo;

    /// <summary>Cria o catálogo reunindo as fontes registradas.</summary>
    /// <exception cref="CatalogoDePermissoesInvalidoException">O conjunto é inválido.</exception>
    public CatalogoDaAplicacao(IEnumerable<IFonteDeCatalogo> fontes)
    {
        ArgumentNullException.ThrowIfNull(fontes);

        List<string> problemas = [];
        Dictionary<string, DefinicaoDePermissao> porCodigo = new(StringComparer.Ordinal);
        Dictionary<string, DefinicaoDePermissao> porNome = new(StringComparer.OrdinalIgnoreCase);

        foreach (IFonteDeCatalogo fonte in fontes)
        {
            foreach (DefinicaoDePermissao definicao in fonte.Definicoes())
            {
                if (!porCodigo.TryAdd(definicao.Codigo, definicao))
                {
                    problemas.Add($"{fonte.TipoDoEnum.Name}: o código '{definicao.Codigo}' já está declarado em outro enum.");
                }

                if (!porNome.TryAdd(definicao.Nome.Trim(), definicao))
                {
                    problemas.Add($"{fonte.TipoDoEnum.Name}: o nome '{definicao.Nome}' já está declarado em outro enum.");
                }
            }
        }

        if (problemas.Count > 0)
        {
            throw new CatalogoDePermissoesInvalidoException(problemas);
        }

        _porCodigo = porCodigo.ToFrozenDictionary(StringComparer.Ordinal);
        Definicoes =
        [
            .. porCodigo.Values
                .OrderBy(static d => d.Modulo, StringComparer.Ordinal)
                .ThenBy(static d => d.Recurso, StringComparer.Ordinal)
                .ThenBy(static d => d.Acao, StringComparer.Ordinal),
        ];
    }

    /// <summary>Todas as permissões, ordenadas por módulo, recurso e ação.</summary>
    public IReadOnlyList<DefinicaoDePermissao> Definicoes { get; }

    /// <summary>Busca a definição pelo código.</summary>
    public bool TentarObter(string codigo, out DefinicaoDePermissao? definicao)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return _porCodigo.TryGetValue(codigo, out definicao);
    }
}
