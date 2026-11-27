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
        Result resultado = await cenario.Executar([cenario.CondicaoId], [], [cenario.TipoDeficienciaId]);
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
    public async Task Handle_CondicaoRepetidaEInexistente_JuntaAsRecusasEConsultaOsCadastros()
    {
        Cenario cenario = new();
        Result resultado = await cenario.Executar([cenario.CondicaoId, cenario.CondicaoId], [], []);
        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("condicaoIds", "AtendimentoEspecializado.CondicaoDuplicada"),
            ("condicaoIds[0]", "AtendimentoEspecializado.CondicaoNaoEncontrada"),
            ("condicaoIds[1]", "AtendimentoEspecializado.CondicaoNaoEncontrada"));
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CondicaoInexistente_Recusa()
    {
        Cenario cenario = new();
        Result resultado = await cenario.Executar([cenario.CondicaoId], [], []);
        resultado.Error!.Code.Should().Be("AtendimentoEspecializado.CondicaoNaoEncontrada");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProcessoInexistente_RecusaSemConsultarCadastrosNemGravar()
    {
        Cenario cenario = new();
        Result resultado = await cenario.ExecutarNoProcessoInexistente();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await cenario.ConferirSemLeituraNemGravacao();
    }

    [Fact]
    public async Task Handle_ProcessoPublicado_RecusaSemConsultarCadastrosNemGravar()
    {
        Cenario cenario = new();
        Result resultado = await cenario.ExecutarNoProcessoPublicado();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await cenario.ConferirSemLeituraNemGravacao();
    }

    [Fact]
    public async Task Handle_RecursoETipoInexistentes_JuntaAsDuasRecusasSemGravar()
    {
        Cenario cenario = new();
        cenario.ConfigurarCondicao("LACTANTE");
        Result resultado = await cenario.Executar([cenario.CondicaoId], [cenario.RecursoId], [cenario.TipoDeficienciaId]);
        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("recursoIds[0]", "AtendimentoEspecializado.RecursoNaoEncontrado"),
            ("tipoDeficienciaIds[0]", "AtendimentoEspecializado.TipoDeficienciaNaoEncontrado"));
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TipoDeficienciaInexistente_Recusa()
    {
        Cenario cenario = new();
        cenario.ConfigurarCondicao("PCD");
        cenario.ConfigurarRecurso();
        Result resultado = await cenario.Executar([cenario.CondicaoId], [cenario.RecursoId], [cenario.TipoDeficienciaId]);
        resultado.Error!.Code.Should().Be("AtendimentoEspecializado.TipoDeficienciaNaoEncontrado");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CondicaoRecursoETipoInexistentes_JuntaNaOrdemDoPedidoComOCodigoDaCondicao()
    {
        Cenario cenario = new();
        Result resultado = await cenario.Executar([cenario.CondicaoId], [cenario.RecursoId], [cenario.TipoDeficienciaId]);
        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("condicaoIds[0]", "AtendimentoEspecializado.CondicaoNaoEncontrada"),
            ("recursoIds[0]", "AtendimentoEspecializado.RecursoNaoEncontrado"),
            ("tipoDeficienciaIds[0]", "AtendimentoEspecializado.TipoDeficienciaNaoEncontrado"));
        resultado.Error!.Code.Should().Be("AtendimentoEspecializado.CondicaoNaoEncontrada");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VariosRecursosInexistentes_UmaRecusaParaCadaUmNaPosicaoDoPedido()
    {
        Cenario cenario = new();
        cenario.ConfigurarCondicao("LACTANTE");
        cenario.ConfigurarRecurso();
        Result resultado = await cenario.Executar([cenario.CondicaoId], [Guid.CreateVersion7(), cenario.RecursoId, Guid.CreateVersion7()], []);
        resultado.Errors.Select(static e => e.Field).Should().Equal("recursoIds[0]", "recursoIds[2]");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UmSoTipoInexistente_RecusaSoEleComOCampoDoItem()
    {
        Cenario cenario = new();
        cenario.ConfigurarCondicao("PCD");
        Result resultado = await cenario.Executar([cenario.CondicaoId], [], [cenario.TipoDeficienciaId]);
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("tipoDeficienciaIds[0]");
    }

    [Fact]
    public async Task Handle_TodosOsCadastrosEncontrados_CongelaPorValorEGrava()
    {
        Cenario cenario = new();
        cenario.ConfigurarCondicao("LACTANTE");
        cenario.ConfigurarRecurso();
        Result resultado = await cenario.Executar([cenario.CondicaoId], [cenario.RecursoId], []);
        resultado.IsSuccess.Should().BeTrue();
        cenario.Processo.AtendimentoEspecializado!.Recursos.Should().ContainSingle().Which.RecursoOrigemId.Should().Be(cenario.RecursoId);
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class Cenario
    {
        private readonly IProcessoSeletivoRepository _repository = Substitute.For<IProcessoSeletivoRepository>();
        public IRecursoAcessibilidadeReader RecursoReader { get; } = Substitute.For<IRecursoAcessibilidadeReader>();
        public ITipoDeficienciaReader TipoReader { get; } = Substitute.For<ITipoDeficienciaReader>();
        public ICondicaoAtendimentoReader CondicaoReader { get; } = Substitute.For<ICondicaoAtendimentoReader>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public Guid CondicaoId { get; } = Guid.CreateVersion7();
        public Guid RecursoId { get; } = Guid.CreateVersion7();
        public Guid TipoDeficienciaId { get; } = Guid.CreateVersion7();
        public ProcessoSeletivo Processo { get; } = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        public Cenario() => _repository.ObterParaMutacaoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns(Processo);
        public void ConfigurarCondicao(string codigo) => CondicaoReader.ObterPorIdAsync(CondicaoId, Arg.Any<CancellationToken>())
            .Returns(new CondicaoAtendimentoView(CondicaoId, codigo, "Condição"));
        public void ConfigurarTipoDeficiencia() => TipoReader.ObterPorIdAsync(TipoDeficienciaId, Arg.Any<CancellationToken>())
            .Returns(new TipoDeficienciaView(TipoDeficienciaId, "DEFICIENCIA_VISUAL", "Deficiência visual", "Deficiência relacionada à visão", null));
        public void ConfigurarRecurso() => RecursoReader.ObterPorIdAsync(RecursoId, Arg.Any<CancellationToken>())
            .Returns(new RecursoAcessibilidadeView(RecursoId, "LEDOR", "Ledor"));
        public Task<Result> Executar(Guid[] condicoes, Guid[] recursos, Guid[] tipos) => ExecutarNo(Processo.Id, condicoes, recursos, tipos);
        public Task<Result> ExecutarNoProcessoInexistente() => ExecutarNo(Guid.CreateVersion7(), [CondicaoId], [RecursoId], [TipoDeficienciaId]);
        public Task<Result> ExecutarNoProcessoPublicado()
        {
            ProcessoSeletivo publicado = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
            _repository.ObterParaMutacaoAsync(publicado.Id, Arg.Any<CancellationToken>()).Returns(publicado);
            return ExecutarNo(publicado.Id, [CondicaoId], [RecursoId], [TipoDeficienciaId]);
        }

        public async Task ConferirSemLeituraNemGravacao()
        {
            await CondicaoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
            await RecursoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
            await TipoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
            await UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        }

        private Task<Result> ExecutarNo(Guid processoId, Guid[] condicoes, Guid[] recursos, Guid[] tipos) => DefinirAtendimentoEspecializadoCommandHandler.Handle(
            new DefinirAtendimentoEspecializadoCommand(processoId, condicoes, recursos, tipos), _repository,
            new CadastrosDoAtendimentoEspecializado(CondicaoReader, RecursoReader, TipoReader), UnitOfWork, CancellationToken.None);
    }
}
