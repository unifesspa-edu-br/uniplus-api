namespace Unifesspa.UniPlus.Geo.IntegrationTests.Cep;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Infrastructure.Caching;
using Unifesspa.UniPlus.Geo.Infrastructure.Cep;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// Testes de unidade do cache-aside do <see cref="CepResolver"/> (#676) — sem
/// Redis/DB reais (substitutos NSubstitute). Provam o contrato que a integração não
/// consegue afirmar com precisão: hit não toca o reader (CA-07), 404 nunca é
/// cacheado (CA-08), a chave compõe-se com o selo de versão, e o lookup degrada
/// para o banco quando o Redis está fora.
/// </summary>
public sealed class CepResolverTests
{
    private const string Selo = "202601";
    private const string Cep = "01001000";
    private const string ChaveCep = $"geo:cep:v{Selo}:{Cep}";

    private static CepResolvidoDto Endereco() => new(
        Cep, "Praça", "Praça da Sé", null, "Sé", null, "São Paulo", "3550308", "SP",
        -23.55m, -46.63m, CepResolucao.NivelLogradouro, CepResolucao.OrigemLogradouro);

    private static CepResolver Criar(ICacheService cache, ICepReader reader) =>
        new(new Lazy<ICacheService>(() => cache), reader, Options.Create(new GeoCepCacheOptions()), NullLogger<CepResolver>.Instance);

    [Fact(DisplayName = "CA-07: cache hit devolve do Redis e não consulta o reader (banco)")]
    public async Task CacheHit_NaoConsultaReader()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        ICepReader reader = Substitute.For<ICepReader>();
        CepResolvidoDto cacheado = Endereco();
        cache.ObterAsync<string>(RedisGeoCepCacheInvalidador.ChaveSeloVersaoVigente, Arg.Any<CancellationToken>())
            .Returns(Selo);
        cache.ObterAsync<CepResolvidoDto>(ChaveCep, Arg.Any<CancellationToken>())
            .Returns(cacheado);

        CepResolvidoDto? resultado = await Criar(cache, reader).ResolverAsync(Cep, CancellationToken.None);

        resultado.Should().Be(cacheado);
        await reader.DidNotReceive().ResolverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cache miss: resolve do banco e popula a chave versionada com o TTL")]
    public async Task CacheMiss_ResolveDoBancoEPopula()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        ICepReader reader = Substitute.For<ICepReader>();
        CepResolvidoDto resolvido = Endereco();
        cache.ObterAsync<string>(RedisGeoCepCacheInvalidador.ChaveSeloVersaoVigente, Arg.Any<CancellationToken>())
            .Returns(Selo);
        cache.ObterAsync<CepResolvidoDto>(ChaveCep, Arg.Any<CancellationToken>())
            .Returns((CepResolvidoDto?)null);
        reader.ResolverAsync(Cep, Arg.Any<CancellationToken>()).Returns(resolvido);

        CepResolvidoDto? resultado = await Criar(cache, reader).ResolverAsync(Cep, CancellationToken.None);

        resultado.Should().Be(resolvido);
        await reader.Received(1).ResolverAsync(Cep, Arg.Any<CancellationToken>());
        await cache.Received(1).DefinirAsync(ChaveCep, resolvido, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-08: reader devolve null (404) — nada é gravado no cache")]
    public async Task ReaderNull_NaoCacheia()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        ICepReader reader = Substitute.For<ICepReader>();
        cache.ObterAsync<string>(RedisGeoCepCacheInvalidador.ChaveSeloVersaoVigente, Arg.Any<CancellationToken>())
            .Returns(Selo);
        cache.ObterAsync<CepResolvidoDto>(ChaveCep, Arg.Any<CancellationToken>())
            .Returns((CepResolvidoDto?)null);
        reader.ResolverAsync(Cep, Arg.Any<CancellationToken>()).Returns((CepResolvidoDto?)null);

        CepResolvidoDto? resultado = await Criar(cache, reader).ResolverAsync(Cep, CancellationToken.None);

        resultado.Should().BeNull();
        await cache.DidNotReceive().DefinirAsync(Arg.Any<string>(), Arg.Any<CepResolvidoDto>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Redis indisponível (Lazy lança ao conectar): degrada para o banco sem propagar erro")]
    public async Task CacheIndisponivel_DegradaParaBanco()
    {
        ICepReader reader = Substitute.For<ICepReader>();
        CepResolvidoDto resolvido = Endereco();
        reader.ResolverAsync(Cep, Arg.Any<CancellationToken>()).Returns(resolvido);

        // Lazy que lança ao resolver — simula o IConnectionMultiplexer.Connect falhando.
        Lazy<ICacheService> cacheQuebrado = new(() => throw new InvalidOperationException("Redis fora do ar"));
        CepResolver resolver = new(cacheQuebrado, reader, Options.Create(new GeoCepCacheOptions()), NullLogger<CepResolver>.Instance);

        CepResolvidoDto? resultado = await resolver.ResolverAsync(Cep, CancellationToken.None);

        resultado.Should().Be(resolvido);
        await reader.Received(1).ResolverAsync(Cep, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem selo de versão (ETL ainda não selou): resolve do banco e não cacheia")]
    public async Task SemSelo_NaoCacheia()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        ICepReader reader = Substitute.For<ICepReader>();
        cache.ObterAsync<string>(RedisGeoCepCacheInvalidador.ChaveSeloVersaoVigente, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        CepResolvidoDto resolvido = Endereco();
        reader.ResolverAsync(Cep, Arg.Any<CancellationToken>()).Returns(resolvido);

        CepResolvidoDto? resultado = await Criar(cache, reader).ResolverAsync(Cep, CancellationToken.None);

        resultado.Should().Be(resolvido);
        await reader.Received(1).ResolverAsync(Cep, Arg.Any<CancellationToken>());
        await cache.DidNotReceive().ObterAsync<CepResolvidoDto>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().DefinirAsync(Arg.Any<string>(), Arg.Any<CepResolvidoDto>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }
}
