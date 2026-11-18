namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// Um objeto ou uma lista aberto durante a varredura do texto: onde ele está no documento e, no objeto, os nomes de
/// propriedade já vistos, para reconhecer a que se repete.
/// </summary>
internal sealed class NivelDaVarredura(NivelDaVarredura? superior, string segmento, bool ehLista, int ordemDeAbertura)
{
    private readonly HashSet<string> _nomesVistos = new(StringComparer.Ordinal);
    private string _nomeAtual = string.Empty;
    private int _proximoIndice;
    private bool _repetiuNome;

    public NivelDaVarredura? Superior { get; } = superior;

    /// <summary>A posição em que o nível se abriu entre todos os níveis do texto: ordena as causas pela posição no documento.</summary>
    public int OrdemDeAbertura { get; } = ordemDeAbertura;

    public string Ponteiro() => Superior is null ? string.Empty : PonteiroJson.Acrescentar(Superior.Ponteiro(), segmento);

    /// <summary>O segmento que o próximo valor deste nível ocupa: o índice na lista ou o nome da propriedade que acabou de ser lida.</summary>
    public string SegmentoDoProximoValor() => ehLista ? (_proximoIndice++).ToString(System.Globalization.CultureInfo.InvariantCulture) : _nomeAtual;

    /// <summary>Registra a propriedade lida e diz se com ela o objeto passou a repetir um nome, o que só vale na primeira repetição.</summary>
    public bool PassouARepetirNome(string nome)
    {
        _nomeAtual = nome;
        bool repetiu = !_nomesVistos.Add(nome);
        bool primeiraRepeticao = repetiu && !_repetiuNome;
        _repetiuNome |= repetiu;
        return primeiraRepeticao;
    }
}
