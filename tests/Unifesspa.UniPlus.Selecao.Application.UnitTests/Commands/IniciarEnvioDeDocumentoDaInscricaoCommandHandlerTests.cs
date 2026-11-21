namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O início do envio de documento da inscrição: a exigência na árvore congelada da versão da inscrição,
/// a ocorrência, a apresentação reaproveitada só por id explícito e a disputa da revisão da inscrição.
/// </summary>
public sealed class IniciarEnvioDeDocumentoDaInscricaoCommandHandlerTests
{
    private readonly CenarioDosDocumentosDaInscricaoDeTeste _cenario = new();

    [Fact(DisplayName = "Iniciar o envio de uma exigência da versão da inscrição cria a apresentação e o arquivo pendente e devolve a URL")]
    public async Task Iniciar_ExigenciaDaVersao_CriaApresentacaoEArquivo()
    {
        _cenario.Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await _cenario.Iniciar(_cenario.Laudo);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ContentTypeExigido.Should().Be("application/pdf");
        await _cenario.Documentos.Received(1).AdicionarApresentacaoAsync(Arg.Any<ApresentacaoDeDocumento>(), Arg.Any<CancellationToken>());
        await _cenario.Documentos.Received(1).AdicionarArquivoAsync(
            Arg.Is<ArquivoDaApresentacao>(a => a.Id == resultado.Value.ArquivoId && a.ObjectKey.Contains(a.Id.ToString("D"), StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await _cenario.UnidadeDeTrabalho.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O iniciar avança a revisão da inscrição, e quem perde a disputa não cria arquivo")]
    public async Task Iniciar_DisputaDaRevisao_PerdidaNaoCriaArquivo()
    {
        Inscricao inscricao = _cenario.Rascunho();
        (await _cenario.Iniciar(_cenario.Laudo)).IsSuccess.Should().BeTrue();
        await _cenario.Inscricoes.Received(1).TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, false, Arg.Any<CancellationToken>());

        _cenario.Documentos.ClearReceivedCalls();
        _cenario.Inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, false, Arg.Any<CancellationToken>()).Returns(false);

        (await _cenario.Iniciar(_cenario.Laudo)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.InscricaoAlterada);
        await _cenario.Documentos.DidNotReceive().AdicionarArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O rascunho que adota a versão divulgada tem a adoção gravada junto do envio pendente")]
    public async Task Iniciar_AdocaoDeVersao_EGravada()
    {
        Inscricao inscricao = _cenario.Rascunho();
        VersaoConfiguracao divulgada = DivulgacaoDeTeste.VersaoConforme(_cenario.ProcessoId).Versao;
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            _cenario.Processos, _cenario.Codecs, _cenario.Configuracao,
            fixada: (_cenario.Versao, DivulgacaoDeTeste.FimDoPeriodo), divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo));

        (await _cenario.Iniciar(_cenario.Laudo)).IsSuccess.Should().BeTrue();

        inscricao.Formulario.VersaoFormularioId.Should().Be(divulgada.Id);
        await _cenario.Inscricoes.Received(1).AtualizarAsync(inscricao, Arg.Any<CancellationToken>());
        await _cenario.Inscricoes.DidNotReceive().TentarAvancarRevisaoAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Exigência que não está na árvore congelada da versão da inscrição não recebe arquivo")]
    public async Task Iniciar_ExigenciaForaDaVersao_Recusa()
    {
        _cenario.Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await _cenario.Iniciar(CenarioDosDocumentosDaInscricaoDeTeste.Exigencia("OUTRA"));

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada);
        await _cenario.Documentos.DidNotReceive().AdicionarArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Exigência de outra fase que não a da inscrição não recebe arquivo")]
    public async Task Iniciar_ExigenciaDeOutraFase_Recusa()
    {
        _cenario.Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await _cenario.Iniciar(_cenario.DeOutraFase);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada);
        await _cenario.Documentos.DidNotReceive().AdicionarArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "A exigência por ocorrência pede uma ocorrência que a inscrição tem; a que não se repete não admite ocorrência")]
    [InlineData("LAUDO", "dep-1", false)]
    [InlineData("CERTIDAO", null, false)]
    [InlineData("CERTIDAO", "dep-9", false)]
    [InlineData("CERTIDAO", "dep-1", true)]
    public async Task Iniciar_Ocorrencia_ConfereComAExigencia(string exigencia, string? ocorrencia, bool aceita)
    {
        _cenario.Rascunho("dep-1", "dep-2");

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await _cenario.Iniciar(exigencia == "LAUDO" ? _cenario.Laudo : _cenario.Certidao, ocorrencia);

        resultado.IsSuccess.Should().Be(aceita);
        if (!aceita)
        {
            resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.OcorrenciaInvalida);
        }
    }

    [Fact(DisplayName = "A apresentação só é reaproveitada com o id explícito e da mesma exigência; sem o id, abre-se outra")]
    public async Task Iniciar_Apresentacao_SoReaproveitaPorIdExplicito()
    {
        Inscricao inscricao = _cenario.Rascunho("dep-1");
        ApresentacaoDeDocumento doLaudo = ApresentacaoDeDocumento.Criar(inscricao.Id, _cenario.Laudo, null);
        _cenario.Documentos.ObterApresentacaoAsync(doLaudo.Id, Arg.Any<CancellationToken>()).Returns(doLaudo);

        (await _cenario.Iniciar(_cenario.Certidao, "dep-1", doLaudo.Id)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ApresentacaoNaoEncontrada);

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> verso = await _cenario.Iniciar(_cenario.Laudo, apresentacaoId: doLaudo.Id);
        verso.Value!.ApresentacaoId.Should().Be(doLaudo.Id);
        await _cenario.Documentos.DidNotReceive().AdicionarApresentacaoAsync(Arg.Any<ApresentacaoDeDocumento>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com as inscrições encerradas o envio é recusado")]
    public async Task Iniciar_ForaDoPeriodo_Recusa()
    {
        _cenario.Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await _cenario.Iniciar(_cenario.Laudo, agora: DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }
}
