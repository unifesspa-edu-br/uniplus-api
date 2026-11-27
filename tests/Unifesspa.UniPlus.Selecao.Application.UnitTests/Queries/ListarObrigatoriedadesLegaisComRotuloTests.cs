namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ListarObrigatoriedadesLegaisComRotuloTests
{
    private static readonly DateOnly DiaEmBelem = new(2026, 6, 15);

    private readonly IObrigatoriedadeLegalRepository _regras = Substitute.For<IObrigatoriedadeLegalRepository>();
    private readonly ITipoProcessoReader _tipos = Substitute.For<ITipoProcessoReader>();

    [Fact]
    public async Task Handle_PaginaComTiposRepetidos_ConsultaCodigosDistintosSemReservado()
    {
        IReadOnlyList<ObrigatoriedadeLegal> pagina = [Regra("TIPO_A"), Regra("TIPO_A"), Regra(TodosOsTiposDeProcesso.Codigo)];
        _regras.ListarPaginadoAsync(null, null, 10, PaginationDirection.Next, new FiltroDasObrigatoriedadesLegais(null, null, DiaEmBelem), default)
            .Returns((pagina, null, null));
        _tipos.ObterNomesPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), default)
            .Returns(new Dictionary<string, string> { ["TIPO_A"] = "Tipo A" });

        Result<ListarObrigatoriedadesLegaisResult> resultado = await ListarAsync(true, new ResolvedorFusoDeTeste(), InstanteEmBelem.Em(2026, 6, 15, 12));

        resultado.Value!.Items.Select(item => item.TipoProcesso.Rotulo).Should().Equal("Tipo A", "Tipo A", TodosOsTiposDeProcesso.Rotulo);
        await _tipos.Received(1).ObterNomesPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 1 && codigos.Contains("TIPO_A")), default);
    }

    [Theory(DisplayName = "Às 21h e às 23h59 de Belém a listagem vigente filtra pelo dia civil local, não pelo dia em UTC")]
    [InlineData(21, 0)]
    [InlineData(23, 59)]
    public async Task Handle_NoFimDoDiaEmBelem_FiltraPeloDiaCivilLocal(int hora, int minuto)
    {
        _regras.ListarPaginadoAsync(null, null, 10, PaginationDirection.Next, Arg.Any<FiltroDasObrigatoriedadesLegais>(), default)
            .Returns((Array.Empty<ObrigatoriedadeLegal>(), null, null));

        Result<ListarObrigatoriedadesLegaisResult> resultado = await ListarAsync(
            true, new ResolvedorFusoDeTeste(), InstanteEmBelem.Em(2026, 6, 15, hora, minuto));

        resultado.IsSuccess.Should().BeTrue();
        await _regras.Received(1).ListarPaginadoAsync(null, null, 10, PaginationDirection.Next, new FiltroDasObrigatoriedadesLegais(null, null, DiaEmBelem), default);
    }

    [Fact(DisplayName = "Sem o filtro de vigência o repositório recebe nenhum dia e o fuso não é consultado")]
    public async Task Handle_SemVigentes_NaoConsultaOFuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        _regras.ListarPaginadoAsync(null, null, 10, PaginationDirection.Next, new FiltroDasObrigatoriedadesLegais(null, null, null), default)
            .Returns((Array.Empty<ObrigatoriedadeLegal>(), null, null));

        Result<ListarObrigatoriedadesLegaisResult> resultado = await ListarAsync(false, fuso, InstanteEmBelem.Em(2026, 6, 15, 23, 59));

        resultado.IsSuccess.Should().BeTrue();
        fuso.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Fuso institucional não reconhecido devolve a recusa nomeada e não lista nada")]
    public async Task Handle_FusoNaoReconhecido_RecusaSemListar()
    {
        Result<ListarObrigatoriedadesLegaisResult> resultado = await ListarAsync(
            true, new ResolvedorFusoIndisponivelDeTeste(), InstanteEmBelem.Em(2026, 6, 15, 21));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FusoInstitucionalNaoReconhecido");
        _regras.ReceivedCalls().Should().BeEmpty();
        _tipos.ReceivedCalls().Should().BeEmpty();
    }

    private Task<Result<ListarObrigatoriedadesLegaisResult>> ListarAsync(
        bool vigentes, IResolvedorFusoInstitucional fuso, DateTimeOffset agora) =>
        ListarObrigatoriedadesLegaisQueryHandler.Handle(
            new(null, null, 10, PaginationDirection.Next, null, null, vigentes), _regras, _tipos, fuso, new RelogioFixo(agora), default);

    private static ObrigatoriedadeLegal Regra(string codigo) => ObrigatoriedadeLegal.Criar(
        codigo, CategoriaObrigatoriedade.Outros, "REGRA", new ConcorrenciaDuplaObrigatoria(),
        new TextoNormativo("Descrição", "Lei", null, null), new DateOnly(2026, 1, 1), null).Value!;
}
