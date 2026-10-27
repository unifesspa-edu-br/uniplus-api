namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Acompanhamento;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AcompanhamentoQueryHandlerTests
{
    [Theory]
    [InlineData(PaginationDirection.Next)]
    [InlineData(PaginationDirection.Prev)]
    public async Task Listar_EncaminhaDirecaoRecebidaAoRepositorio(PaginationDirection direction)
    {
        Guid afterId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ListarPaginadoAsync(afterId, 7, direction, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<ProcessoSeletivo>)Array.Empty<ProcessoSeletivo>(), null, null));

        await ListarProcessosAcompanhamentoQueryHandler.Handle(
            new ListarProcessosAcompanhamentoQuery(afterId, 7, direction), repository, CancellationToken.None);

        await repository.Received(1).ListarPaginadoAsync(afterId, 7, direction, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Listar_RepositorioSemProcessos_RetornaArrayVazioSemAncora()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ListarPaginadoAsync(null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Unifesspa.UniPlus.Selecao.Domain.Entities.ProcessoSeletivo>(), null, null));

        ListarProcessosAcompanhamentoResult result = await ListarProcessosAcompanhamentoQueryHandler.Handle(
            new ListarProcessosAcompanhamentoQuery(null, 20, PaginationDirection.Next),
            repository,
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.AnteriorAfterId.Should().BeNull();
        result.ProximoAfterId.Should().BeNull();
    }

    [Fact]
    public async Task Listar_ProjetaSomenteIdentificacaoENaoIncluiConfiguracao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "Processo fictício",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ListarPaginadoAsync(null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<ProcessoSeletivo>)[processo], null, null));

        ListarProcessosAcompanhamentoResult result = await ListarProcessosAcompanhamentoQueryHandler.Handle(
            new ListarProcessosAcompanhamentoQuery(null, 20, PaginationDirection.Next),
            repository,
            CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Should().Be(
            new ProcessoAcompanhamentoResumoDto(processo.Id, "Processo fictício", StatusProcesso.Rascunho));
    }

    [Fact]
    public async Task ObterPorId_ProcessoAusente_RetornaNulo()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        Guid id = Guid.NewGuid();
        repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((
            Unifesspa.UniPlus.Selecao.Domain.Entities.ProcessoSeletivo?)null);

        ProcessoAcompanhamentoResumoDto? result = await ObterProcessoAcompanhamentoQueryHandler.Handle(
            new ObterProcessoAcompanhamentoQuery(id), repository, CancellationToken.None);

        result.Should().BeNull();
    }
}
