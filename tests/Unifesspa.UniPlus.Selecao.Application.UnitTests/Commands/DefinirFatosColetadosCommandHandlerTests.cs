namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura do <see cref="DefinirFatosColetadosCommandHandler"/> (Story #984): a coletabilidade
/// (só fato declarado com binding de campo de inscrição), a validação semântica das
/// pré-condições contra o vocabulário fechado, e a delegação da estrutura do grafo ao agregado.
/// </summary>
public sealed class DefinirFatosColetadosCommandHandlerTests
{
    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IFatoCandidatoReader FatoCandidatoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        Mocks mocks = new(repository, Substitute.For<IFatoCandidatoReader>(), Substitute.For<ISelecaoUnitOfWork>());
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(VocabularioSeed());
        return mocks;
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirFatosColetadosCommand command) =>
        DefinirFatosColetadosCommandHandler.Handle(
            command, mocks.Repository, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

    private static IReadOnlyList<FatoCandidatoView> VocabularioSeed() =>
    [
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO", "ESCALAR",
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"], "INSCRICAO", "CAMPO_INSCRICAO:COR_RACA", null),
        new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", null, "BOOLEANO", "DECLARADO", "ESCALAR",
            null, "INSCRICAO", "CAMPO_INSCRICAO:BAIXA_RENDA", null),
        new(Guid.CreateVersion7(), "MODALIDADE", "Modalidade", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO",
            null, "INSCRICAO", "REGRA_DERIVACAO:MODALIDADE", null),
        new(Guid.CreateVersion7(), "RENDA_PER_CAPITA", "Renda per capita", null, "NUMERICO", "DERIVADO", "ESCALAR",
            null, "INSCRICAO", "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", null),
    ];

    private static ProcessoSeletivo ProcessoEmRascunho() =>
        ProcessoSeletivo.Criar("PS Fatos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

    private static CondicaoPrecondicaoInput Condicao(string fato, string operador, object valor) =>
        new(fato, operador, JsonSerializer.SerializeToElement(valor));

    [Fact(DisplayName = "Processo inexistente retorna ProcessoSeletivo.NaoEncontrado sem tocar no reader")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo: null, processoId);
        DefinirFatosColetadosCommand command = new(processoId, []);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Coleta com pré-condição válida é aceita, persistida, e devolve 204 sem ETag em rascunho")]
    public async Task Handle_ColetaValida_DefineEPersiste()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFatosColetadosCommand command = new(processo.Id,
        [
            new FatoColetadoInput("COR_RACA", 0, null),
            new FatoColetadoInput("BAIXA_RENDA", 1, [[Condicao("COR_RACA", "IGUAL", "PRETA")]]),
        ]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");
        processo.FatosColetados.Select(f => f.FatoCodigo).Should().BeEquivalentTo(["COR_RACA", "BAIXA_RENDA"]);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Coletar um fato derivado (MODALIDADE) é recusado como não coletável")]
    public async Task Handle_FatoDerivado_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, [new FatoColetadoInput("MODALIDADE", 0, null)]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.FatoNaoColetavel");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Coletar um fato computado de atributo (RENDA_PER_CAPITA) é recusado como não coletável")]
    public async Task Handle_FatoComputado_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, [new FatoColetadoInput("RENDA_PER_CAPITA", 0, null)]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.FatoNaoColetavel");
    }

    [Fact(DisplayName = "Fato fora do vocabulário é recusado como desconhecido, sem tradução")]
    public async Task Handle_FatoForaDoVocabulario_RetornaDesconhecido()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, [new FatoColetadoInput("V", 0, null)]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.FatoDesconhecido");
    }

    [Fact(DisplayName = "Pré-condição com operador incompatível com o domínio do fato citado é recusada")]
    public async Task Handle_OperadorIncompativel_RetornaErroSemantico()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // COR_RACA é categórico: MAIOR_IGUAL não se aplica.
        DefinirFatosColetadosCommand command = new(processo.Id,
        [
            new FatoColetadoInput("COR_RACA", 0, null),
            new FatoColetadoInput("BAIXA_RENDA", 1, [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]]),
        ]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }

    [Fact(DisplayName = "Pré-condição que cita fato posterior na ordem é recusada pela estrutura do grafo (domínio)")]
    public async Task Handle_CitaFatoPosterior_RetornaErroDoDominio()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // BAIXA_RENDA na ordem 0 cita COR_RACA (ordem 1, posterior).
        DefinirFatosColetadosCommand command = new(processo.Id,
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, [[Condicao("COR_RACA", "IGUAL", "PRETA")]]),
            new FatoColetadoInput("COR_RACA", 1, null),
        ]);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoColetado.PrecondicaoCitaFatoPosterior");
    }
}
