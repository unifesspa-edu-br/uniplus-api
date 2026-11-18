namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaPublicacaoDoProdutoDoEdital;

public sealed class PublicarProdutoDoEditalCommandHandlerTests
{
    private readonly CenarioDaPublicacaoDoProdutoDoEdital _cenario = new();

    [Fact(DisplayName = "O preliminar de um resultado, com a política confirmada, grava o registro com o item e quem confirmou, e envia o ato com o hash do PDF")]
    public async Task Preliminar_ComPoliticaConfirmada_GravaORegistroDoItemEEntregaOAtoAPorta()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        AtoDoProcesso? gravado = null;
        await _cenario.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());
        AtoNormativoPublicado? entregue = null;
        _cenario.Registro.RegistrarAsync(Arg.Do<AtoNormativoPublicado>(a => entregue = a), Arg.Any<CancellationToken>()).Returns(Result.Success());

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Publicar(_cenario.Comando(preliminar.Id));

        resposta.IsSuccess.Should().BeTrue();
        ConferirAtoGravado(gravado, preliminar);
        mensagens.Should().ContainSingle().Which.Should().Be(new CopiarDocumentosDoAtoAoAcervo(_cenario.ProcessoId, gravado!.Id));
        ConferirAtoEntregue(entregue, gravado);
    }

    private void ConferirAtoGravado(AtoDoProcesso? gravado, ProdutoDaFase preliminar)
    {
        gravado.Should().NotBeNull();
        gravado!.ItemDoEdital.Should().Be(_cenario.ItensDoCronograma[preliminar.Id].Chave);
        gravado.TipoCodigo.Should().Be(Materia, "o tipo vem do item que o edital congelou");
        gravado.AtoCorrigidoId.Should().BeNull();
        gravado.PoliticaDeDivulgacaoConfirmadaPorContaId.Should().Be(ContaId);
        gravado.PoliticaDeDivulgacaoConfirmadaEm.Should().Be(Agora);
    }

    private void ConferirAtoEntregue(AtoNormativoPublicado? entregue, AtoDoProcesso gravado)
    {
        entregue!.AtoId.Should().Be(gravado.Id);
        entregue.TipoCodigo.Should().Be(Materia);
        entregue.DocumentoHash.Should().Be(_cenario.Principal.HashSha256);
        entregue.Retificacao.Should().BeNull();
    }

    [Fact(DisplayName = "O item lê o relógio uma vez e usa o instante no ato gravado e no ato entregue à porta")]
    public async Task Item_LeORelogioUmaVez_EOInstanteVaiParaOAtoEParaAPorta()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        RelogioDeLeituraContada relogio = new(Agora);
        AtoDoProcesso? gravado = null;
        await _cenario.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(preliminar.Id), relogio);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        relogio.Leituras.Should().Be(1);
        gravado!.PublicadoEm.Should().Be(Agora);
        await _cenario.Registro.Received(1).RegistrarAsync(Arg.Is<AtoNormativoPublicado>(a => a.PublicadoEm == Agora), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo do item é consultado no dia do instante, e a data que o documento declara não entra na consulta")]
    public async Task Item_ConsultaOTipoNoDiaDoInstante_NaoNoDaDataDoAto()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        DateOnly declarada = DataDoAto.AddDays(-5);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(preliminar.Id) with { DataDoAto = declarada });

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await _cenario.Tipos.Received().ObterVigenteAsync(Materia, DataDoAto, Arg.Any<CancellationToken>());
        await _cenario.Tipos.DidNotReceive().ObterVigenteAsync(Materia, declarada, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item que a versão vigente não declara é recusado, orientando a retificar o edital")]
    public async Task Produto_ForaDaVersaoVigente_ERecusado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));

        await _cenario.DeveRecusar(_cenario.Comando(Guid.CreateVersion7()), AtoDoProcessoErrorCodes.ProdutoNaoDeclarado);
    }

    [Fact(DisplayName = "O definitivo antes do preliminar da mesma matéria é recusado")]
    public async Task Definitivo_AntesDoPreliminar_ERecusado()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = _cenario.ParNaMesmaFase();

        await _cenario.DeveRecusar(_cenario.Comando(definitivo.Id), AtoDoProcessoErrorCodes.PreliminarNaoPublicado);
    }

    [Fact(DisplayName = "O definitivo com o preliminar já publicado é aceito")]
    public async Task Definitivo_ComPreliminarPublicado_EAceito()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = _cenario.ParNaMesmaFase();
        _cenario.Publicado(Materia, preliminar.Id);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(definitivo.Id));

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    [Fact(DisplayName = "Publicar de novo um item já publicado é recusado")]
    public async Task Produto_JaPublicado_ERecusado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        _cenario.Publicado(Materia, preliminar.Id);

        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id), AtoDoProcessoErrorCodes.ProdutoJaPublicado);
    }

    [Fact(DisplayName = "O resultado sem a confirmação da política é recusado, e o item que não é resultado dispensa a confirmação e não a registra")]
    public async Task Politica_ExigidaSoDoResultado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));

        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id, politica: false), AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);

        _cenario.Congelado = _cenario.Congelado with { EhResultado = false };
        AtoDoProcesso? gravado = null;
        await _cenario.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());
        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(preliminar.Id, politica: false));

        resposta.IsSuccess.Should().BeTrue();
        gravado!.PoliticaDeDivulgacaoConfirmadaPorContaId.Should().BeNull();
    }

    [Fact(DisplayName = "Item cujo tipo congela configuração é recusado: só o edital cria versão")]
    public async Task Tipo_QueCongelaConfiguracao_ERecusado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        _cenario.Congelado = _cenario.Congelado with { CongelaConfiguracao = true };

        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id), AtoDoProcessoErrorCodes.TipoCongelaConfiguracao);
    }

    [Fact(DisplayName = "A recusa da porta, como a vaga do objeto ocupada, devolve a recusa nomeada, descarta o rastreado e não grava")]
    public async Task RecusaDaPorta_DevolveADescartaENaoGrava()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        DomainError vagaOcupada = new("AtoNormativo.ObjetoJaTemAtoVivoDoTipo", "O objeto já tem o ato RESULTADO_HOMOLOGACAO nº 1/2027 vivo.");
        _cenario.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(vagaOcupada));

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Publicar(_cenario.Comando(preliminar.Id));

        resposta.Error.Should().Be(vagaOcupada);
        mensagens.Should().BeEmpty();
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O item publicado por outra requisição na corrida é recusado pela Seleção, e a porta nem é consultada")]
    public async Task ItemPublicadoNaCorrida_EDaSelecaoENaoConsultaAPorta()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_atos_do_processo_item_original"));

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Publicar(_cenario.Comando(preliminar.Id));

        resposta.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.ProdutoJaPublicado);
        mensagens.Should().BeEmpty();
        _cenario.Registro.DidNotReceive().TraduzirConflitoDaGravacao(Arg.Any<Exception>());
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Marcar o tipo como único no catálogo depois do preliminar não impede o definitivo: vale a marcação congelada na versão publicada")]
    public async Task Definitivo_DepoisDeMarcarOTipoComoUnicoNoCatalogo_NaoEImpedido()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = _cenario.ParNaMesmaFase();
        _cenario.Publicado(Materia, preliminar.Id);
        _cenario.Tipo(Materia, resultado: true, unico: true);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(definitivo.Id));

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await _cenario.Registro.Received(1).RegistrarAsync(
            Arg.Is<AtoNormativoPublicado>(a => !a.AtributosDoTipo.UnicoPorObjeto), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Desligar ehResultado no catálogo depois da publicação não dispensa a confirmação da política de divulgação")]
    public async Task Resultado_DepoisDeDesligarEhResultadoNoCatalogo_AindaExigeAPolitica()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        _cenario.Tipo(Materia, resultado: false);

        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id, politica: false), AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);
    }
}
