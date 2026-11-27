namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class CadastrosDoAtendimentoEspecializadoTests
{
    private readonly ICondicaoAtendimentoReader _condicoes = Substitute.For<ICondicaoAtendimentoReader>();
    private readonly IRecursoAcessibilidadeReader _recursos = Substitute.For<IRecursoAcessibilidadeReader>();
    private readonly ITipoDeficienciaReader _tiposDeficiencia = Substitute.For<ITipoDeficienciaReader>();

    [Fact(DisplayName = "Congela por valor o identificador, o código e o nome de cada condição, na ordem de entrada")]
    public async Task MontarAsync_Condicoes_CopiaPorValorNaOrdemDeEntrada()
    {
        CondicaoAtendimentoView lactante = new(Guid.CreateVersion7(), "LACTANTE", "Lactante");
        CondicaoAtendimentoView pcd = new(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        Cadastrar(lactante);
        Cadastrar(pcd);

        Result<AtendimentoEspecializado> montado = await Montar([pcd.Id, lactante.Id], [], []);

        montado.Value!.Condicoes.Select(static c => (c.CondicaoOrigemId, c.CondicaoCodigo, c.CondicaoNome))
            .Should().Equal((pcd.Id, "PCD", "Pessoa com deficiência"), (lactante.Id, "LACTANTE", "Lactante"));
    }

    [Fact(DisplayName = "Congela por valor o identificador, o código e o nome de cada recurso, na ordem de entrada")]
    public async Task MontarAsync_Recursos_CopiaPorValorNaOrdemDeEntrada()
    {
        RecursoAcessibilidadeView ledor = new(Guid.CreateVersion7(), "LEDOR", "Ledor");
        RecursoAcessibilidadeView transcritor = new(Guid.CreateVersion7(), "TRANSCRITOR", "Transcritor");
        Cadastrar(ledor);
        Cadastrar(transcritor);

        Result<AtendimentoEspecializado> montado = await Montar([], [transcritor.Id, ledor.Id], []);

        montado.Value!.Recursos.Select(static r => (r.RecursoOrigemId, r.RecursoCodigo, r.RecursoNome))
            .Should().Equal((transcritor.Id, "TRANSCRITOR", "Transcritor"), (ledor.Id, "LEDOR", "Ledor"));
    }

    [Fact(DisplayName = "Congela por valor o identificador, o código e o nome de cada tipo de deficiência, na ordem de entrada")]
    public async Task MontarAsync_TiposDeDeficiencia_CopiaPorValorNaOrdemDeEntrada()
    {
        CondicaoAtendimentoView pcd = new(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        TipoDeficienciaView visual = new(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual", "Deficiência relacionada à visão", null);
        TipoDeficienciaView auditiva = new(Guid.CreateVersion7(), "DEFICIENCIA_AUDITIVA", "Deficiência auditiva", "Deficiência relacionada à audição", null);
        Cadastrar(pcd);
        Cadastrar(visual);
        Cadastrar(auditiva);

        Result<AtendimentoEspecializado> montado = await Montar([pcd.Id], [], [auditiva.Id, visual.Id]);

        montado.Value!.TiposDeficiencia.Select(static t => (t.TipoDeficienciaOrigemId, t.TipoDeficienciaCodigo, t.TipoDeficienciaNome))
            .Should().Equal((auditiva.Id, "DEFICIENCIA_AUDITIVA", "Deficiência auditiva"), (visual.Id, "DEFICIENCIA_VISUAL", "Deficiência visual"));
    }

    [Fact(DisplayName = "Tipo de deficiência sem a condição de pessoa com deficiência é recusado pelo atendimento")]
    public async Task MontarAsync_TipoSemCondicaoPcd_RecusaPelaRegraDoAtendimento()
    {
        CondicaoAtendimentoView lactante = new(Guid.CreateVersion7(), "LACTANTE", "Lactante");
        TipoDeficienciaView visual = new(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual", "Deficiência relacionada à visão", null);
        Cadastrar(lactante);
        Cadastrar(visual);

        Result<AtendimentoEspecializado> montado = await Montar([lactante.Id], [], [visual.Id]);

        montado.Error!.Code.Should().Be("AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd");
    }

    [Fact(DisplayName = "Condição que o cadastro não devolveu não faz o tipo de deficiência ser recusado por falta da condição de pessoa com deficiência")]
    public async Task MontarAsync_CondicaoAusente_NaoRecusaOTipoPorFaltaDaCondicaoPcd()
    {
        TipoDeficienciaView visual = new(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual", "Deficiência relacionada à visão", null);
        Cadastrar(visual);

        Result<AtendimentoEspecializado> montado = await Montar([Guid.CreateVersion7()], [], [visual.Id]);

        montado.Errors.Select(static e => e.Error.Code).Should().Equal("AtendimentoEspecializado.CondicaoNaoEncontrada");
    }

    [Fact(DisplayName = "Recurso ausente não esconde a recusa do tipo de deficiência sem a condição de pessoa com deficiência")]
    public async Task MontarAsync_RecursoAusente_MantemARecusaDoTipoSemCondicaoPcd()
    {
        CondicaoAtendimentoView lactante = new(Guid.CreateVersion7(), "LACTANTE", "Lactante");
        TipoDeficienciaView visual = new(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual", "Deficiência relacionada à visão", null);
        Cadastrar(lactante);
        Cadastrar(visual);

        Result<AtendimentoEspecializado> montado = await Montar([lactante.Id], [Guid.CreateVersion7()], [visual.Id]);

        montado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("tipoDeficienciaIds", "AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd"),
            ("recursoIds[0]", "AtendimentoEspecializado.RecursoNaoEncontrado"));
    }

    [Fact(DisplayName = "Recusas sem cadastro não repetem o identificador enviado")]
    public async Task MontarAsync_NaoEncontrado_NaoRepeteOIdentificadorNaMensagem()
    {
        Guid ausente = Guid.CreateVersion7();

        Result<AtendimentoEspecializado> montado = await Montar([ausente], [], []);

        montado.Errors.Should().ContainSingle().Which.Error.Message.Should().NotContain(ausente.ToString());
    }

    [Fact(DisplayName = "Sem identificadores, monta o atendimento vazio sem consultar os cadastros")]
    public async Task MontarAsync_SemIdentificadores_MontaVazioSemConsultar()
    {
        Result<AtendimentoEspecializado> montado = await Montar([], [], []);

        montado.IsSuccess.Should().BeTrue();
        montado.Value!.Condicoes.Should().BeEmpty();
        await _condicoes.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    private void Cadastrar(CondicaoAtendimentoView condicao) =>
        _condicoes.ObterPorIdAsync(condicao.Id, Arg.Any<CancellationToken>()).Returns(condicao);

    private void Cadastrar(RecursoAcessibilidadeView recurso) =>
        _recursos.ObterPorIdAsync(recurso.Id, Arg.Any<CancellationToken>()).Returns(recurso);

    private void Cadastrar(TipoDeficienciaView tipo) =>
        _tiposDeficiencia.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

    private Task<Result<AtendimentoEspecializado>> Montar(Guid[] condicoes, Guid[] recursos, Guid[] tiposDeficiencia) =>
        new CadastrosDoAtendimentoEspecializado(_condicoes, _recursos, _tiposDeficiencia).MontarAsync(
            new DefinirAtendimentoEspecializadoCommand(Guid.CreateVersion7(), condicoes, recursos, tiposDeficiencia), CancellationToken.None);
}
