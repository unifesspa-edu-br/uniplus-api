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
    /// Resolve <paramref name="cep"/> no Geo (8 dígitos): a cidade, a UF e até onde a resolução chegou.
    /// O nível vem do Geo, nunca do que o navegador declarou. Sem cobertura:
    /// <see cref="ConferenciaEnderecoGeoErrorCodes.CepNaoResolve"/>; Geo fora do ar:
    /// <see cref="ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel"/> — nunca confundir os dois.
    /// </summary>
    Task<Result<CepResolvido>> ResolverCepAsync(string cep, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confere que o município (sem CEP — ex.: naturalidade) pertence à UF declarada.
    /// </summary>
    Task<Result> ConferirMunicipioAsync(
        string cidadeCodigoIbgeDeclarado, string ufDeclarada, CancellationToken cancellationToken = default);
}

/// <summary>O que o Geo resolveu para um CEP.</summary>
/// <param name="Origem">
/// A estratégia que resolveu o CEP, como o Geo a declara: <c>logradouro</c>, <c>grande-usuario</c> e
/// <c>cep-geral</c> são resolução exata; qualquer outra (as de faixa, em que o número não corresponde a
/// um CEP cadastrado, ou uma desconhecida) exige a confirmação do candidato.
/// </param>
public sealed record CepResolvido(string CodigoIbge, string Uf, string Origem)
{
    public const string OrigemLogradouro = "logradouro";
    public const string OrigemGrandeUsuario = "grande-usuario";
    public const string OrigemCepGeral = "cep-geral";

    /// <summary>
    /// As únicas origens que o CEP cadastrado tem: aceitas sem confirmação. É lista fechada — qualquer
    /// outra, inclusive uma que a API não conhece, exige a confirmação do candidato.
    /// </summary>
    private static readonly string[] Exatas = [OrigemLogradouro, OrigemGrandeUsuario, OrigemCepGeral];

    /// <summary>O CEP está cadastrado: de logradouro, de grande usuário ou o geral oficial de cidade ou distrito.</summary>
    public bool EhExata => Exatas.Contains(Origem, StringComparer.Ordinal);

    /// <summary>
    /// A origem não é exata (hoje, as de faixa): o número pode ser erro de digitação, e o candidato precisa
    /// confirmá-lo.
    /// </summary>
    public bool ExigeConfirmacao => !EhExata;

    /// <summary>
    /// Logradouro e bairro não saem do CEP: o candidato os informa. Só no CEP geral e no CEP confirmado
    /// sem ser exato — o de grande usuário chega sem bairro, e o de logradouro tem os dois.
    /// </summary>
    public bool ExigeLogradouroEBairro => ExigeConfirmacao || string.Equals(Origem, OrigemCepGeral, StringComparison.Ordinal);
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
