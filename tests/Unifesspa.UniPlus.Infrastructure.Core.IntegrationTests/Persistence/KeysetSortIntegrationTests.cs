namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O motor de ordenação keyset contra Postgres real: várias columns na ordem de
/// prioridade, cada uma com o próprio sentido, e a travessia que retoma
/// exatamente de onde parou.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit para classes de teste.")]
public sealed class KeysetSortIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_ordenacao_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "Duas columns ordenam por prioridade: a segunda só decide o empate da primeira")]
    public async Task DuasColunas_SegundaDesempataAPrimeira()
    {
        await using TestDbContext contexto = await ComDadosAsync(
            ("Barros", "b1"), ("Alves", "a2"), ("Alves", "a1"), ("Castro", "c1"));

        OrderedKeysetPage<Linha> pagina = await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(SortDirection.Ascending),
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
        KeysetSort<Linha> descendente = new(
            [.. PorSobrenomeECodigo(SortDirection.Ascending).Columns
                .Select(c => c.With(SortDirection.Descending))],
            (partes, id) => new Linha { Sobrenome = partes[0], Codigo = partes[1], Id = id });

        OrderedKeysetPage<Linha> pagina = await OrderedKeysetCursor.ApplyAsync(
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

        KeysetSort<Linha> sort = PorSobrenomeECodigo(SortDirection.Ascending);

        OrderedKeysetPage<Linha> primeira = await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(), sort, null, null, 2, PaginationDirection.Next);
        primeira.Items.Select(l => l.Codigo).Should().Equal("a1", "a2");
        primeira.Previous.Should().BeNull("é a primeira página");

        OrderedKeysetPage<Linha> segunda = await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            sort,
            primeira.Next!.Value.SortKey,
            primeira.Next!.Value.Id,
            2,
            PaginationDirection.Next);
        segunda.Items.Select(l => l.Codigo).Should().Equal("b1", "c1");

        OrderedKeysetPage<Linha> volta = await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            sort,
            segunda.Previous!.Value.SortKey,
            segunda.Previous!.Value.Id,
            2,
            PaginationDirection.Prev);
        volta.Items.Select(l => l.Codigo).Should().Equal("a1", "a2");
    }

    [Fact(DisplayName = "Chave com número de columns diferente é recusada, e não paginada de um ponto qualquer")]
    public async Task ChaveComOutraLargura_EhRecusada()
    {
        await using TestDbContext contexto = await ComDadosAsync(("Alves", "a1"));

        Func<Task> act = async () => await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(SortDirection.Ascending),
            CompositeSortKey.Serialize("uma", "chave", "de tres columns"),
            Guid.CreateVersion7(),
            10,
            PaginationDirection.Next);

        await act.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Fact(DisplayName = "Chave de outra ordenação da mesma largura é recusada — a contagem não as distingue")]
    public async Task ChaveDeOutraOrdenacaoDaMesmaLargura_EhRecusada()
    {
        await using TestDbContext contexto = await ComDadosAsync(
            ("Alves", "a1"), ("Barros", "b1"), ("Castro", "c1"));

        // Cursor emitido para a ordenação ascendente, reapresentado à descendente:
        // mesmas columns, mesma quantidade, sentido oposto. O seek partiria dos
        // valores certos na direção errada, pulando ou repetindo linhas.
        OrderedKeysetPage<Linha> ascendente = await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(SortDirection.Ascending),
            null,
            null,
            1,
            PaginationDirection.Next);

        (string SortKey, Guid Id) ancora = ascendente.Next!.Value;

        Func<Task> act = async () => await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(SortDirection.Descending),
            ancora.SortKey,
            ancora.Id,
            1,
            PaginationDirection.Next);

        await act.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Fact(DisplayName = "Chave de ordenação por outras columns, na mesma quantidade, também é recusada")]
    public async Task ChaveDeOutrasColunas_EhRecusada()
    {
        await using TestDbContext contexto = await ComDadosAsync(("Alves", "a1"), ("Barros", "b1"));

        OrderedKeysetPage<Linha> porSobrenome = await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            PorSobrenomeECodigo(SortDirection.Ascending),
            null,
            null,
            1,
            PaginationDirection.Next);

        (string SortKey, Guid Id) ancora = porSobrenome.Next!.Value;

        // Duas columns nos dois casos, mas ordenando por outra coisa.
        KeysetSort<Linha> porCodigoESobrenome = new(
            [
                KeysetSortColumn<Linha>.For("codigo", l => l.Codigo, l => l.Codigo),
                KeysetSortColumn<Linha>.For("sobrenome", l => l.Sobrenome, l => l.Sobrenome),
            ],
            (partes, id) => new Linha { Codigo = partes[0], Sobrenome = partes[1], Id = id });

        Func<Task> act = async () => await OrderedKeysetCursor.ApplyAsync(
            contexto.Linhas.AsNoTracking(),
            porCodigoESobrenome,
            ancora.SortKey,
            ancora.Id,
            1,
            PaginationDirection.Next);

        await act.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Fact(DisplayName = "Tokens que contêm o separador não fazem duas ordenações parecerem a mesma")]
    public async Task TokensComSeparador_NaoColidem()
    {
        await using TestDbContext context = await ComDadosAsync(("Alves", "a1"), ("Barros", "b1"));

        // Os dois conjuntos de tokens são diferentes, mas concatená-los com ":" e "|"
        // produziria a mesma string nos dois casos. Se a assinatura fosse montada
        // assim, o cursor de uma ordenação seria aceito pela outra.
        KeysetSort<Linha> primeira = ComTokens("sobrenome", "codigo:asc|extra");
        KeysetSort<Linha> segunda = ComTokens("sobrenome:asc|codigo", "extra");

        OrderedKeysetPage<Linha> pagina = await OrderedKeysetCursor.ApplyAsync(
            context.Linhas.AsNoTracking(), primeira, null, null, 1, PaginationDirection.Next);

        (string SortKey, Guid Id) anchor = pagina.Next!.Value;

        Func<Task> act = async () => await OrderedKeysetCursor.ApplyAsync(
            context.Linhas.AsNoTracking(), segunda, anchor.SortKey, anchor.Id, 1, PaginationDirection.Next);

        await act.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    private static KeysetSort<Linha> ComTokens(string tokenA, string tokenB) => new(
        [
            KeysetSortColumn<Linha>.For(tokenA, l => l.Sobrenome, l => l.Sobrenome),
            KeysetSortColumn<Linha>.For(tokenB, l => l.Codigo, l => l.Codigo),
        ],
        (parts, id) => new Linha { Sobrenome = parts[0], Codigo = parts[1], Id = id });

    private static KeysetSort<Linha> PorSobrenomeECodigo(SortDirection direction) => new(
        [
            KeysetSortColumn<Linha>.For("sobrenome", l => l.Sobrenome, l => l.Sobrenome, direction),
            KeysetSortColumn<Linha>.For("codigo", l => l.Codigo, l => l.Codigo, direction),
        ],
        (partes, id) => new Linha { Sobrenome = partes[0], Codigo = partes[1], Id = id });

    private async Task<TestDbContext> ComDadosAsync(params (string Sobrenome, string Codigo)[] linhas)
    {
        TestDbContext contexto = new(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options);

        await contexto.Database.EnsureCreatedAsync();

        // Ids sorteados fora da ordem das columns: a posição tem de vir das
        // columns de ordenação, não do identificador de desempate.
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
