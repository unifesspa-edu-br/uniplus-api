namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O motor de ordenação keyset contra Postgres real: várias colunas na ordem de
/// prioridade, cada uma com o próprio sentido, e a travessia que retoma
/// exatamente de onde parou.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit para classes de teste.")]
public sealed class OrdenacaoKeysetIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_ordenacao_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "Duas colunas ordenam por prioridade: a segunda só decide o empate da primeira")]
    public async Task DuasColunas_SegundaDesempataAPrimeira()
    {
        await using TestDbContext contexto = await ComDadosAsync(
            ("Barros", "b1"), ("Alves", "a2"), ("Alves", "a1"), ("Castro", "c1"));

        KeysetOrdenadoPage<Linha> pagina = await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(DirecaoOrdenacao.Ascendente),
            afterSortKey: null,
            afterId: null,
            limit: 10,
            direction: PaginationDirection.Next);

        pagina.Items.Select(l => l.Codigo).Should().Equal("a1", "a2", "b1", "c1");
    }

    [Fact(DisplayName = "Inverter o sentido de uma coluna já declarada inverte a consulta, e não só o rótulo")]
    public async Task ColunaInvertida_ProduzOrdemDescendente()
    {
        await using TestDbContext contexto = await ComDadosAsync(
            ("Barros", "b1"), ("Alves", "a1"), ("Castro", "c1"));

        // A coluna nasce ascendente e é invertida depois — o caminho que uma
        // ordenação escolhida em tempo de execução percorre ao aplicar o sentido
        // pedido sobre a coluna do catálogo.
        OrdenacaoKeyset<Linha> descendente = new(
            [.. PorSobrenomeECodigo(DirecaoOrdenacao.Ascendente).Colunas
                .Select(c => c.Com(DirecaoOrdenacao.Descendente))],
            (partes, id) => new Linha { Sobrenome = partes[0], Codigo = partes[1], Id = id });

        KeysetOrdenadoPage<Linha> pagina = await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            descendente,
            afterSortKey: null,
            afterId: null,
            limit: 10,
            direction: PaginationDirection.Next);

        pagina.Items.Select(l => l.Codigo).Should().Equal("c1", "b1", "a1");
    }

    [Fact(DisplayName = "A travessia retoma da âncora sem repetir nem pular, e volta pela âncora anterior")]
    public async Task Travessia_RetomaDaAncoraNosDoisSentidos()
    {
        await using TestDbContext contexto = await ComDadosAsync(
            ("Alves", "a1"), ("Alves", "a2"), ("Barros", "b1"), ("Castro", "c1"), ("Dias", "d1"));

        OrdenacaoKeyset<Linha> ordenacao = PorSobrenomeECodigo(DirecaoOrdenacao.Ascendente);

        KeysetOrdenadoPage<Linha> primeira = await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(), ordenacao, null, null, 2, PaginationDirection.Next);
        primeira.Items.Select(l => l.Codigo).Should().Equal("a1", "a2");
        primeira.Anterior.Should().BeNull("é a primeira página");

        KeysetOrdenadoPage<Linha> segunda = await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            ordenacao,
            primeira.Proximo!.Value.SortKey,
            primeira.Proximo!.Value.Id,
            2,
            PaginationDirection.Next);
        segunda.Items.Select(l => l.Codigo).Should().Equal("b1", "c1");

        KeysetOrdenadoPage<Linha> volta = await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            ordenacao,
            segunda.Anterior!.Value.SortKey,
            segunda.Anterior!.Value.Id,
            2,
            PaginationDirection.Prev);
        volta.Items.Select(l => l.Codigo).Should().Equal("a1", "a2");
    }

    [Fact(DisplayName = "Chave com número de colunas diferente é recusada, e não paginada de um ponto qualquer")]
    public async Task ChaveComOutraLargura_EhRecusada()
    {
        await using TestDbContext contexto = await ComDadosAsync(("Alves", "a1"));

        Func<Task> act = async () => await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(DirecaoOrdenacao.Ascendente),
            SortKeyComposta.Serializar("uma", "chave", "de tres colunas"),
            Guid.CreateVersion7(),
            10,
            PaginationDirection.Next);

        await act.Should().ThrowAsync<CursorAncoraInvalidaException>();
    }

    [Fact(DisplayName = "Chave de outra ordenação da mesma largura é recusada — a contagem não as distingue")]
    public async Task ChaveDeOutraOrdenacaoDaMesmaLargura_EhRecusada()
    {
        await using TestDbContext contexto = await ComDadosAsync(
            ("Alves", "a1"), ("Barros", "b1"), ("Castro", "c1"));

        // Cursor emitido para a ordenação ascendente, reapresentado à descendente:
        // mesmas colunas, mesma quantidade, sentido oposto. O seek partiria dos
        // valores certos na direção errada, pulando ou repetindo linhas.
        KeysetOrdenadoPage<Linha> ascendente = await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(DirecaoOrdenacao.Ascendente),
            null,
            null,
            1,
            PaginationDirection.Next);

        (string SortKey, Guid Id) ancora = ascendente.Proximo!.Value;

        Func<Task> act = async () => await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(DirecaoOrdenacao.Descendente),
            ancora.SortKey,
            ancora.Id,
            1,
            PaginationDirection.Next);

        await act.Should().ThrowAsync<CursorAncoraInvalidaException>();
    }

    [Fact(DisplayName = "Chave de ordenação por outras colunas, na mesma quantidade, também é recusada")]
    public async Task ChaveDeOutrasColunas_EhRecusada()
    {
        await using TestDbContext contexto = await ComDadosAsync(("Alves", "a1"), ("Barros", "b1"));

        KeysetOrdenadoPage<Linha> porSobrenome = await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(DirecaoOrdenacao.Ascendente),
            null,
            null,
            1,
            PaginationDirection.Next);

        (string SortKey, Guid Id) ancora = porSobrenome.Proximo!.Value;

        // Duas colunas nos dois casos, mas ordenando por outra coisa.
        OrdenacaoKeyset<Linha> porCodigoESobrenome = new(
            [
                ColunaOrdenacaoKeyset<Linha>.De("codigo", l => l.Codigo, l => l.Codigo),
                ColunaOrdenacaoKeyset<Linha>.De("sobrenome", l => l.Sobrenome, l => l.Sobrenome),
            ],
            (partes, id) => new Linha { Codigo = partes[0], Sobrenome = partes[1], Id = id });

        Func<Task> act = async () => await KeysetOrdenadoCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            porCodigoESobrenome,
            ancora.SortKey,
            ancora.Id,
            1,
            PaginationDirection.Next);

        await act.Should().ThrowAsync<CursorAncoraInvalidaException>();
    }

    private static OrdenacaoKeyset<Linha> PorSobrenomeECodigo(DirecaoOrdenacao direcao) => new(
        [
            ColunaOrdenacaoKeyset<Linha>.De("sobrenome", l => l.Sobrenome, l => l.Sobrenome, direcao),
            ColunaOrdenacaoKeyset<Linha>.De("codigo", l => l.Codigo, l => l.Codigo, direcao),
        ],
        (partes, id) => new Linha { Sobrenome = partes[0], Codigo = partes[1], Id = id });

    private async Task<TestDbContext> ComDadosAsync(params (string Sobrenome, string Codigo)[] linhas)
    {
        TestDbContext contexto = new(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options);

        await contexto.Database.EnsureCreatedAsync();

        // Ids sorteados fora da ordem das colunas: a posição tem de vir das
        // colunas de ordenação, não do identificador de desempate.
        foreach ((string sobrenome, string codigo) in linhas)
        {
            contexto.Linhas.Add(new Linha
            {
                Id = Guid.CreateVersion7(),
                Sobrenome = sobrenome,
                Codigo = codigo,
            });
        }

        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return contexto;
    }

    internal sealed class Linha : IIdentificavel
    {
        public Guid Id { get; init; }

        public string Sobrenome { get; init; } = string.Empty;

        public string Codigo { get; init; } = string.Empty;
    }

    internal sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
            : base(options)
        {
        }

        public DbSet<Linha> Linhas => Set<Linha>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<Linha>(b =>
            {
                b.ToTable("linha");
                b.HasKey(l => l.Id);
                b.Property(l => l.Sobrenome).HasMaxLength(100).IsRequired();
                b.Property(l => l.Codigo).HasMaxLength(50).IsRequired();
            });
        }
    }
}
