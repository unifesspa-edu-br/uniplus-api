namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// O que o esquema do documento diz de um ponto dele e a normalização precisa saber: se o ponto é só de leitura, se é uma parte
/// com chave local e que pontos ficam abaixo. Os descendentes são montados na primeira leitura porque o esquema é recursivo:
/// um ponto pode voltar a conter a si mesmo.
/// </summary>
public sealed class FormaDoNo
{
    private readonly Lazy<IReadOnlyDictionary<string, FormaDoNo>> _propriedades;
    private readonly Lazy<FormaDoNo?> _itens;

    public FormaDoNo(
        IReadOnlyList<string> localizacoes,
        IReadOnlyList<string> tiposDeclarados,
        bool somenteLeitura,
        ParteComChave? parte,
        Func<IReadOnlyDictionary<string, FormaDoNo>> propriedades,
        Func<FormaDoNo?> itens)
    {
        ArgumentNullException.ThrowIfNull(localizacoes);
        ArgumentNullException.ThrowIfNull(tiposDeclarados);
        ArgumentNullException.ThrowIfNull(propriedades);
        ArgumentNullException.ThrowIfNull(itens);

        Localizacoes = localizacoes;
        TiposDeclarados = tiposDeclarados;
        SomenteLeitura = somenteLeitura;
        Parte = parte;
        _propriedades = new Lazy<IReadOnlyDictionary<string, FormaDoNo>>(propriedades);
        _itens = new Lazy<FormaDoNo?>(itens);
    }

    /// <summary>Os pontos do esquema que formam o nó, na ordem em que são expandidos.</summary>
    public IReadOnlyList<string> Localizacoes { get; }

    /// <summary>Os tipos declarados nos trechos expandidos do ponto.</summary>
    public IReadOnlyList<string> TiposDeclarados { get; }

    public bool Numerico => TiposDeclarados.Contains("number", StringComparer.Ordinal) || TiposDeclarados.Contains("integer", StringComparer.Ordinal);

    public bool AceitaNumeroMasNaoTexto => Numerico && !TiposDeclarados.Contains("string", StringComparer.Ordinal);

    public bool AceitaNulo => TiposDeclarados.Contains("null", StringComparer.Ordinal);

    /// <summary>Verdadeiro quando o esquema marca o ponto como só de leitura: a importação o ignora.</summary>
    public bool SomenteLeitura { get; }

    /// <summary>Preenchido quando o ponto é uma parte que exige chave local.</summary>
    public ParteComChave? Parte { get; }

    /// <summary>Os pontos que o esquema declara para cada propriedade de um objeto, por nome.</summary>
    public IReadOnlyDictionary<string, FormaDoNo> Propriedades => _propriedades.Value;

    /// <summary>O ponto que o esquema declara para os elementos de uma lista, ou <see langword="null"/> quando não declara.</summary>
    public FormaDoNo? Itens => _itens.Value;
}
