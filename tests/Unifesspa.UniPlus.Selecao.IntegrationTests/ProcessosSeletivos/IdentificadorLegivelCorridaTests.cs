namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A corrida que a consulta de unicidade não alcança: outro processo grava o mesmo identificador
/// entre a consulta e a gravação. A consulta é forçada a responder "livre", e quem recusa é o
/// índice único do Postgres real — a recusa tem de sair como o conflito nomeado, não como erro
/// interno.
/// </summary>
public sealed class IdentificadorLegivelCorridaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly Guid UnidadeId = Guid.NewGuid();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public IdentificadorLegivelCorridaTests(ProcessoSeletivoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Criar: identificador gravado por outro processo depois da consulta vira o conflito nomeado")]
    public async Task Criar_CorridaNoIndice_ConflitoNomeado()
    {
        IdentificadorLegivel disputado = IdentificadoresDeTeste.Novo();
        await PersistirConcorrenteAsync(disputado);

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        IProcessoSeletivoRepository repository = RepositorioQueNaoVeAConcorrencia(ctx);
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(new UnidadeView(
            UnidadeId, "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null,
            "1504208", "Marabá", "PA"));
        ITipoProcessoReader tipoProcessoReader = Substitute.For<ITipoProcessoReader>();
        tipoProcessoReader.ObterAtivoPorIdAsync(TipoProcesso.SiSU.OrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(TipoProcesso.SiSU.OrigemId, "SiSU", "SiSU", null));
        CriarProcessoSeletivoCommand command = new(
            "PS Corrida", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA")
        {
            IdentificadorLegivel = disputado.Valor,
        };

        Result<Guid> resultado = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, tipoProcessoReader, ctx, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso);
        ctx.ChangeTracker.HasChanges().Should().BeFalse(
            "o processo recusado não pode continuar no contexto para uma gravação posterior o inserir");
    }

    [Fact(DisplayName = "Definir: identificador gravado por outro processo depois da consulta vira o conflito nomeado, sem nada rastreado")]
    public async Task Definir_CorridaNoIndice_ConflitoNomeadoEDescarta()
    {
        IdentificadorLegivel disputado = IdentificadoresDeTeste.Novo();
        ProcessoSeletivo alvo = NovoProcesso(null);
        await PersistirAsync(alvo);
        await PersistirConcorrenteAsync(disputado);

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        IProcessoSeletivoRepository repository = RepositorioQueNaoVeAConcorrencia(ctx);

        Result<MutacaoAceita> resultado = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(alvo.Id, disputado.Valor, PrecondicaoIfMatch.Ausente),
            repository, ctx, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso);
        ctx.ChangeTracker.HasChanges().Should().BeFalse(
            "a gravação que o Wolverine dispara depois do handler não pode reencontrar a alteração recusada");
    }

    /// <summary>
    /// Repositório real em tudo, menos na consulta de unicidade, que responde "livre" — o estado
    /// de quem consultou antes de o processo concorrente gravar.
    /// </summary>
    private static IProcessoSeletivoRepository RepositorioQueNaoVeAConcorrencia(SelecaoDbContext ctx)
    {
        ProcessoSeletivoRepository real = new(ctx, TimeProvider.System);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.IdentificadorLegivelEmUsoAsync(default, default, default).ReturnsForAnyArgs(false);
        repository.AdicionarAsync(default!, default)
            .ReturnsForAnyArgs(ci => real.AdicionarAsync(ci.Arg<ProcessoSeletivo>(), ci.Arg<CancellationToken>()));
        repository.ObterParaMutacaoAsync(default, default)
            .ReturnsForAnyArgs(ci => real.ObterParaMutacaoAsync(ci.Arg<Guid>(), ci.Arg<CancellationToken>()));
        return repository;
    }

    private static ProcessoSeletivo NovoProcesso(IdentificadorLegivel? identificador) => ProcessoSeletivo.Criar(
        "PS Corrida", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        identificador);

    private Task PersistirConcorrenteAsync(IdentificadorLegivel identificador) =>
        PersistirAsync(NovoProcesso(identificador));

    private async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        await ctx.ProcessosSeletivos.AddAsync(processo);
        await ctx.SaveChangesAsync();
    }
}
