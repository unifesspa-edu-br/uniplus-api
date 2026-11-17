namespace Unifesspa.UniPlus.Authorization.Catalogo;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Catálogo de permissões da aplicação inteira: a reunião dos enums que cada módulo
/// registrou com <c>AddCatalogoDePermissoes</c>. A construção confere o que só se vê
/// no conjunto — código ou nome repetido entre módulos e par (natureza, classificação) com mais de uma permissão no módulo — e derruba a subida se algo
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

        ReuniaoDasFontes reuniao = ReuniaoDasFontes.De(fontes);
        reuniao.ExigirCoerente();

        _porCodigo = reuniao.PorCodigo.ToFrozenDictionary(StringComparer.Ordinal);
        Definicoes = EmOrdemDeModuloResourceEAcao(reuniao.PorCodigo.Values);
        Linhas = LinhasEmOrdemDeCodigo(reuniao.PorCodigo.Values);
        Impressao = LinhaDoCatalogo.ImpressaoDe(Linhas);
    }

    private static DefinicaoDePermissao[] EmOrdemDeModuloResourceEAcao(IEnumerable<DefinicaoDePermissao> definicoes) =>
    [
        .. definicoes
            .OrderBy(static d => d.Modulo, StringComparer.Ordinal)
            .ThenBy(static d => d.Resource, StringComparer.Ordinal)
            .ThenBy(static d => d.Acao, StringComparer.Ordinal),
    ];

    private static LinhaDoCatalogo[] LinhasEmOrdemDeCodigo(IEnumerable<DefinicaoDePermissao> definicoes) =>
    [
        .. definicoes
            .Select(static d => new LinhaDoCatalogo(CodigoDePermissao.De(d.Codigo), d.Requisito.Sensibilidade, d.DeUnidade, d.ReservadaAGovernanca))
            .OrderBy(static l => l.Codigo.Valor, StringComparer.Ordinal),
    ];

    /// <summary>Todas as permissões, ordenadas por módulo, resource e ação.</summary>
    public IReadOnlyList<DefinicaoDePermissao> Definicoes { get; }

    /// <summary>O que o catálogo registra de cada permissão, em ordem ordinal de código.</summary>
    public IReadOnlyList<LinhaDoCatalogo> Linhas { get; }

    /// <summary>
    /// A impressão do catálogo desta instância, calculada uma vez na construção: duas instâncias com o mesmo conteúdo têm
    /// a mesma impressão, qualquer que seja a ordem em que registraram os módulos.
    /// </summary>
    public ResumoSha256 Impressao { get; }

    /// <summary>Busca a definição pelo código.</summary>
    public bool TentarObter(string codigo, out DefinicaoDePermissao? definicao)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return _porCodigo.TryGetValue(codigo, out definicao);
    }
}
