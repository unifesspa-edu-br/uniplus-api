namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
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
        await GravarPrimeiraVersaoAsync(codigo);

        await using UniPlusDbContext contexto = _fixture.CreateDbContext("admin");
        var cego = new RepositorioComConsultaObsoleta(new TipoAtoPublicadoRepository(contexto));

        Result<Guid> segunda = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(codigo), cego, contexto, CancellationToken.None);

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);
    }

    /// <summary>
    /// Reproduz o achado da ADR-0119 para a exclusion constraint de vigência de Publicações: o
    /// outbox do Wolverine (AutoApplyTransactions, ADR-0004) chama <c>SaveChangesAsync</c> no mesmo
    /// <c>DbContext</c> depois que o handler retorna. Se o handler capturar a violação e devolver a
    /// falha sem descartar o rastreamento da entidade <c>Added</c> da tentativa fracassada, essa
    /// segunda chamada tenta gravar a mesma entidade de novo e a mesma exceção estoura fora do
    /// <c>catch</c> do handler — 500 em vez do erro de domínio já traduzido.
    /// </summary>
    [Fact(DisplayName =
        "Após o conflito capturado, descartar o rastreamento evita a segunda exceção do SaveChanges do outbox")]
    public async Task Handle_AposConflito_DescartarRastreamentoEvitaSegundaExcecao()
    {
        string codigo = CodigoUnico();
        await GravarPrimeiraVersaoAsync(codigo);

        await using UniPlusDbContext contexto = _fixture.CreateDbContext("admin");
        var cego = new RepositorioComConsultaObsoleta(new TipoAtoPublicadoRepository(contexto));
        Result<Guid> segunda = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(codigo), cego, contexto, CancellationToken.None);
        segunda.IsFailure.Should().BeTrue();

        Func<Task> segundoSave = async () => await contexto.SaveChangesAsync();

        await segundoSave.Should().NotThrowAsync("o handler já descartou o rastreamento da tentativa fracassada");
    }

    [Fact(DisplayName = "Sem corrida, a consulta prévia recusa antes de tocar o banco")]
    public async Task Handle_ComSobreposicaoConhecida_RecusaPelaConsultaPrevia()
    {
        string codigo = CodigoUnico();
        await GravarPrimeiraVersaoAsync(codigo);

        await using UniPlusDbContext contexto = _fixture.CreateDbContext("admin");
        Result<Guid> segunda = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(codigo), new TipoAtoPublicadoRepository(contexto), contexto, CancellationToken.None);

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);
    }

    private async Task GravarPrimeiraVersaoAsync(string codigo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext("admin");

        Result<Guid> primeira = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(codigo), new TipoAtoPublicadoRepository(contexto), contexto, CancellationToken.None);

        primeira.IsSuccess.Should().BeTrue();
    }

    private static CriarTipoAtoPublicadoCommand Comando(string codigo) =>
        new(codigo, "Tipo de ato de teste", true, false, false, false, false, false, Inicio);

    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "CORRIDA_" + string.Concat(hex.Select(digito => (char)('A' + Convert.ToInt32(digito.ToString(), 16))));
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
            string codigo,
            DateOnly vigenciaInicio,
            DateOnly? vigenciaFim,
            Guid? excluirId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<TipoAtoPublicado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
            _real.ObterPorIdAsync(id, cancellationToken);

        public Task<TipoAtoPublicado?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
            _real.ObterPorIdParaLeituraAsync(id, cancellationToken);

        public Task<TipoAtoPublicado?> ObterVigenteAsync(string codigo, DateOnly data, CancellationToken cancellationToken) =>
            _real.ObterVigenteAsync(codigo, data, cancellationToken);

        public Task<(IReadOnlyList<TipoAtoPublicado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            DateOnly? vigentesEm,
            CancellationToken cancellationToken) =>
            _real.ListarPaginadoAsync(afterId, limit, direction, vigentesEm, cancellationToken);

        public Task AdicionarAsync(TipoAtoPublicado tipo, CancellationToken cancellationToken) =>
            _real.AdicionarAsync(tipo, cancellationToken);

        public void Remover(TipoAtoPublicado tipo) => _real.Remover(tipo);
    }
}
