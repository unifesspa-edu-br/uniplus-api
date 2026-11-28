namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// A publicação e a retificação do edital somam as recusas do documento e do tipo do ato, que não dependem uma da outra, cada uma no
/// campo do pedido. O estado do processo, a base da retificação e o fuso institucional recusam sozinhos, antes de qualquer consulta, e
/// a configuração não é conferida quando o documento ou o tipo é recusado.
/// </summary>
public sealed class RecusasDoDocumentoEDoTipoDoAtoTests
{
    private const string CampoDoDocumento = ConferenciaDoDocumentoDoEdital.CampoDoDocumento;
    private const string CampoDoTipo = "ato.tipoAtoCodigo";
    private static readonly RelogioFixo Relogio = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public static TheoryData<OperacaoQueCongela> Operacoes() => [OperacaoQueCongela.Publicar, OperacaoQueCongela.Retificar];

    [Theory(DisplayName = "Documento não encontrado e tipo que não congela: as duas recusas, a do documento primeiro, sem ler a configuração")]
    [MemberData(nameof(Operacoes))]
    public async Task DocumentoNaoEncontradoETipoQueNaoCongela_SomamSemLerAConfiguracao(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.Documento).Com(DefeitoDoCongelamento.TipoDeAto);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        Causas(resposta).Should().Equal(
            (CampoDoDocumento, ConferenciaDoDocumentoDoEdital.NaoEncontrado),
            (CampoDoTipo, CodigoDa(DefeitoDoCongelamento.TipoDeAto)));
        resposta.Error!.Code.Should().Be(ConferenciaDoDocumentoDoEdital.NaoEncontrado);
        await AfirmarQueNadaFoiLidoNemGravadoAsync(cenario, eventos);
    }

    [Theory(DisplayName = "Documento não confirmado e tipo sem versão vigente: as duas recusas, e não afirma que o tipo não congela")]
    [MemberData(nameof(Operacoes))]
    public async Task DocumentoNaoConfirmadoETipoSemVersaoVigente_Somam(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.ComDocumentos(RepositorioComODocumentoPendente(cenario.Processo.Id));
        cenario.Servicos.TiposDeAto.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TipoAtoPublicadoView?>(null));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        Causas(resposta).Should().Equal(
            (CampoDoDocumento, ConferenciaDoDocumentoDoEdital.NaoConfirmado),
            (CampoDoTipo, ProcessoSeletivoErrorCodes.TipoDeAtoSemVersaoVigente));
        await AfirmarQueNadaFoiLidoNemGravadoAsync(cenario, eventos);
    }

    [Theory(DisplayName = "Documento enviado como outra finalidade e tipo recusado: o campo do documento sai antes do campo do tipo")]
    [MemberData(nameof(Operacoes))]
    public async Task DocumentoDeOutraFinalidadeETipoRecusado_SomamNaOrdemDoPedido(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.TipoDeAto);
        cenario.Servicos.ComDocumentos(RepositorioComODocumentoPendente(cenario.Processo.Id, FinalidadeDoDocumento.AnexoDoEdital, confirmado: true));

        (Result resposta, _) = await cenario.ExecutarAsync();

        Causas(resposta).Should().Equal(
            (CampoDoDocumento, ConferenciaDoDocumentoDoEdital.FinalidadeIncompativel),
            (CampoDoTipo, CodigoDa(DefeitoDoCongelamento.TipoDeAto)));
    }

    [Theory(DisplayName = "Tipo ausente e documento recusado: a ausência vem antes, e o catálogo de tipos não é consultado")]
    [MemberData(nameof(Operacoes))]
    public async Task TipoAusenteEDocumentoRecusado_NaoConsultaOTipo(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.Documento);

        (Result resposta, _) = await cenario.ExecutarAsync(Guid.CreateVersion7(), NovoAto() with { TipoAtoCodigo = null });

        Causas(resposta).Should().Equal(
            (CampoDoTipo, CodigosDeValidacao.Obrigatorio), (CampoDoDocumento, ConferenciaDoDocumentoDoEdital.NaoEncontrado));
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }

    [Theory(DisplayName = "Documento ausente e tipo recusado: a ausência vem antes, e o repositório de documentos não é consultado")]
    [MemberData(nameof(Operacoes))]
    public async Task DocumentoAusenteETipoRecusado_NaoConsultaODocumento(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.TipoDeAto);

        (Result resposta, _) = await cenario.ExecutarAsync(null, NovoAto());

        Causas(resposta).Should().Equal(
            (CampoDoDocumento, CodigosDeValidacao.Obrigatorio), (CampoDoTipo, CodigoDa(DefeitoDoCongelamento.TipoDeAto)));
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Theory(DisplayName = "Fuso institucional ausente com ausência, documento e tipo recusados: só a falha do fuso, sem consulta alguma")]
    [MemberData(nameof(Operacoes))]
    public async Task FusoAusente_RecusaSozinhoSemConsultar(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.Documento).Com(DefeitoDoCongelamento.TipoDeAto);
        cenario.Servicos.ComFusoDoInstante(new ResolvedorFusoIndisponivelDeTeste());

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync(Guid.CreateVersion7(), NovoAto() with { Ano = null });

        resposta.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.FusoInstitucionalNaoReconhecido");
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
        await AfirmarQueNadaFoiLidoNemGravadoAsync(cenario, eventos);
    }

    [Theory(DisplayName = "O conflito de estado responde sozinho, sem as recusas do documento e do tipo e sem consulta alguma")]
    [MemberData(nameof(Operacoes))]
    public async Task ConflitoDeEstado_RecusaSozinhoSemConsultar(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OutraOperacao(operacao)).Com(DefeitoDoCongelamento.Documento).Com(DefeitoDoCongelamento.TipoDeAto);

        (Result resposta, IEnumerable<object> eventos) = operacao == OperacaoQueCongela.Publicar
            ? await cenario.Servicos.PublicarAsync(cenario.Processo.Id)
            : await cenario.Servicos.RetificarAsync(cenario.Processo.Id);

        resposta.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificação sem motivo e com documento recusado: só o motivo, sem consulta alguma")]
    public async Task MotivoAusenteEDocumentoRecusado_RecusaSoOMotivo()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Retificar).Com(DefeitoDoCongelamento.Documento).Com(DefeitoDoCongelamento.TipoDeAto);

        (Result resposta, _) = await cenario.Servicos.RetificarAsync(new RetificarProcessoSeletivoCommand(
            cenario.Processo.Id, "   ", "001/2026-R1", null, null, Guid.CreateVersion7(), NovoAto()));

        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("motivo");
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }

    [Fact(DisplayName = "Retificação com o ciclo de recurso aberto e documento e tipo recusados: só as recusas do ciclo, com o código e o campo da conferência, sem consulta alguma")]
    public async Task CicloDeRecursoAberto_RecusaSozinhoSemConsultar()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();
        cenario.Servicos.ComDocumentos(Substitute.For<IDocumentoAPublicarRepository>());
        cenario.Servicos.TiposDeAto.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<TipoAtoPublicadoView?>(null));

        (Result resposta, _) = await cenario.RetificarAsync();

        Causas(resposta).Should().Equal(("/cronograma/fases", "ProcessoSeletivo.RetificacaoDesfazCicloRecursalAberto"));
        resposta.Errors.Select(static e => e.Error.Code).Should().NotContain([ConferenciaDoDocumentoDoEdital.NaoEncontrado, ProcessoSeletivoErrorCodes.TipoDeAtoSemVersaoVigente]);
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }

    [Theory(DisplayName = "O processo é carregado com bloqueio antes de qualquer leitura, e o documento é lido antes do tipo")]
    [MemberData(nameof(Operacoes))]
    public async Task OrdemDasLeituras_ProcessoDocumentoTipo(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.Documento).Com(DefeitoDoCongelamento.TipoDeAto);

        await cenario.ExecutarAsync();

        Received.InOrder(() =>
        {
            _ = cenario.Servicos.Repositorio.ObterParaMutacaoAsync(cenario.Processo.Id, Arg.Any<CancellationToken>());
            _ = cenario.Servicos.Documentos.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            _ = cenario.Servicos.TiposDeAto.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        });
    }

    [Theory(DisplayName = "O pedido válido consulta o documento e o tipo uma vez cada e é aceito")]
    [MemberData(nameof(Operacoes))]
    public async Task PedidoValido_ConsultaUmaVezECongela(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue();
        await cenario.Servicos.Documentos.Received(1).ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await cenario.Servicos.TiposDeAto.Received(1).ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    private static OperacaoQueCongela OutraOperacao(OperacaoQueCongela operacao) =>
        operacao == OperacaoQueCongela.Publicar ? OperacaoQueCongela.Retificar : OperacaoQueCongela.Publicar;

    private static (string? Campo, string Codigo)[] Causas(Result resposta) => [.. resposta.Errors.Select(static e => (e.Field, e.Error.Code))];

    // Com documento ou tipo recusado a configuração não é conferida: nada dela é lido, canonicalizado nem gravado.
    private static async Task AfirmarQueNadaFoiLidoNemGravadoAsync(CenarioDeRecusaNoCongelamento cenario, IEnumerable<object> eventos)
    {
        eventos.Should().BeEmpty();
        cenario.Servicos.CatalogoDeFatos.ReceivedCalls().Should().BeEmpty();
        cenario.Servicos.Canonicalizer.ReceivedCalls().Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }

    private static IDocumentoAPublicarRepository RepositorioComODocumentoPendente(
        Guid processoId, FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital, bool confirmado = false)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, finalidade, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, Relogio, TimeSpan.FromMinutes(15)).Value!;
        if (confirmado)
        {
            documento.Confirmar(1024, ProcessoSeletivoConformeBuilder.HashFixo, Relogio).IsSuccess.Should().BeTrue();
        }

        IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
        repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(documento);

        return repositorio;
    }
}
