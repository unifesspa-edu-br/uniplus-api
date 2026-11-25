namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirAtendimentoEspecializadoCommandHandlerTests
{
    [Theory]
    [InlineData("PCD", true)]
    [InlineData("LACTANTE", false)]
    public async Task Handle_TipoDeficiencia_ExigeCondicaoPcd(string codigo, bool permitido)
    {
        Cenario cenario = new();
        cenario.ConfigurarCondicao(codigo);
        cenario.ConfigurarTipoDeficiencia();
        Result resultado = await cenario.Executar([cenario.CondicaoId], [cenario.TipoDeficienciaId]);
        resultado.IsSuccess.Should().Be(permitido);
        await ConferirPersistencia(cenario, resultado, permitido);
    }

    private static async Task ConferirPersistencia(Cenario cenario, Result resultado, bool permitido)
    {
        if (permitido)
        {
            cenario.Processo.AtendimentoEspecializado!.TiposDeficiencia.Should().ContainSingle();
            await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            resultado.Error!.Code.Should().Be("AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd");
            await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Handle_CondicaoIdDuplicado_RecusaSemConsultarLeitores()
    {
        Cenario cenario = new();
        Result resultado = await cenario.Executar([cenario.CondicaoId, cenario.CondicaoId], []);
        resultado.Error!.Code.Should().Be("AtendimentoEspecializado.CondicaoDuplicada");
        await cenario.CondicaoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_CondicaoInexistente_Recusa()
    {
        Cenario cenario = new();
        Result resultado = await cenario.Executar([cenario.CondicaoId], []);
        resultado.Error!.Code.Should().Be("AtendimentoEspecializado.CondicaoNaoEncontrada");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class Cenario
    {
        private readonly IProcessoSeletivoRepository _repository = Substitute.For<IProcessoSeletivoRepository>();
        private readonly IRecursoAcessibilidadeReader _recursoReader = Substitute.For<IRecursoAcessibilidadeReader>();
        private readonly ITipoDeficienciaReader _tipoReader = Substitute.For<ITipoDeficienciaReader>();
        public ICondicaoAtendimentoReader CondicaoReader { get; } = Substitute.For<ICondicaoAtendimentoReader>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public Guid CondicaoId { get; } = Guid.CreateVersion7();
        public Guid TipoDeficienciaId { get; } = Guid.CreateVersion7();
        public ProcessoSeletivo Processo { get; } = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        public Cenario() => _repository.ObterParaMutacaoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns(Processo);
        public void ConfigurarCondicao(string codigo) => CondicaoReader.ObterPorIdAsync(CondicaoId, Arg.Any<CancellationToken>())
            .Returns(new CondicaoAtendimentoView(CondicaoId, codigo, "Condição"));
        public void ConfigurarTipoDeficiencia() => _tipoReader.ObterPorIdAsync(TipoDeficienciaId, Arg.Any<CancellationToken>())
            .Returns(new TipoDeficienciaView(TipoDeficienciaId, "DEFICIENCIA_VISUAL", "Deficiência visual", "Deficiência relacionada à visão", null));
        public Task<Result> Executar(Guid[] condicoes, Guid[] tipos) => DefinirAtendimentoEspecializadoCommandHandler.Handle(
            new DefinirAtendimentoEspecializadoCommand(Processo.Id, condicoes, [], tipos), _repository,
            CondicaoReader, _recursoReader, _tipoReader, UnitOfWork, CancellationToken.None);
    }
}
