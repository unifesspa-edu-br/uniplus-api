namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Exercita os handlers de TipoDeficiencia na corrida check-then-act contra
/// Postgres real: a consulta de unicidade não vê o concorrente, o índice único
/// parcial dispara <c>23505</c> no <c>SaveChangesAsync</c> e o handler precisa
/// traduzir a violação no conflito da constraint efetivamente violada — há dois
/// índices na tabela, e trocar um pelo outro mentiria sobre a causa.
/// </summary>
/// <remarks>
/// A corrida é reproduzida de forma determinística por um repositório que declara
/// o valor livre (<see cref="RepositorioComUnicidadeCega"/>), em vez de depender
/// do entrelaçamento real de duas requisições. Cada teste também confere que um
/// <c>SaveChangesAsync</c> posterior — o que o outbox do Wolverine dispara depois
/// que o handler retorna (ADR-0004) — não relança a exceção já traduzida.
/// </remarks>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDeficienciaCorridaDeUnicidadeTests
{
    private const string Admin = "admin-corrida";
    private const string Descricao = "Descrição de teste";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoDeficienciaCorridaDeUnicidadeTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar em corrida no código devolve CodigoJaExiste (409), não 500")]
    public async Task Criar_CorridaNoCodigo_TraduzParaCodigoJaExiste()
    {
        string codigo = CodigoUnico();
        await SemearAsync(codigo, NomeUnico());

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin);
        var repositorio = new RepositorioComUnicidadeCega(ctx);

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            new CriarTipoDeficienciaCommand(codigo, NomeUnico(), Descricao),
            repositorio,
            ctx,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoJaExiste);
        await SaveChangesPosterior(ctx).Should().NotThrowAsync(
            "o handler descartou o rastreamento antes de devolver a falha");
    }

    [Fact(DisplayName = "Criar em corrida no nome devolve NomeJaExiste (409), não CodigoJaExiste")]
    public async Task Criar_CorridaNoNome_TraduzParaNomeJaExiste()
    {
        string nome = NomeUnico();
        await SemearAsync(CodigoUnico(), nome);

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin);
        var repositorio = new RepositorioComUnicidadeCega(ctx);

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            new CriarTipoDeficienciaCommand(CodigoUnico(), nome, Descricao),
            repositorio,
            ctx,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(
            TipoDeficienciaErrorCodes.NomeJaExiste,
            "a constraint violada foi a do nome — devolver CodigoJaExiste apontaria o campo errado ao operador");
        await SaveChangesPosterior(ctx).Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Atualizar em corrida no código devolve CodigoJaExiste (409), não 500")]
    public async Task Atualizar_CorridaNoCodigo_TraduzParaCodigoJaExiste()
    {
        string codigoOcupado = CodigoUnico();
        await SemearAsync(codigoOcupado, NomeUnico());

        string nomeAlvo = NomeUnico();
        Guid alvoId = await SemearAsync(CodigoUnico(), nomeAlvo);

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin);
        var repositorio = new RepositorioComUnicidadeCega(ctx);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            new AtualizarTipoDeficienciaCommand(alvoId, codigoOcupado, nomeAlvo, Descricao),
            repositorio,
            ctx,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoJaExiste);
        await SaveChangesPosterior(ctx).Should().NotThrowAsync();
    }

    private async Task<Guid> SemearAsync(string codigo, string nome)
    {
        TipoDeficiencia tipo = TipoDeficiencia.Criar(codigo, nome, Descricao).Value!;

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(Admin);
        ctx.TiposDeficiencia.Add(tipo);
        await ctx.SaveChangesAsync();

        return tipo.Id;
    }

    private static Func<Task> SaveChangesPosterior(ConfiguracaoDbContext ctx) =>
        async () => await ctx.SaveChangesAsync();

    private static string CodigoUnico() => $"DEF_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static string NomeUnico() => $"DEF_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    /// <summary>
    /// Repositório real, exceto pelas consultas de unicidade: ambas declaram o
    /// valor livre, reproduzindo a janela entre a checagem do handler e o INSERT
    /// concorrente que o índice único parcial rejeita.
    /// </summary>
    private sealed class RepositorioComUnicidadeCega : ITipoDeficienciaRepository
    {
        private readonly TipoDeficienciaRepository _interno;

        public RepositorioComUnicidadeCega(ConfiguracaoDbContext dbContext)
        {
            _interno = new TipoDeficienciaRepository(dbContext);
        }

        public Task<bool> CodigoExisteEntreVivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> NomeExisteEntreVivosAsync(string nome, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<TipoDeficiencia?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
            _interno.ObterPorIdAsync(id, cancellationToken);

        public Task<TipoDeficiencia?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
            _interno.ObterPorIdParaLeituraAsync(id, cancellationToken);

        public Task<(IReadOnlyList<TipoDeficiencia> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken) =>
            _interno.ListarPaginadoAsync(afterId, limit, direction, cancellationToken);

        public Task AdicionarAsync(TipoDeficiencia tipo, CancellationToken cancellationToken) =>
            _interno.AdicionarAsync(tipo, cancellationToken);

        public void Remover(TipoDeficiencia tipo) => _interno.Remover(tipo);
    }
}
