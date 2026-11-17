namespace Unifesspa.UniPlus.Authorization.Catalogo;

/// <summary>
/// A reunião das fontes do catálogo da aplicação e a conferência do que só se vê no conjunto: código ou nome repetido
/// entre enums e par (natureza, classificação) com mais de uma permissão no módulo (ADR-0151).
/// </summary>
internal sealed class ReuniaoDasFontes
{
    private readonly List<string> _problemas = [];
    private readonly List<(string Quem, DefinicaoDePermissao Definicao)> _todas = [];
    private readonly Dictionary<string, DefinicaoDePermissao> _porNome = new(StringComparer.OrdinalIgnoreCase);

    private ReuniaoDasFontes()
    {
    }

    /// <summary>As definições reunidas, por código.</summary>
    public Dictionary<string, DefinicaoDePermissao> PorCodigo { get; } = new(StringComparer.Ordinal);

    /// <summary>Reúne as definições de todas as fontes, na ordem em que chegam, acusando as repetições.</summary>
    public static ReuniaoDasFontes De(IEnumerable<IFonteDeCatalogo> fontes)
    {
        ArgumentNullException.ThrowIfNull(fontes);

        ReuniaoDasFontes reuniao = new();
        foreach (IFonteDeCatalogo fonte in fontes)
        {
            foreach (DefinicaoDePermissao definicao in fonte.Definicoes())
            {
                reuniao.Incluir(fonte, definicao);
            }
        }

        return reuniao;
    }

    /// <summary>Acrescenta os pares repetidos e derruba a subida se a reunião tiver qualquer problema.</summary>
    /// <exception cref="CatalogoDePermissoesInvalidoException">O conjunto é inválido.</exception>
    public void ExigirCoerente()
    {
        _problemas.AddRange(UnicidadeDoPar.Problemas(_todas));

        if (_problemas.Count > 0)
        {
            throw new CatalogoDePermissoesInvalidoException(_problemas);
        }
    }

    private void Incluir(IFonteDeCatalogo fonte, DefinicaoDePermissao definicao)
    {
        _todas.Add((definicao.Codigo, definicao));

        if (!PorCodigo.TryAdd(definicao.Codigo, definicao))
        {
            _problemas.Add($"{fonte.TipoDoEnum.Name}: o código '{definicao.Codigo}' já está declarado em outro enum.");
        }

        if (!_porNome.TryAdd(definicao.Nome.Trim(), definicao))
        {
            _problemas.Add($"{fonte.TipoDoEnum.Name}: o nome '{definicao.Nome}' já está declarado em outro enum.");
        }
    }
}
