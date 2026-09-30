namespace Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Os fatos de que uma regra do formulário pode depender (UNI-REQ-0145): as respostas a campos
/// anteriores na ordem do formulário, os fatos já conhecidos antes dele — em outra finalidade, os
/// coletados pela inscrição (UNI-REQ-0144) — e os fatos derivados por regra cujas dependências
/// estão todas entre esses.
/// </summary>
public sealed class DependenciasDoFormulario
{
    /// <summary>A posição de um termo: depois de todos os campos do formulário, qualquer que seja a ordem deles.</summary>
    public const long PosicaoDosTermos = long.MaxValue;

    private const long ConhecidoAntes = -1;

    private readonly IReadOnlyDictionary<string, int> _posicoes;
    private readonly IReadOnlySet<string> _conhecidosAntes;
    private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _derivacoes;
    private readonly Dictionary<string, long?> _conhecidoEm = new(StringComparer.Ordinal);

    private DependenciasDoFormulario(
        IReadOnlyDictionary<string, int> posicoes,
        IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        _posicoes = posicoes;
        _conhecidosAntes = conhecidosAntes;
        _derivacoes = derivacoes;
    }

    /// <param name="posicoes">A posição de cada campo do formulário, na ordem de coleta.</param>
    /// <param name="conhecidosAntes">Os fatos conhecidos antes do primeiro campo do formulário.</param>
    /// <param name="derivacoes">As dependências de cada fato derivado por regra.</param>
    public static DependenciasDoFormulario Criar(
        IReadOnlyDictionary<string, int> posicoes,
        IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(posicoes);
        ArgumentNullException.ThrowIfNull(conhecidosAntes);
        ArgumentNullException.ThrowIfNull(derivacoes);
        return new DependenciasDoFormulario(posicoes, conhecidosAntes, derivacoes);
    }

    /// <summary>Se uma regra na posição dada pode citar o fato, e por que não, quando não pode.</summary>
    public RecusaDeCitacao? Conferir(string citado, long posicaoDoCitante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(citado);
        return ConhecidoEm(citado, []) switch
        {
            null => RecusaDeCitacao.FatoNaoConhecido,
            { } posicao when posicao >= posicaoDoCitante => RecusaDeCitacao.FatoPosterior,
            _ => null,
        };
    }

    /// <summary>
    /// A posição a partir da qual o fato é conhecido: a do próprio campo, antes do formulário para o
    /// pressuposto, e a da última dependência para o derivado. Nula quando o fato não é conhecido no
    /// formulário, inclusive o derivado que depende, direta ou indiretamente, de si mesmo.
    /// </summary>
    private long? ConhecidoEm(string fato, HashSet<string> emAvaliacao)
    {
        if (_conhecidoEm.TryGetValue(fato, out long? calculado))
        {
            return calculado;
        }

        long? posicao;
        if (_conhecidosAntes.Contains(fato))
        {
            posicao = ConhecidoAntes;
        }
        else if (_posicoes.TryGetValue(fato, out int doCampo))
        {
            posicao = doCampo;
        }
        else if (_derivacoes.TryGetValue(fato, out IReadOnlyCollection<string>? dependencias) && emAvaliacao.Add(fato))
        {
            posicao = ConhecidoAntes;
            foreach (string dependencia in dependencias)
            {
                posicao = ConhecidoEm(dependencia, emAvaliacao) is { } daDependencia ? Math.Max(posicao.Value, daDependencia) : null;
                if (posicao is null)
                {
                    break;
                }
            }

            emAvaliacao.Remove(fato);
        }
        else
        {
            return null;
        }

        _conhecidoEm[fato] = posicao;
        return posicao;
    }
}

/// <summary>Por que uma regra do formulário não pode citar um fato.</summary>
public enum RecusaDeCitacao
{
    /// <summary>O fato não é campo do formulário, nem conhecido antes dele, nem derivado desses.</summary>
    FatoNaoConhecido = 1,

    /// <summary>O fato só fica conhecido no próprio campo ou depois dele.</summary>
    FatoPosterior = 2,
}
