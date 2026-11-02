namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Dublê de <see cref="IConferenciaEnderecoGeo"/> (issue #1865) para testes que precisam da
/// conferência sem depender da stack local (Geo real) nem de rede. Por padrão, todo CEP e todo
/// município são desconhecidos — "confere" só quando o teste registra o mapeamento esperado,
/// para que um teste nunca passe por engano com um valor que nunca foi, de fato, confirmado.
/// </summary>
public sealed class FakeConferenciaEnderecoGeo : IConferenciaEnderecoGeo
{
    private readonly Dictionary<string, (string CodigoIbge, string Cidade, string Uf, string Origem)> _cepsConhecidos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Nome, string Uf)> _municipiosConhecidos = new(StringComparer.Ordinal);
    private bool _indisponivel;

    /// <summary>
    /// Registra que <paramref name="cep"/> resolve para a cidade/UF dadas — é o que o Geo real devolveria. Sem
    /// <paramref name="cidade"/>, o nome é o do município registrado por <see cref="ComMunicipio"/>, ou o próprio código.
    /// </summary>
    public FakeConferenciaEnderecoGeo ComCep(string cep, string codigoIbge, string uf, string origem = "logradouro", string? cidade = null)
    {
        _cepsConhecidos[cep] = (codigoIbge, cidade ?? (_municipiosConhecidos.TryGetValue(codigoIbge, out (string Nome, string Uf) m) ? m.Nome : codigoIbge), uf, origem);
        return this;
    }

    /// <summary>Registra que o município (código IBGE) tem o nome dado e pertence à UF dada.</summary>
    public FakeConferenciaEnderecoGeo ComMunicipio(string codigoIbge, string uf, string? nome = null)
    {
        _municipiosConhecidos[codigoIbge] = (nome ?? codigoIbge, uf);
        return this;
    }

    /// <summary>Faz toda chamada subsequente devolver falha fechada (Geo indisponível) — simula o Geo fora do ar.</summary>
    public FakeConferenciaEnderecoGeo Indisponivel()
    {
        _indisponivel = true;
        return this;
    }

    public Task<Result<string>> ConferirCepAsync(
        string cep, string cidadeCodigoIbgeDeclarada, string ufDeclarada, CancellationToken cancellationToken = default)
    {
        if (_indisponivel)
        {
            return Task.FromResult(Result<string>.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível (dublê de teste).")));
        }

        if (!_cepsConhecidos.TryGetValue(cep, out (string CodigoIbge, string Cidade, string Uf, string Origem) resolvido))
        {
            return Task.FromResult(Result<string>.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, "CEP sem cobertura (dublê de teste).")));
        }

        bool bate = string.Equals(resolvido.CodigoIbge, cidadeCodigoIbgeDeclarada, StringComparison.Ordinal)
            && string.Equals(resolvido.Uf, ufDeclarada, StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(bate
            ? Result<string>.Success(resolvido.Cidade)
            : Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.CidadeDivergente, "Cidade divergente (dublê de teste).")));
    }

    public Task<Result<CepResolvido>> ResolverCepAsync(string cep, CancellationToken cancellationToken = default)
    {
        if (_indisponivel)
        {
            return Task.FromResult(Result<CepResolvido>.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível (dublê de teste).")));
        }

        return Task.FromResult(_cepsConhecidos.TryGetValue(cep, out (string CodigoIbge, string Cidade, string Uf, string Origem) resolvido)
            ? Result<CepResolvido>.Success(new CepResolvido(resolvido.CodigoIbge, resolvido.Cidade, resolvido.Uf, resolvido.Origem))
            : Result<CepResolvido>.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, "CEP sem cobertura (dublê de teste).")));
    }

    public Task<Result<string>> ConferirMunicipioAsync(
        string cidadeCodigoIbgeDeclarado, string ufDeclarada, CancellationToken cancellationToken = default)
    {
        if (_indisponivel)
        {
            return Task.FromResult(Result<string>.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível (dublê de teste).")));
        }

        if (!_municipiosConhecidos.TryGetValue(cidadeCodigoIbgeDeclarado, out (string Nome, string Uf) municipio))
        {
            return Task.FromResult(Result<string>.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "Município inexistente (dublê de teste).")));
        }

        return Task.FromResult(string.Equals(municipio.Uf, ufDeclarada, StringComparison.OrdinalIgnoreCase)
            ? Result<string>.Success(municipio.Nome)
            : Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente, "Município fora da UF declarada (dublê de teste).")));
    }
}
