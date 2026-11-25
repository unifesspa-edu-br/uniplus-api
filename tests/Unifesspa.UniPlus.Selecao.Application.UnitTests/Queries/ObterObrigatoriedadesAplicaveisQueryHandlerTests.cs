namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using Unifesspa.UniPlus.Kernel.Domain;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ObterObrigatoriedadesAplicaveisQueryHandlerTests
{
    [Fact(DisplayName = "Handle delega ao ruleset com o tipo do processo e a data explícita")]
    public async Task Handle_DataExplicita_DelegadaAoRepositorio()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DateOnly dataReferencia = new(2026, 6, 15);

        IProcessoSeletivoRepository processoRepository = RepositorioDoProcesso(processo);
        IObrigatoriedadeLegalRepository regraRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        regraRepository.ObterVigentesParaTipoProcessoAsync("INDIGENA_E_QUILOMBOLA", dataReferencia, Arg.Any<CancellationToken>())
            .Returns([NovaRegra(TodosOsTiposDeProcesso.Codigo, "UNIVERSAL"), NovaRegra("INDIGENA_E_QUILOMBOLA", "PSIQ")]);

        IReadOnlyList<ObrigatoriedadeLegalDto> resultado = await Consultar(processo.Id, dataReferencia, processoRepository, regraRepository);

        resultado.Select(static regra => regra.RegraCodigo).Should().BeEquivalentTo(["UNIVERSAL", "PSIQ"]);
        await regraRepository.Received(1).ObterVigentesParaTipoProcessoAsync(
            "INDIGENA_E_QUILOMBOLA", dataReferencia, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle não consulta ruleset quando o processo não existe")]
    public async Task Handle_ProcessoInexistente_RetornaVazio()
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        IObrigatoriedadeLegalRepository regraRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        IReadOnlyList<ObrigatoriedadeLegalDto> resultado = await ObterObrigatoriedadesAplicaveisQueryHandler.Handle(
            new ObterObrigatoriedadesAplicaveisQuery(Guid.CreateVersion7(), new DateOnly(2026, 6, 15)),
            processoRepository,
            regraRepository,
            CancellationToken.None);

        resultado.Should().BeEmpty();
        await regraRepository.DidNotReceiveWithAnyArgs()
            .ObterVigentesParaTipoProcessoAsync(default!, default, default);
    }

    [Fact(DisplayName = "Handle devolve conjuntos diferentes para datas de referência diferentes")]
    public async Task Handle_DatasDiferentes_DevolveConjuntosDiferentes()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DateOnly duranteVigencia = new(2026, 6, 15);
        DateOnly aposVigencia = new(2026, 7, 5);
        ObrigatoriedadeLegal regra = NovaRegra("INDIGENA_E_QUILOMBOLA", "PSIQ_VIGENTE");

        IProcessoSeletivoRepository processoRepository = RepositorioDoProcesso(processo);
        IObrigatoriedadeLegalRepository regraRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ConfigurarVigencia(regraRepository, regra, duranteVigencia, aposVigencia);

        IReadOnlyList<ObrigatoriedadeLegalDto> durante = await Consultar(processo.Id, duranteVigencia, processoRepository, regraRepository);
        IReadOnlyList<ObrigatoriedadeLegalDto> apos = await Consultar(processo.Id, aposVigencia, processoRepository, regraRepository);

        durante.Select(static item => item.RegraCodigo).Should().ContainSingle().Which.Should().Be("PSIQ_VIGENTE");
        apos.Should().BeEmpty();
    }

    private static void ConfigurarVigencia(IObrigatoriedadeLegalRepository repositorio, ObrigatoriedadeLegal regra,
        DateOnly durante, DateOnly depois)
    {
        repositorio.ObterVigentesParaTipoProcessoAsync("INDIGENA_E_QUILOMBOLA", durante, Arg.Any<CancellationToken>())
            .Returns([regra]);
        repositorio.ObterVigentesParaTipoProcessoAsync("INDIGENA_E_QUILOMBOLA", depois, Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterPorIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repository;
    }

    private static Task<IReadOnlyList<ObrigatoriedadeLegalDto>> Consultar(Guid id, DateOnly data,
        IProcessoSeletivoRepository processos, IObrigatoriedadeLegalRepository regras) =>
        ObterObrigatoriedadesAplicaveisQueryHandler.Handle(new(id, data), processos, regras, CancellationToken.None);

    private static ObrigatoriedadeLegal NovaRegra(string tipoProcessoCodigo, string regraCodigo) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            new ConcorrenciaDuplaObrigatoria(),
            new TextoNormativo("Descrição", "Lei", null, null),
            new DateOnly(2026, 1, 1),
            null).Value!;
}
