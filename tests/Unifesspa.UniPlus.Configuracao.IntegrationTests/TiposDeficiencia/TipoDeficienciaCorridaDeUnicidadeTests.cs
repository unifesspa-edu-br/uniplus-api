namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia.DadosDeTipoDeficiencia;

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
        await SemearAsync(codigo, NomeDePersistenciaUnico());

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Admin);
        Result<Guid> resultado = await CriarEmCorridaAsync(ctx, codigo, NomeDePersistenciaUnico());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoJaExiste);
        await SaveChangesPosterior(ctx).Should().NotThrowAsync(
            "o handler descartou o rastreamento antes de devolver a falha");
    }

    [Fact(DisplayName = "Criar em corrida no nome devolve NomeJaExiste (409), não CodigoJaExiste")]
    public async Task Criar_CorridaNoNome_TraduzParaNomeJaExiste()
    {
        string nome = NomeDePersistenciaUnico();
        await SemearAsync(CodigoUnico(), nome);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Admin);
        Result<Guid> resultado = await CriarEmCorridaAsync(ctx, CodigoUnico(), nome);

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
        await SemearAsync(codigoOcupado, NomeDePersistenciaUnico());

        string nomeAlvo = NomeDePersistenciaUnico();
        Guid alvoId = await SemearAsync(CodigoUnico(), nomeAlvo);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Admin);
        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            new AtualizarTipoDeficienciaCommand(alvoId, codigoOcupado, nomeAlvo, Descricao),
            new RepositorioComUnicidadeCega(ctx), ctx, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoJaExiste);
        await SaveChangesPosterior(ctx).Should().NotThrowAsync();
    }

    private static Task<Result<Guid>> CriarEmCorridaAsync(UniPlusDbContext ctx, string codigo, string nome) =>
        CriarTipoDeficienciaCommandHandler.Handle(
            new CriarTipoDeficienciaCommand(codigo, nome, Descricao),
            new RepositorioComUnicidadeCega(ctx), ctx, CancellationToken.None);

    private async Task<Guid> SemearAsync(string codigo, string nome)
    {
        TipoDeficiencia tipo = TipoDeficiencia.Criar(codigo, nome, Descricao).Value!;

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Admin);
        ctx.TiposDeficiencia.Add(tipo);
        await ctx.SaveChangesAsync();

        return tipo.Id;
    }

    private static Func<Task> SaveChangesPosterior(UniPlusDbContext ctx) =>
        async () => await ctx.SaveChangesAsync();

    /// <summary>
    /// Repositório real, exceto pelas consultas de unicidade: ambas declaram o
    /// valor livre, reproduzindo a janela entre a checagem do handler e o INSERT
    /// concorrente que o índice único parcial rejeita.
    /// </summary>
    private sealed class RepositorioComUnicidadeCega : ITipoDeficienciaRepository
    {
        private readonly TipoDeficienciaRepository _interno;

        public RepositorioComUnicidadeCega(UniPlusDbContext dbContext)
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

        public Task<IReadOnlyList<TipoDeficiencia>> ListarVivosParaLeituraAsync(CancellationToken cancellationToken) =>
            _interno.ListarVivosParaLeituraAsync(cancellationToken);

        public Task<(IReadOnlyList<TipoDeficiencia> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
            string? afterSortKey, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken) =>
            _interno.ListarPaginadoAsync(afterSortKey, afterId, limit, direction, cancellationToken);

        public Task AdicionarAsync(TipoDeficiencia tipo, CancellationToken cancellationToken) =>
            _interno.AdicionarAsync(tipo, cancellationToken);

        public void Remover(TipoDeficiencia tipo) => _interno.Remover(tipo);
    }
}
