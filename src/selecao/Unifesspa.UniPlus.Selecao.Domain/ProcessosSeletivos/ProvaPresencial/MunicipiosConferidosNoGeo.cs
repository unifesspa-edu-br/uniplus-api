namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o Geo disse de um município de aplicação: confirmou e devolveu o nome, recusou com o erro da
/// conferência, ou não foi consultado ou não respondeu. O que não foi conferido nunca vale como aceito
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

    /// <summary>O Geo recusou o município: não existe, ou a UF não é a dele.</summary>
    public static ResultadoDoMunicipioNoGeo Recusado(DomainError erro)
    {
        ArgumentNullException.ThrowIfNull(erro);
        return new RecusadoNoGeo(erro);
    }

    /// <summary>O município não foi conferido: o Geo não respondeu, ou a chamada nem chegou a ser feita.</summary>
    public static ResultadoDoMunicipioNoGeo NaoConferido { get; } = new NaoConferidoNoGeo();

    internal sealed record ConferidoNoGeo(string Nome) : ResultadoDoMunicipioNoGeo;

    internal sealed record RecusadoNoGeo(DomainError Erro) : ResultadoDoMunicipioNoGeo;

    internal sealed record NaoConferidoNoGeo : ResultadoDoMunicipioNoGeo;
}

/// <summary>
/// O resultado da conferência no Geo de cada município de aplicação, pelo código do IBGE: o domínio o
/// recebe já resolvido, sem conhecer a porta nem o Geo. O código sem resultado é um município não conferido.
/// </summary>
public sealed class MunicipiosConferidosNoGeo
{
    private readonly IReadOnlyDictionary<string, ResultadoDoMunicipioNoGeo> _resultados;

    private MunicipiosConferidosNoGeo(IReadOnlyDictionary<string, ResultadoDoMunicipioNoGeo> resultados) => _resultados = resultados;

    /// <summary>Nenhum município conferido.</summary>
    public static MunicipiosConferidosNoGeo Nenhum { get; } = new(new Dictionary<string, ResultadoDoMunicipioNoGeo>());

    public static MunicipiosConferidosNoGeo De(IReadOnlyDictionary<string, ResultadoDoMunicipioNoGeo> resultados)
    {
        ArgumentNullException.ThrowIfNull(resultados);
        return new(new Dictionary<string, ResultadoDoMunicipioNoGeo>(resultados, StringComparer.Ordinal));
    }

    /// <summary>O que o Geo disse do município; não conferido quando não há resultado.</summary>
    public ResultadoDoMunicipioNoGeo Resultado(string codigoIbge) =>
        _resultados.GetValueOrDefault(codigoIbge) ?? ResultadoDoMunicipioNoGeo.NaoConferido;

    /// <summary>O nome que o Geo devolveu para o município conferido; nulo quando não foi conferido ou foi recusado.</summary>
    public string? NomeConferido(string codigoIbge) => Resultado(codigoIbge) is ResultadoDoMunicipioNoGeo.ConferidoNoGeo conferido ? conferido.Nome : null;
}
