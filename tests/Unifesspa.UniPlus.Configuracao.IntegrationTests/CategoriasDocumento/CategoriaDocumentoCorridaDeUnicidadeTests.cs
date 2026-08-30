namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CategoriasDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Exercita os handlers de CategoriaDocumento na corrida check-then-act contra
/// Postgres real: a consulta de unicidade não vê a concorrente, o índice único
/// parcial dispara <c>23505</c> no <c>SaveChangesAsync</c> e o handler traduz a
/// violação em <c>CodigoJaExiste</c> (409) em vez de deixar vazar um 500.
/// </summary>
/// <remarks>
/// A corrida é reproduzida de forma determinística por um repositório que declara
/// o código livre (<see cref="RepositorioComUnicidadeCega"/>), em vez de depender
/// do entrelaçamento real de duas requisições. Cada teste também confere que um
/// <c>SaveChangesAsync</c> posterior — o que o outbox do Wolverine dispara depois
/// que o handler retorna (ADR-0004) — não relança a exceção já traduzida, o que
/// só se sustenta porque o handler descartou o rastreamento.
/// </remarks>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CategoriaDocumentoCorridaDeUnicidadeTests
{
    private const string Admin = "admin-corrida";

    private readonly ConfiguracaoDbFixture _fixture;

    public CategoriaDocumentoCorridaDeUnicidadeTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar em corrida no código devolve CodigoJaExiste (409), não 500")]
    public async Task Criar_CorridaNoCodigo_TraduzParaCodigoJaExiste()
    {
        string codigo = CodigoUnico();
        await SemearAsync(codigo);

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin);
        var repositorio = new RepositorioComUnicidadeCega(ctx);

        Result<Guid> resultado = await CriarCategoriaDocumentoCommandHandler.Handle(
            new CriarCategoriaDocumentoCommand(codigo, "Concorrente", null, 0),
            repositorio,
            ctx,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoJaExiste);
        await SaveChangesPosterior(ctx).Should().NotThrowAsync(
            "o handler descartou o rastreamento antes de devolver a falha");
    }

    [Fact(DisplayName = "Atualizar em corrida no código devolve CodigoJaExiste (409), não 500")]
    public async Task Atualizar_CorridaNoCodigo_TraduzParaCodigoJaExiste()
    {
        string codigoOcupado = CodigoUnico();
        await SemearAsync(codigoOcupado);
        Guid alvoId = await SemearAsync(CodigoUnico());

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin);
        var repositorio = new RepositorioComUnicidadeCega(ctx);

        Result resultado = await AtualizarCategoriaDocumentoCommandHandler.Handle(
            new AtualizarCategoriaDocumentoCommand(alvoId, codigoOcupado, "Alvo renomeado", null, 0),
            repositorio,
            ctx,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoJaExiste);
        await SaveChangesPosterior(ctx).Should().NotThrowAsync(
            "o handler descartou o rastreamento antes de devolver a falha");
    }

    private async Task<Guid> SemearAsync(string codigo)
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar(codigo, "Categoria semeada", null, 0).Value!;

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin);
        ctx.CategoriasDocumento.Add(categoria);
        await ctx.SaveChangesAsync();

        return categoria.Id;
    }

    private static Func<Task> SaveChangesPosterior(ConfiguracaoDbContext ctx) =>
        async () => await ctx.SaveChangesAsync();

    private static string CodigoUnico() => $"CAT_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    /// <summary>
    /// Repositório real, exceto pela consulta de unicidade: declara o código livre,
    /// reproduzindo a janela entre a checagem do handler e o INSERT concorrente que
    /// o índice único parcial rejeita.
    /// </summary>
    private sealed class RepositorioComUnicidadeCega : ICategoriaDocumentoRepository
    {
        private readonly CategoriaDocumentoRepository _interno;

        public RepositorioComUnicidadeCega(ConfiguracaoDbContext dbContext)
        {
            _interno = new CategoriaDocumentoRepository(dbContext);
        }

        public Task<bool> CodigoExisteEntreVivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<CategoriaDocumento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
            _interno.ObterPorIdAsync(id, cancellationToken);

        public Task<CategoriaDocumento?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
            _interno.ObterPorIdParaLeituraAsync(id, cancellationToken);

        public Task<IReadOnlyList<CategoriaDocumento>> ListarVivasOrdenadasAsync(CancellationToken cancellationToken) =>
            _interno.ListarVivasOrdenadasAsync(cancellationToken);

        public Task AdicionarAsync(CategoriaDocumento categoria, CancellationToken cancellationToken) =>
            _interno.AdicionarAsync(categoria, cancellationToken);

        public void Remover(CategoriaDocumento categoria) => _interno.Remover(categoria);
    }
}
