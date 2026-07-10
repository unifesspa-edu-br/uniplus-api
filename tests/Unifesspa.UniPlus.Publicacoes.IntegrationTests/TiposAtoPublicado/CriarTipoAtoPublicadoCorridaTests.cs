namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Prova, contra Postgres real, que a corrida check-then-act vira erro de domínio e
/// não uma exceção que escaparia como HTTP 500.
/// </summary>
/// <remarks>
/// <para>A tradução depende de o Npgsql preencher <c>ConstraintName</c> num
/// <c>23P01</c>. Um teste com repositório dublê jamais veria isso: ele passaria
/// mesmo que o nome viesse nulo e o <c>catch</c> nunca casasse.</para>
/// <para>A corrida real é simulada suprimindo a consulta prévia do handler — o que
/// é exatamente o que uma transação concorrente faz: torna obsoleta a resposta que
/// a consulta deu um instante antes.</para>
/// </remarks>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CriarTipoAtoPublicadoCorridaTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly PublicacoesDbFixture _fixture;

    public CriarTipoAtoPublicadoCorridaTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A violação da exclusion constraint vira VigenciaSobreposta, não exceção")]
    public async Task Handle_QuandoAConsultaPreviaFicaObsoleta_TraduzOErroDoBanco()
    {
        string codigo = CodigoUnico();

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext("admin");
        var real = new TipoAtoPublicadoRepository(ctx, TimeProvider.System);

        // Primeira versão, gravada normalmente.
        Result<Guid> primeira = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(codigo), real, ctx, CancellationToken.None);
        primeira.IsSuccess.Should().BeTrue();

        // Segunda versão, com a consulta prévia cega — o estado do banco mudou sob ela.
        await using PublicacoesDbContext ctx2 = _fixture.CreateDbContext("admin");
        var cego = new RepositorioComConsultaObsoleta(new TipoAtoPublicadoRepository(ctx2, TimeProvider.System));

        Result<Guid> segunda = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(codigo), cego, ctx2, CancellationToken.None);

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);
    }

    [Fact(DisplayName = "Sem corrida, a consulta prévia recusa antes de tocar o banco")]
    public async Task Handle_ComSobreposicaoConhecida_RecusaPelaConsultaPrevia()
    {
        string codigo = CodigoUnico();

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext("admin");
        var repositorio = new TipoAtoPublicadoRepository(ctx, TimeProvider.System);

        (await CriarTipoAtoPublicadoCommandHandler.Handle(Comando(codigo), repositorio, ctx, CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        await using PublicacoesDbContext ctx2 = _fixture.CreateDbContext("admin");
        Result<Guid> segunda = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(codigo), new TipoAtoPublicadoRepository(ctx2, TimeProvider.System), ctx2, CancellationToken.None);

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);
    }

    private static CriarTipoAtoPublicadoCommand Comando(string codigo) =>
        new(codigo, "Tipo de ato de teste", true, false, false, Inicio);

    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "CORRIDA_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }

    /// <summary>
    /// Delega tudo ao repositório real, exceto a consulta de sobreposição, que
    /// responde "não há" — como responderia se outra transação tivesse gravado a
    /// versão conflitante logo depois da consulta.
    /// </summary>
    private sealed class RepositorioComConsultaObsoleta : ITipoAtoPublicadoRepository
    {
        private readonly ITipoAtoPublicadoRepository _real;

        public RepositorioComConsultaObsoleta(ITipoAtoPublicadoRepository real) => _real = real;

        public Task<bool> ExisteSobreposicaoDeVigenciaAsync(
            string codigo, DateOnly vigenciaInicio, DateOnly? vigenciaFim, Guid? excluirId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<TipoAtoPublicado?> ObterPorIdAsync(Guid id, CancellationToken ct) => _real.ObterPorIdAsync(id, ct);

        public Task<TipoAtoPublicado?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken ct) =>
            _real.ObterPorIdParaLeituraAsync(id, ct);

        public Task<TipoAtoPublicado?> ObterVigenteAsync(string codigo, DateOnly data, CancellationToken ct) =>
            _real.ObterVigenteAsync(codigo, data, ct);

        public Task<(IReadOnlyList<TipoAtoPublicado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, bool vigentes, CancellationToken ct) =>
            _real.ListarPaginadoAsync(afterId, limit, direction, vigentes, ct);

        public Task AdicionarAsync(TipoAtoPublicado tipo, CancellationToken ct) => _real.AdicionarAsync(tipo, ct);

        public void Remover(TipoAtoPublicado tipo) => _real.Remover(tipo);
    }
}
