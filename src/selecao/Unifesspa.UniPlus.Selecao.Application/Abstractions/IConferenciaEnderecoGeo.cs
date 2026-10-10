namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Kernel.Results;

/// <summary>
/// Confere, no servidor, que o CEP e o município informados pelo candidato existem e batem a UF
/// declarada — contra o Geo, nunca aceitos como retrato do navegador (ADR-0136, issue #1865).
/// Falha fechada: Geo indisponível é erro TEMPORÁRIO (<see cref="ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel"/>),
/// nunca "aceito" — quem chama decide se isso bloqueia a submissão ou permite salvar o rascunho.
/// </summary>
public interface IConferenciaEnderecoGeo
{
    /// <summary>
    /// Confere que <paramref name="cep"/> resolve a uma cidade cujo código IBGE e UF batem os
    /// declarados pelo candidato.
    /// </summary>
    Task<Result> ConferirCepAsync(
        string cep, string cidadeCodigoIbgeDeclarada, string ufDeclarada, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confere que o município (sem CEP — ex.: naturalidade) pertence à UF declarada.
    /// </summary>
    Task<Result> ConferirMunicipioAsync(
        string cidadeCodigoIbgeDeclarado, string ufDeclarada, CancellationToken cancellationToken = default);
}

/// <summary>Códigos de erro da conferência de endereço no Geo (issue #1865).</summary>
public static class ConferenciaEnderecoGeoErrorCodes
{
    /// <summary>O CEP é bem-formado, mas o Geo não tem cobertura para ele.</summary>
    public const string CepNaoResolve = "ConferenciaEnderecoGeo.CepNaoResolve";

    /// <summary>O CEP resolve, mas para uma cidade ou UF diferente da declarada.</summary>
    public const string CidadeDivergente = "ConferenciaEnderecoGeo.CidadeDivergente";

    /// <summary>O código IBGE do município não existe no Geo.</summary>
    public const string MunicipioNaoEncontrado = "ConferenciaEnderecoGeo.MunicipioNaoEncontrado";

    /// <summary>O município existe, mas a UF declarada não é a dele.</summary>
    public const string MunicipioDivergente = "ConferenciaEnderecoGeo.MunicipioDivergente";

    /// <summary>
    /// Falha fechada: o Geo não respondeu (timeout, rede, 5xx) — erro TEMPORÁRIO, nunca "aceito".
    /// Quem chama recusa a submissão, mas o rascunho continua salvável.
    /// </summary>
    public const string GeoIndisponivel = "ConferenciaEnderecoGeo.GeoIndisponivel";
}
