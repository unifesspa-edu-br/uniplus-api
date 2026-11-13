namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Inclusão e retirada de motivo no processo, a leitura dos escolhidos e o portão dos motivos nas
/// transições que congelam versão (UNI-REQ-0122, UNI-REQ-0123, UNI-REQ-0285).
/// </summary>
public sealed class MotivosDoProcessoCommandHandlersTests
{
    private readonly ProcessoSeletivo _processo = NovoProcesso();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IMotivoDeDecisaoRepository _cadastro = Substitute.For<IMotivoDeDecisaoRepository>();
    private readonly IMotivoDoProcessoRepository _escolhidos = Substitute.For<IMotivoDoProcessoRepository>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
    private readonly TimeProvider _relogio = new RelogioFixo(new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero));

    public MotivosDoProcessoCommandHandlersTests()
    {
        _processos.ObterParaMutacaoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns(_processo);
        _escolhidos.ListarDoProcessoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact(DisplayName = "Incluir copia o motivo do cadastro no processo e grava")]
    public async Task Incluir_MotivoAtivo_Grava()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result<MutacaoAceita> resultado = await Incluir(origem.Id);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _escolhidos.Received(1).AdicionarAsync(
            Arg.Is<MotivoDoProcesso>(m => m.MotivoDeDecisaoOrigemId == origem.Id && m.ProcessoSeletivoId == _processo.Id),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Incluir em processo inexistente devolve não encontrado")]
    public async Task Incluir_ProcessoInexistente_NaoEncontrado()
    {
        Result<MutacaoAceita> resultado = await IncluirMotivoNoProcessoCommandHandler.Handle(
            new IncluirMotivoNoProcessoCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), null, PrecondicaoIfMatch.Ausente),
            _processos, _cadastro, _escolhidos, _unitOfWork, _relogio, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Incluir motivo inexistente no cadastro devolve não encontrado")]
    public async Task Incluir_MotivoInexistente_NaoEncontrado()
    {
        (await Incluir(Guid.CreateVersion7())).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "A recusa do domínio descarta o rastreamento e não grava")]
    public async Task Incluir_MotivoInativo_DescartaSemGravar()
    {
        MotivoDeDecisao origem = Motivo();
        origem.Desativar();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result<MutacaoAceita> resultado = await Incluir(origem.Id);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoInativo);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A escolha repetida em corrida volta como conflito, com o rastreamento descartado")]
    public async Task Incluir_ConflitoAoGravar_DevolveConflito()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético"));

        Result<MutacaoAceita> resultado = await Incluir(origem.Id);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Retirar motivo escolhido no rascunho remove e grava")]
    public async Task Retirar_Escolhido_Remove()
    {
        MotivoDoProcesso escolhido = Escolhido(Motivo());
        _escolhidos.ListarDoProcessoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([escolhido]);

        Result<MutacaoAceita> resultado = await Retirar(escolhido.MotivoDeDecisaoOrigemId);

        resultado.IsSuccess.Should().BeTrue();
        _escolhidos.Received(1).Remover(escolhido);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retirar motivo que o processo não escolheu devolve não escolhido")]
    public async Task Retirar_NaoEscolhido_Recusa()
    {
        (await Retirar(Guid.CreateVersion7())).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEscolhido);
    }

    [Fact(DisplayName = "Retirar motivo já publicado é recusado sem remover")]
    public async Task Retirar_Congelado_Recusa()
    {
        MotivoDoProcesso escolhido = Escolhido(Motivo());
        MotivosAoCongelar.Congelar([escolhido]);
        _escolhidos.ListarDoProcessoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([escolhido]);

        Result<MutacaoAceita> resultado = await Retirar(escolhido.MotivoDeDecisaoOrigemId);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao);
        _escolhidos.DidNotReceive().Remover(Arg.Any<MotivoDoProcesso>());
    }

    [Fact(DisplayName = "Retirar de processo inexistente devolve não encontrado")]
    public async Task Retirar_ProcessoInexistente_NaoEncontrado()
    {
        Result<MutacaoAceita> resultado = await RetirarMotivoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDoProcessoCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), PrecondicaoIfMatch.Ausente),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "A leitura devolve os escolhidos ordenados por tipo e código")]
    public async Task Listar_OrdenaPorTipoECodigo()
    {
        MotivoDoProcesso classificacao = Escolhido(Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", null));
        MotivoDoProcesso isencaoB = Escolhido(Motivo(codigo: "RENDA_B"));
        MotivoDoProcesso isencaoA = Escolhido(Motivo(codigo: "RENDA_A"));
        _escolhidos.ListarDoProcessoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([classificacao, isencaoB, isencaoA]);

        IReadOnlyList<MotivoDoProcessoDto> lidos = await ListarMotivosDoProcessoQueryHandler.Handle(
            new ListarMotivosDoProcessoQuery(_processo.Id), _escolhidos, CancellationToken.None);

        lidos.Select(m => m.Codigo).Should().Equal("RENDA_A", "RENDA_B", "NOTA_ZERADA");
        lidos[0].Tipo.Should().Be(TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa);
        lidos[0].Sentido.Should().Be(SentidoDoMotivoCodigo.Desfavoravel);
    }

    [Fact(DisplayName = "Ao congelar, a falta de cobertura recusa e não congela")]
    public async Task AoCongelar_SemCobertura_RecusaSemCongelar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(true, 100m, [FundamentoIsencaoCodigo.CadastroUnico]).Value!, PrecondicaoIfMatch.Ausente);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = Escolhido(origem);

        Result resultado = await new ConferenciaDosMotivosAoCongelar(
            MotivosDoProcessoDeTeste.Com([new MotivoEscolhidoComOrigem(escolhido, origem)]))
            .ConferirAsync(processo, CancellationToken.None);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
        escolhido.Congelado.Should().BeFalse();
    }

    [Fact(DisplayName = "Ao congelar, a escolha de motivo desativado recusa antes da cobertura")]
    public async Task AoCongelar_MotivoDesativado_Recusa()
    {
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = Escolhido(origem);
        origem.Desativar();

        Result resultado = await new ConferenciaDosMotivosAoCongelar(
            MotivosDoProcessoDeTeste.Com([new MotivoEscolhidoComOrigem(escolhido, origem)]))
            .ConferirAsync(NovoProcesso(), CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoDesativado);
    }

    [Fact(DisplayName = "Ao congelar com cobertura completa, a escolha congela")]
    public async Task AoCongelar_ComCobertura_Congela()
    {
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = Escolhido(origem);

        Result resultado = await new ConferenciaDosMotivosAoCongelar(
            MotivosDoProcessoDeTeste.Com([new MotivoEscolhidoComOrigem(escolhido, origem)]))
            .ConferirAsync(NovoProcesso(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        escolhido.Congelado.Should().BeTrue();
    }

    [Fact(DisplayName = "Nos últimos portões, sem recusa anterior, a falta de cobertura recusa")]
    public async Task UltimosPortoes_SemRecusaAnterior_RecusaPorCobertura()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(true, 100m, [FundamentoIsencaoCodigo.CadastroUnico]).Value!, PrecondicaoIfMatch.Ausente);

        Result<DefinicaoAvaliavel> resultado = await new ConferenciaDosMotivosAoCongelar(
            MotivosDoProcessoDeTeste.Com([]))
            .UltimosPortoesAsync(processo, [], CancellationToken.None);

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(e => e.Error.Code == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    private Task<Result<MutacaoAceita>> Incluir(Guid motivoId) =>
        IncluirMotivoNoProcessoCommandHandler.Handle(
            new IncluirMotivoNoProcessoCommand(_processo.Id, motivoId, null, PrecondicaoIfMatch.Ausente),
            _processos, _cadastro, _escolhidos, _unitOfWork, _relogio, CancellationToken.None);

    private Task<Result<MutacaoAceita>> Retirar(Guid motivoId) =>
        RetirarMotivoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDoProcessoCommand(_processo.Id, motivoId, PrecondicaoIfMatch.Ausente),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

    private MotivoDoProcesso Escolhido(MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(_processo, [], origem, null, PrecondicaoIfMatch.Ausente, _relogio.GetUtcNow(), null)).Value!;

    private static MotivoDeDecisao Motivo(
        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisao.IsencaoDaTaxa, string codigo = "RENDA_ACIMA_DO_LIMITE", string? subclassificacao = FundamentoIsencaoCodigo.CadastroUnico) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo, codigo, "Renda familiar per capita acima do limite legal.", subclassificacao, SentidoDoMotivo.Desfavoravel)).Value!;

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar(
            "PSR Medicina 2027", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
