namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Geo.Infrastructure.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Transições do registro de execução do ETL — Story #674.</summary>
public sealed class GeoImportacaoExecucaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Iniciar com dados válidos cria a execução em andamento")]
    public void Iniciar_Valido_CriaEmAndamento()
    {
        Result<GeoImportacaoExecucao> resultado = GeoImportacaoExecucao.Iniciar("202601", "admin-123", Agora);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Status.Should().Be(StatusImportacao.EmAndamento);
        resultado.Value.VersaoDataset.Should().Be("202601");
        resultado.Value.IniciadoEm.Should().Be(Agora);
    }

    [Theory(DisplayName = "Iniciar com versão fora de AAAAMM falha")]
    [InlineData("20260")]
    [InlineData("202600")]
    [InlineData("202613")]
    [InlineData("abcdef")]
    public void Iniciar_VersaoInvalida_Falha(string versao)
    {
        Result<GeoImportacaoExecucao> resultado = GeoImportacaoExecucao.Iniciar(versao, "admin", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GeoImportacaoErrorCodes.VersaoFormatoInvalido);
    }

    [Fact(DisplayName = "Iniciar com versão nula/vazia falha (entrada de API, não lança)")]
    public void Iniciar_VersaoNula_Falha()
    {
        Result<GeoImportacaoExecucao> resultado = GeoImportacaoExecucao.Iniciar(null!, "admin", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GeoImportacaoErrorCodes.VersaoObrigatoria);
    }

    [Fact(DisplayName = "Iniciar sem disparador falha")]
    public void Iniciar_SemDisparador_Falha()
    {
        Result<GeoImportacaoExecucao> resultado = GeoImportacaoExecucao.Iniciar("202601", "  ", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GeoImportacaoErrorCodes.DisparadoPorObrigatorio);
    }

    [Fact(DisplayName = "Concluir a partir de EmAndamento grava status e relatório")]
    public void Concluir_DeEmAndamento_Sucede()
    {
        GeoImportacaoExecucao execucao = GeoImportacaoExecucao.Iniciar("202601", "admin", Agora).Value!;

        Result resultado = execucao.Concluir(Agora.AddMinutes(5), "{}", "ok");

        resultado.IsSuccess.Should().BeTrue();
        execucao.Status.Should().Be(StatusImportacao.Concluida);
        execucao.ConcluidoEm.Should().Be(Agora.AddMinutes(5));
        execucao.RelatorioJson.Should().Be("{}");
    }

    [Fact(DisplayName = "Concluir duas vezes é rejeitado na segunda (transição inválida)")]
    public void Concluir_Duplicado_Rejeita()
    {
        GeoImportacaoExecucao execucao = GeoImportacaoExecucao.Iniciar("202601", "admin", Agora).Value!;
        execucao.Concluir(Agora, "{}", null);

        Result segunda = execucao.Concluir(Agora, "{}", null);

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be(GeoImportacaoErrorCodes.TransicaoInvalida);
    }

    [Fact(DisplayName = "Falhar a partir de EmAndamento grava status e mensagem")]
    public void Falhar_DeEmAndamento_Sucede()
    {
        GeoImportacaoExecucao execucao = GeoImportacaoExecucao.Iniciar("202601", "admin", Agora).Value!;

        Result resultado = execucao.Falhar(Agora, "erro de infra");

        resultado.IsSuccess.Should().BeTrue();
        execucao.Status.Should().Be(StatusImportacao.Falhou);
        execucao.Mensagem.Should().Be("erro de infra");
    }
}

/// <summary>Invalidação do cache de CEP por selo de versão — Story #674, CA-05.</summary>
public sealed class RedisGeoCepCacheInvalidadorTests
{
    [Fact(DisplayName = "Grava o selo da versão vigente numa única chave (sem varredura, sem tocar outras chaves)")]
    public async Task Invalidar_GravaSeloDaVersao()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        RedisGeoCepCacheInvalidador invalidador = new(cache, NullLogger<RedisGeoCepCacheInvalidador>.Instance);

        await invalidador.InvalidarAsync("202602", CancellationToken.None);

        await cache.Received(1).DefinirAsync(
            RedisGeoCepCacheInvalidador.ChaveSeloVersaoVigente,
            "202602",
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
        await cache.DidNotReceive().RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Falha de cache é best-effort — não propaga (a carga já concluiu)")]
    public async Task Invalidar_FalhaDeCache_NaoPropaga()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        cache.When(c => c.DefinirAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Redis fora do ar"));
        RedisGeoCepCacheInvalidador invalidador = new(cache, NullLogger<RedisGeoCepCacheInvalidador>.Instance);

        Func<Task> acao = () => invalidador.InvalidarAsync("202602", CancellationToken.None);

        await acao.Should().NotThrowAsync();
    }
}
