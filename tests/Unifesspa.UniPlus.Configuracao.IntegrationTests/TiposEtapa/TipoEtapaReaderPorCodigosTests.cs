namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A leitura em lote de tipos de etapa ativos por código (ADR-0056), contra Postgres real. É ela que diz ao documento
/// da configuração quais tipos de etapa citados existem: um tipo desativado que continuasse respondendo faria a etapa
/// ser adotada com um tipo que o cadastro não aceita mais. Os tipos vêm da carga do cadastro; o teste que precisa de um
/// tipo desativado o desativa dentro de uma transação desfeita no fim, para não contaminar a coleção.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TipoEtapaReaderPorCodigosTests
{
    private const string ProvaObjetiva = "PROVA_OBJETIVA";
    private const string Redacao = "REDACAO";
    private const string Entrevista = "ENTREVISTA";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoEtapaReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ListarAtivosPorCodigosAsync devolve só os tipos ativos pedidos, ordenados por código")]
    public async Task ListarAtivosPorCodigosAsync_DevolveSoOsPedidosOrdenados()
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<TipoEtapaView> tipos = await ListarAsync(leitura, Redacao, ProvaObjetiva);

        tipos.Select(tipo => tipo.Codigo).Should().Equal(ProvaObjetiva, Redacao);
        tipos.Should().OnlyContain(tipo => tipo.Id != Guid.Empty && tipo.Nome.Length > 0);
    }

    [Fact(DisplayName = "ListarAtivosPorCodigosAsync traz a avaliação e os conceitos admitidos de cada tipo")]
    public async Task ListarAtivosPorCodigosAsync_TrazAAvaliacaoDoTipo()
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<TipoEtapaView> tipos = await ListarAsync(leitura, Redacao);

        TipoEtapaView redacao = tipos.Should().ContainSingle().Which;
        redacao.Avaliacao.ExigeCorrecaoIndependente.Should().BeTrue();
        redacao.Avaliacao.ConceitosAdmitidos.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "ListarAtivosPorCodigosAsync não devolve código sem tipo cadastrado nem escrito de outra forma")]
    public async Task ListarAtivosPorCodigosAsync_CodigoSemTipo_NaoVolta()
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<TipoEtapaView> tipos =
            await ListarAsync(leitura, ProvaObjetiva, "ETAPA_QUE_NAO_EXISTE", "prova_objetiva", $" {ProvaObjetiva} ", string.Empty);

        tipos.Select(tipo => tipo.Codigo).Should().Equal(ProvaObjetiva);
    }

    [Fact(DisplayName = "ListarAtivosPorCodigosAsync não devolve tipo de etapa desativado")]
    public async Task ListarAtivosPorCodigosAsync_TipoDesativado_NaoVolta()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ctx.Database.ExecuteSqlRawAsync("UPDATE configuracao.tipos_etapa SET ativo = false WHERE codigo = 'ENTREVISTA'");

        IReadOnlyList<TipoEtapaView> tipos = await ListarAsync(ctx, ProvaObjetiva, Entrevista);

        tipos.Select(tipo => tipo.Codigo).Should().Equal(ProvaObjetiva);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "ListarAtivosPorCodigosAsync com códigos repetidos devolve cada tipo uma só vez")]
    public async Task ListarAtivosPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaTipo()
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<TipoEtapaView> tipos = await ListarAsync(leitura, ProvaObjetiva, Redacao, ProvaObjetiva);

        tipos.Select(tipo => tipo.Codigo).Should().Equal(ProvaObjetiva, Redacao);
    }

    [Fact(DisplayName = "ListarAtivosPorCodigosAsync com lote vazio não devolve tipo")]
    public async Task ListarAtivosPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<TipoEtapaView> tipos = await ListarAsync(leitura);

        tipos.Should().BeEmpty();
    }

    private static Task<IReadOnlyList<TipoEtapaView>> ListarAsync(UniPlusDbContext contexto, params string[] codigos) =>
        new TipoEtapaReader(contexto).ListarAtivosPorCodigosAsync(codigos);
}
