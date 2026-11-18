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
        bool somenteLeitura,
        ParteComChave? parte,
        Func<IReadOnlyDictionary<string, FormaDoNo>> propriedades,
        Func<FormaDoNo?> itens)
    {
        ArgumentNullException.ThrowIfNull(propriedades);
        ArgumentNullException.ThrowIfNull(itens);

        SomenteLeitura = somenteLeitura;
        Parte = parte;
        _propriedades = new Lazy<IReadOnlyDictionary<string, FormaDoNo>>(propriedades);
        _itens = new Lazy<FormaDoNo?>(itens);
    }

    /// <summary>Verdadeiro quando o esquema marca o ponto como só de leitura: a importação o ignora.</summary>
    public bool SomenteLeitura { get; }

    /// <summary>Preenchido quando o ponto é uma parte que exige chave local.</summary>
    public ParteComChave? Parte { get; }

    /// <summary>Os pontos que o esquema declara para cada propriedade de um objeto, por nome.</summary>
    public IReadOnlyDictionary<string, FormaDoNo> Propriedades => _propriedades.Value;

    /// <summary>O ponto que o esquema declara para os elementos de uma lista, ou <see langword="null"/> quando não declara.</summary>
    public FormaDoNo? Itens => _itens.Value;
}
