namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// O que o Geo disse de um município de prova: confirmou e devolveu o nome, ou não conferiu o município (a indisponibilidade do Geo é outra resposta, antes da conferência). O que não foi conferido nunca vale como aceito
/// (falha fechada, ADR-0090).
/// </summary>
public abstract record ResultadoDoMunicipioNoGeo
{
    private ResultadoDoMunicipioNoGeo()
    {
    }

    /// <summary>O Geo confirmou o município e devolveu o nome, que a versão congela.</summary>
    public static ResultadoDoMunicipioNoGeo Conferido(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        return new ConferidoNoGeo(nome);
    }

    /// <summary>O município não foi conferido no Geo: a chamada não foi feita ou o Geo não o conhece.</summary>
    public static ResultadoDoMunicipioNoGeo NaoConferido { get; } = new NaoConferidoNoGeo();

    internal sealed record ConferidoNoGeo(string Nome) : ResultadoDoMunicipioNoGeo;

    internal sealed record NaoConferidoNoGeo : ResultadoDoMunicipioNoGeo;
}

/// <summary>
/// O resultado da conferência no Geo de cada município de prova, pelo código do IBGE: o domínio o
/// recebe já resolvido, sem conhecer a porta nem o Geo. O código sem resultado é um município não conferido.
/// </summary>
public sealed record MunicipiosConferidosNoGeo
{
    private readonly IReadOnlyDictionary<string, ResultadoDoMunicipioNoGeo> _resultados;

    private MunicipiosConferidosNoGeo(IReadOnlyDictionary<string, ResultadoDoMunicipioNoGeo> resultados) => _resultados = resultados;

    /// <summary>Nenhum município conferido.</summary>
    public static MunicipiosConferidosNoGeo Nenhum { get; } = new(new Dictionary<string, ResultadoDoMunicipioNoGeo>());

    public static MunicipiosConferidosNoGeo De(IReadOnlyDictionary<string, ResultadoDoMunicipioNoGeo> resultados)
    {
        ArgumentNullException.ThrowIfNull(resultados);
        if (resultados.Values.Any(static resultado => resultado is null))
        {
            throw new ArgumentException("Todo município conferido tem um resultado.", nameof(resultados));
        }

        return new(new Dictionary<string, ResultadoDoMunicipioNoGeo>(resultados, StringComparer.Ordinal));
    }

    /// <summary>Os conjuntos são iguais quando trazem o mesmo resultado para os mesmos municípios.</summary>
    public bool Equals(MunicipiosConferidosNoGeo? outros) =>
        outros is not null && _resultados.Count == outros._resultados.Count
        && _resultados.All(resultado => outros._resultados.TryGetValue(resultado.Key, out ResultadoDoMunicipioNoGeo? doOutro) && resultado.Value.Equals(doOutro));

    public override int GetHashCode() => _resultados.Count;

    /// <summary>O que o Geo disse do município; não conferido quando não há resultado.</summary>
    public ResultadoDoMunicipioNoGeo Resultado(string codigoIbge) =>
        _resultados.GetValueOrDefault(codigoIbge) ?? ResultadoDoMunicipioNoGeo.NaoConferido;

    /// <summary>O nome que o Geo devolveu para o município conferido; nulo quando não foi conferido ou foi recusado.</summary>
    public string? NomeConferido(string codigoIbge) => Resultado(codigoIbge) is ResultadoDoMunicipioNoGeo.ConferidoNoGeo conferido ? conferido.Nome : null;
}
