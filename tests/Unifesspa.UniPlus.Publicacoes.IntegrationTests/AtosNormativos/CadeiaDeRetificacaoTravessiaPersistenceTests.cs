namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A travessia da cadeia de retificação contra Postgres real, por resultado: a raiz e a linhagem inteira
/// de uma cadeia de profundidade quatro, de um ato isolado e de um ciclo que escapou da escrita.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
public sealed class CadeiaDeRetificacaoTravessiaPersistenceTests
{
    private static readonly AtoNormativoDeTeste Base = new();
    /// <summary>Dois atos que se retificam um ao outro: {0} e {1} são os ids, {2} a data do ato e {3} o hash do documento.</summary>
    private const string CicloDeDoisAtos = """
        INSERT INTO publicacoes.atos_normativos
            (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
             unico_por_objeto, data_do_ato, documento_hash, assinante, publicado_em,
             ato_retificado_id, motivo_retificacao)
        VALUES
            ({0}, 'CEPS', 'EDITAL', 2026, '13', 'EDITAL_ABERTURA', false, false,
             false, {2}, {3}, 'Assinante', now(), {1}, 'corrige'),
            ({1}, 'CEPS', 'EDITAL', 2026, '13', 'EDITAL_ABERTURA', false, false,
             false, {2}, {3}, 'Assinante', now(), {0}, 'corrige')
        """;

    private static readonly TimeSpan LimiteDaConsulta = TimeSpan.FromSeconds(30);

    private readonly PublicacoesDbFixture _fixture;
    private readonly AtoNormativoPersistenciaApoio _apoio;

    public CadeiaDeRetificacaoTravessiaPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoPersistenciaApoio(_fixture);
    }

    [Theory(DisplayName = "ObterRaizDaCadeia devolve a raiz a partir de cada elo de uma cadeia de profundidade 4")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ObterRaizDaCadeia_PartindoDeCadaElo_DevolveARaiz(int elo)
    {
        AtoNormativo[] cadeia = await GravarCadeiaDeQuatroAsync();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Guid raiz = await new AtoNormativoRepository(ctx).ObterRaizDaCadeiaAsync(cadeia[elo].Id, default);

        raiz.Should().Be(cadeia[0].Id);
    }

    [Fact(DisplayName = "ObterRaizDaCadeia de um ato isolado devolve o próprio ato")]
    public async Task ObterRaizDaCadeia_AtoIsolado_DevolveOProprio()
    {
        AtoNormativo isolado = Base.Registrar();
        await _apoio.GravarAsync(isolado);
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Guid raiz = await new AtoNormativoRepository(ctx).ObterRaizDaCadeiaAsync(isolado.Id, default);

        raiz.Should().Be(isolado.Id);
    }

    [Theory(DisplayName = "ListarIdsDaCadeia devolve os quatro elos a partir da raiz, do meio e da cabeça")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ListarIdsDaCadeia_PartindoDeCadaElo_DevolveALinhagemInteira(int elo)
    {
        AtoNormativo[] cadeia = await GravarCadeiaDeQuatroAsync();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<Guid> ids = await new AtoNormativoRepository(ctx).ListarIdsDaCadeiaAsync(cadeia[elo].Id, default);

        ids.Should().BeEquivalentTo(cadeia.Select(a => a.Id));
    }

    /// <remarks>
    /// O gatilho que barra o ciclo é diferido para o fim da transação, e a transação aqui é revertida:
    /// ele nunca dispara, e o ciclo vive só dentro dela. As duas linhas entram num único INSERT porque a
    /// chave estrangeira só é conferida ao fim do comando.
    /// </remarks>
    [Fact(DisplayName = "Num ciclo que escapou da escrita, as duas consultas terminam e a raiz recusa a cadeia sem raiz")]
    public async Task Cadeia_ComCiclo_AsConsultasTerminam()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        (Guid a, Guid b) = await InserirCicloAsync(ctx);
        var repo = new AtoNormativoRepository(ctx);
        using CancellationTokenSource limite = new(LimiteDaConsulta);

        IReadOnlyList<Guid> linhagem = await repo.ListarIdsDaCadeiaAsync(a, limite.Token);
        Func<Task> raiz = () => repo.ObterRaizDaCadeiaAsync(b, limite.Token);

        linhagem.Should().BeEmpty("sem raiz não há de onde descer");
        await raiz.Should().ThrowAsync<InvalidOperationException>();
        await transacao.RollbackAsync();
    }

    private async Task<AtoNormativo[]> GravarCadeiaDeQuatroAsync()
    {
        AtoNormativo raiz = Base.Registrar();
        AtoNormativo segundo = Retificando(raiz, "segunda versão");
        AtoNormativo terceiro = Retificando(segundo, "terceira versão");
        AtoNormativo cabeca = Retificando(terceiro, "quarta versão");
        await _apoio.GravarAsync(raiz, segundo, terceiro, cabeca);

        return [raiz, segundo, terceiro, cabeca];
    }

    private static async Task<(Guid A, Guid B)> InserirCicloAsync(UniPlusDbContext ctx)
    {
        Guid a = Guid.CreateVersion7();
        Guid b = Guid.CreateVersion7();

        await ctx.Database.ExecuteSqlRawAsync(CicloDeDoisAtos, a, b, new DateOnly(2026, 3, 13), AtoNormativoDeTeste.HashValido);

        return (a, b);
    }

    private static AtoNormativo Retificando(AtoNormativo retificado, string motivo) =>
        (Base with { AtoRetificadoId = retificado.Id, MotivoRetificacao = motivo }).Registrar();
}
