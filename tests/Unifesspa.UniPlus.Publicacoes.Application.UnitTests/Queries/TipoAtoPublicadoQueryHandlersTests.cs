namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Queries;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class TipoAtoPublicadoQueryHandlersTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly ITipoAtoPublicadoRepository _repository = Substitute.For<ITipoAtoPublicadoRepository>();

    [Fact(DisplayName = "ObterPorId projeta em DTO, com os três atributos de consequência")]
    public async Task ObterPorId_Projeta()
    {
        TipoAtoPublicado tipo = Novo("RESULTADO_FINAL", congela: false, irreversivel: true);
        _repository.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        TipoAtoPublicadoDto? dto = await ObterTipoAtoPublicadoPorIdQueryHandler.Handle(
            new ObterTipoAtoPublicadoPorIdQuery(tipo.Id), _repository, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Codigo.Should().Be("RESULTADO_FINAL");
        dto.CongelaConfiguracao.Should().BeFalse();
        dto.EfeitoIrreversivel.Should().BeTrue();
        dto.VigenciaFim.Should().BeNull();
    }

    [Fact(DisplayName = "ObterPorId devolve nulo quando não existe")]
    public async Task ObterPorId_Inexistente_Nulo()
    {
        _repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        TipoAtoPublicadoDto? dto = await ObterTipoAtoPublicadoPorIdQueryHandler.Handle(
            new ObterTipoAtoPublicadoPorIdQuery(Guid.NewGuid()), _repository, CancellationToken.None);

        dto.Should().BeNull();
    }

    [Fact(DisplayName = "Listar repassa cursor, limite e direção, e devolve as âncoras")]
    public async Task Listar_RepassaCursorEAncoras()
    {
        Guid after = Guid.NewGuid();
        Guid prev = Guid.NewGuid();
        Guid next = Guid.NewGuid();
        _repository.ListarPaginadoAsync(after, 25, PaginationDirection.Next, true, Arg.Any<CancellationToken>())
            .Returns((new[] { Novo("AVISO") }, prev, next));

        ListarTiposAtoPublicadoResult resultado = await ListarTiposAtoPublicadoQueryHandler.Handle(
            new ListarTiposAtoPublicadoQuery(after, 25, PaginationDirection.Next),
            _repository, CancellationToken.None);

        resultado.Items.Should().ContainSingle().Which.Codigo.Should().Be("AVISO");
        resultado.AnteriorAfterId.Should().Be(prev);
        resultado.ProximoAfterId.Should().Be(next);
    }

    [Fact(DisplayName = "Listar repassa o filtro de vigência ao repositório")]
    public async Task Listar_RepassaFiltroDeVigencia()
    {
        _repository.ListarPaginadoAsync(
            Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<PaginationDirection>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<TipoAtoPublicado>(), (Guid?)null, (Guid?)null));

        await ListarTiposAtoPublicadoQueryHandler.Handle(
            new ListarTiposAtoPublicadoQuery(null, 25, PaginationDirection.Next, Vigentes: false),
            _repository, CancellationToken.None);

        await _repository.Received(1).ListarPaginadoAsync(
            null, 25, PaginationDirection.Next, false, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Listar filtra por vigentes quando a query não diz o contrário")]
    public async Task Listar_DefaultEhVigentes()
    {
        _repository.ListarPaginadoAsync(
            Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<PaginationDirection>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<TipoAtoPublicado>(), (Guid?)null, (Guid?)null));

        await ListarTiposAtoPublicadoQueryHandler.Handle(
            new ListarTiposAtoPublicadoQuery(null, 25, PaginationDirection.Next),
            _repository, CancellationToken.None);

        await _repository.Received(1).ListarPaginadoAsync(
            null, 25, PaginationDirection.Next, true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ObterVigente sem data usa o relógio injetado, não DateTime.Now")]
    public async Task ObterVigente_SemData_UsaRelogioInjetado()
    {
        var agora = new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.Zero);
        _repository.ObterVigenteAsync("AVISO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Novo("AVISO"));

        await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", null),
            _repository, new RelogioFixo(agora), CancellationToken.None);

        await _repository.Received(1).ObterVigenteAsync(
            "AVISO", new DateOnly(2026, 6, 15), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ObterVigente com data histórica consulta exatamente aquela data")]
    public async Task ObterVigente_ComData_ConsultaADataInformada()
    {
        var data = new DateOnly(2026, 3, 15);
        _repository.ObterVigenteAsync("AVISO", data, Arg.Any<CancellationToken>()).Returns(Novo("AVISO"));

        TipoAtoPublicadoDto? dto = await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", data),
            _repository, new RelogioFixo(DateTimeOffset.UnixEpoch), CancellationToken.None);

        dto.Should().NotBeNull();
        await _repository.Received(1).ObterVigenteAsync("AVISO", data, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ObterVigente devolve nulo quando nenhuma versão vale na data")]
    public async Task ObterVigente_SemVersao_Nulo()
    {
        _repository.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        TipoAtoPublicadoDto? dto = await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", new DateOnly(2020, 1, 1)),
            _repository, new RelogioFixo(DateTimeOffset.UnixEpoch), CancellationToken.None);

        dto.Should().BeNull();
    }

    private static TipoAtoPublicado Novo(string codigo, bool congela = true, bool irreversivel = false) =>
        TipoAtoPublicado.Criar(
            codigo, "Nome do tipo", congela, unicoPorObjeto: false, efeitoIrreversivel: irreversivel,
            vigenciaInicio: Inicio, vigenciaFim: null, baseLegal: null).Value!;
}
