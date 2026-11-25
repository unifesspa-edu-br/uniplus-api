namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class TipoAtoPublicadoQueryHandlersTests
{
    private const string FusoNaoReconhecido = "ProcessoSeletivo.FusoInstitucionalNaoReconhecido";

    private static readonly DateOnly Inicio = new(2026, 1, 1);

    /// <summary>22h30 em Belém, que em UTC já é o dia seguinte.</summary>
    private static readonly DateOnly DiaEmBelem = new(2026, 6, 15);

    private readonly ITipoAtoPublicadoRepository _repository = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IResolvedorFusoInstitucional _fuso = new ResolvedorFusoDeTeste();
    private readonly RelogioFixo _relogioDeBelem = new(InstanteEmBelem.Em(2026, 6, 15, 22, 30));

    [Fact(DisplayName = "ObterPorId projeta em DTO, com os quatro atributos de consequência")]
    public async Task ObterPorId_TipoExistente_ProjetaEmDto()
    {
        TipoAtoPublicado tipo = Novo("CONVOCACAO", congela: false, irreversivel: true, ehResultado: false);
        _repository.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result<TipoAtoPublicadoDto> resultado = await ObterTipoAtoPublicadoPorIdQueryHandler.Handle(
            new ObterTipoAtoPublicadoPorIdQuery(tipo.Id), _repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        TipoAtoPublicadoDto? dto = resultado.Value;
        dto.Should().NotBeNull();
        dto!.Codigo.Should().Be("CONVOCACAO");
        dto.CongelaConfiguracao.Should().BeFalse();
        dto.EfeitoIrreversivel.Should().BeTrue();
        dto.EhResultado.Should().BeFalse();
        dto.VigenciaFim.Should().BeNull();
    }

    [Fact(DisplayName = "ObterPorId falha com tipo não encontrado quando não existe")]
    public async Task ObterPorId_Inexistente_FalhaComNaoEncontrado()
    {
        _repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        Result<TipoAtoPublicadoDto> resultado = await ObterTipoAtoPublicadoPorIdQueryHandler.Handle(
            new ObterTipoAtoPublicadoPorIdQuery(Guid.NewGuid()), _repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Listar com vigência repassa cursor, limite, direção e o dia civil de Belém, e devolve as âncoras")]
    public async Task Listar_ComVigencia_RepassaCursorEDiaCivilDeBelemEDevolveAncoras()
    {
        Guid after = Guid.NewGuid();
        Guid prev = Guid.NewGuid();
        Guid next = Guid.NewGuid();
        _repository.ListarPaginadoAsync(after, 25, PaginationDirection.Next, DiaEmBelem, Arg.Any<CancellationToken>())
            .Returns((new[] { Novo("AVISO") }, prev, next));

        Result<ListarTiposAtoPublicadoResult> resultado = await ListarTiposAtoPublicadoQueryHandler.Handle(
            new ListarTiposAtoPublicadoQuery(after, 25, PaginationDirection.Next),
            _repository, _fuso, _relogioDeBelem, CancellationToken.None);

        resultado.Value!.Items.Should().ContainSingle().Which.Codigo.Should().Be("AVISO");
        resultado.Value.AnteriorAfterId.Should().Be(prev);
        resultado.Value.ProximoAfterId.Should().Be(next);
    }

    [Fact(DisplayName = "Listar sem filtro de vigência repassa nulo e não consulta o fuso")]
    public async Task Listar_SemFiltroDeVigencia_RepassaNuloENaoConsultaOFuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        _repository.ListarPaginadoAsync(
            Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<PaginationDirection>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<TipoAtoPublicado>(), (Guid?)null, (Guid?)null));

        await ListarTiposAtoPublicadoQueryHandler.Handle(
            new ListarTiposAtoPublicadoQuery(null, 25, PaginationDirection.Next, Vigentes: false),
            _repository, fuso, _relogioDeBelem, CancellationToken.None);

        await _repository.Received(1).ListarPaginadoAsync(
            null, 25, PaginationDirection.Next, null, Arg.Any<CancellationToken>());
        fuso.DidNotReceive().Resolver();
    }

    [Fact(DisplayName = "Listar com vigência e fuso não reconhecido falha com a recusa do fuso, sem ler o repositório")]
    public async Task Listar_ComVigenciaEFusoNaoReconhecido_FalhaSemLerORepositorio()
    {
        Result<ListarTiposAtoPublicadoResult> resultado = await ListarTiposAtoPublicadoQueryHandler.Handle(
            new ListarTiposAtoPublicadoQuery(null, 25, PaginationDirection.Next),
            _repository, new ResolvedorFusoIndisponivelDeTeste(), _relogioDeBelem, CancellationToken.None);

        resultado.Error!.Code.Should().Be(FusoNaoReconhecido);
        await _repository.DidNotReceiveWithAnyArgs().ListarPaginadoAsync(
            default, default, default, default, CancellationToken.None);
    }

    [Fact(DisplayName = "ObterVigente sem data consulta o dia civil de Belém, e não o dia em UTC")]
    public async Task ObterVigente_SemData_ConsultaODiaCivilDeBelem()
    {
        _repository.ObterVigenteAsync("AVISO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Novo("AVISO"));

        await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", null),
            _repository, _fuso, _relogioDeBelem, CancellationToken.None);

        await _repository.Received(1).ObterVigenteAsync("AVISO", DiaEmBelem, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ObterVigente com data histórica consulta exatamente aquela data")]
    public async Task ObterVigente_ComData_ConsultaADataInformada()
    {
        var data = new DateOnly(2026, 3, 15);
        _repository.ObterVigenteAsync("AVISO", data, Arg.Any<CancellationToken>()).Returns(Novo("AVISO"));

        Result<TipoAtoPublicadoDto> resultado = await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", data),
            _repository, _fuso, _relogioDeBelem, CancellationToken.None);

        resultado.Value.Should().NotBeNull();
        await _repository.Received(1).ObterVigenteAsync("AVISO", data, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ObterVigente com data informada funciona mesmo com o fuso não reconhecido")]
    public async Task ObterVigente_ComDataEFusoNaoReconhecido_ConsultaADataInformada()
    {
        var data = new DateOnly(2026, 3, 15);
        _repository.ObterVigenteAsync("AVISO", data, Arg.Any<CancellationToken>()).Returns(Novo("AVISO"));

        Result<TipoAtoPublicadoDto> resultado = await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", data),
            _repository, new ResolvedorFusoIndisponivelDeTeste(), _relogioDeBelem, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "ObterVigente sem data e com fuso não reconhecido falha com a recusa do fuso, sem ler o repositório")]
    public async Task ObterVigente_SemDataEFusoNaoReconhecido_FalhaSemLerORepositorio()
    {
        Result<TipoAtoPublicadoDto> resultado = await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", null),
            _repository, new ResolvedorFusoIndisponivelDeTeste(), _relogioDeBelem, CancellationToken.None);

        resultado.Error!.Code.Should().Be(FusoNaoReconhecido);
        await _repository.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, CancellationToken.None);
    }

    [Fact(DisplayName = "ObterVigente falha com tipo não encontrado quando nenhuma versão vale na data")]
    public async Task ObterVigente_SemVersao_FalhaComNaoEncontrado()
    {
        _repository.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        Result<TipoAtoPublicadoDto> resultado = await ObterTipoAtoPublicadoVigenteQueryHandler.Handle(
            new ObterTipoAtoPublicadoVigenteQuery("AVISO", new DateOnly(2020, 1, 1)),
            _repository, _fuso, _relogioDeBelem, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NaoEncontrado);
    }

    private static TipoAtoPublicado Novo(
        string codigo, bool congela = true, bool irreversivel = false, bool ehResultado = false) =>
        (new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            Nome = "Nome do tipo",
            CongelaConfiguracao = congela,
            EfeitoIrreversivel = irreversivel,
            EhResultado = ehResultado,
            VigenciaInicio = Inicio
        }).Criar().Value!;
}
