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

public sealed class CorrigirProdutoDoEditalCommandHandlerTests : ContextoDaPublicacaoDeProdutoDoEdital
{
    [Fact(DisplayName = "A correção do item sem motivo é recusada no campo do motivo, antes de travar ou consultar o processo")]
    public async Task CorrecaoSemMotivo_RecusaAntesDeQualquerLeitura()
    {
        PublicarProdutoDoEditalCommand semMotivo = Comando(Guid.CreateVersion7(), corrige: Guid.CreateVersion7()) with { MotivoDaCorrecao = null };

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(semMotivo);

        resposta.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "motivoDaCorrecao" && e.Error.Code == AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio);
        mensagens.Should().BeEmpty();
        await Processos.DidNotReceive().TravarAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A errata de uma lista publicada é aceita, leva o item e a política confirmada, e a porta recebe o ato corrigido")]
    public async Task Errata_DeItemPublicado_EAceita()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        AtoDoProcesso original = Publicado(Materia, preliminar.Id);
        AtoDoProcesso? gravado = null;
        await Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());

        (Result<AtoPublicadoDto> resposta, _) = await Publicar(Comando(preliminar.Id, corrige: original.Id));

        resposta.IsSuccess.Should().BeTrue();
        gravado!.ItemDoEdital.Should().Be(ItensDoCronograma[preliminar.Id].Chave);
        gravado.AtoCorrigidoId.Should().Be(original.Id);
        gravado.PoliticaDeDivulgacaoConfirmadaPorContaId.Should().Be(ContaId);
        await Registro.Received(1).RegistrarAsync(
            Arg.Is<AtoNormativoPublicado>(a => a.Retificacao != null && a.Retificacao.AtoRetificadoId == original.Id), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A correção de um resultado sem a confirmação da política é recusada")]
    public async Task Correcao_SemPolitica_ERecusada()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        AtoDoProcesso original = Publicado(Materia, preliminar.Id);

        await DeveRecusar(Comando(preliminar.Id, politica: false, corrige: original.Id), AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);
    }

    [Fact(DisplayName = "A correção que aponta ato de outro item é recusada")]
    public async Task Correcao_DeAtoDeOutroItem_ERecusada()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar, definitivo));
        AtoDoProcesso doPreliminar = Publicado(Materia, preliminar.Id);
        Publicado(Materia, definitivo.Id);

        await DeveRecusar(Comando(definitivo.Id, corrige: doPreliminar.Id), AtoDoProcessoErrorCodes.AtoCorrigidoNaoEhDoProduto);
    }

    [Fact(DisplayName = "A correção do preliminar depois do definitivo publicado é recusada, mandando corrigir o definitivo, que continua corrigível")]
    public async Task Correcao_DoPreliminarDepoisDoDefinitivo_ERecusada()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar, definitivo));
        AtoDoProcesso doPreliminar = Publicado(Materia, preliminar.Id);

        (Result<AtoPublicadoDto> antes, _) = await Publicar(Comando(preliminar.Id, corrige: doPreliminar.Id));
        antes.IsSuccess.Should().BeTrue("antes do definitivo, a correção do preliminar é aceita");

        AtoDoProcesso doDefinitivo = Publicado(Materia, definitivo.Id);

        (Result<AtoPublicadoDto> depois, _) = await Publicar(Comando(preliminar.Id, corrige: doPreliminar.Id));
        depois.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo);
        depois.Error.Message.Should().Contain("Corrija o resultado definitivo");

        (Result<AtoPublicadoDto> corrigeODefinitivo, _) = await Publicar(Comando(definitivo.Id, corrige: doDefinitivo.Id));
        corrigeODefinitivo.IsSuccess.Should().BeTrue("a correção do definitivo é aceita");
    }

    [Fact(DisplayName = "O definitivo só fecha o ciclo dele: o preliminar do mesmo tipo em outro ciclo, ainda aberto, continua corrigível")]
    public async Task Correcao_DoPreliminarDeOutroCicloAberto_EAceita()
    {
        (ProdutoDaFase preliminarA, _) = Par();
        (ProdutoDaFase preliminarB, _) = Par();
        ProdutoDaFase definitivoC = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        ProdutoDaFase definitivoD = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        Cronograma(
            FaseQueConclui("ABRE_A", 1, "FECHA_C", preliminarA),
            FaseQueConclui("ABRE_B", 2, "FECHA_D", preliminarB),
            Fase("FECHA_C", 3, definitivoC),
            Fase("FECHA_D", 4, definitivoD));
        AtoDoProcesso doA = Publicado(Materia, preliminarA.Id);
        AtoDoProcesso doB = Publicado(Materia, preliminarB.Id);
        Publicado(Materia, definitivoC.Id);

        await DeveRecusar(Comando(preliminarA.Id, corrige: doA.Id), AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo);
        (Result<AtoPublicadoDto> outroCiclo, _) = await Publicar(Comando(preliminarB.Id, corrige: doB.Id));
        outroCiclo.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Ligar congelaConfiguracao no catálogo depois da publicação não muda a classe da correção do resultado")]
    public async Task Correcao_DepoisDeLigarCongelaNoCatalogo_MantemAClasseCongelada()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        AtoDoProcesso original = Publicado(Materia, preliminar.Id);
        Tipo(Materia, resultado: true, congela: true);

        (Result<AtoPublicadoDto> resposta, _) = await Publicar(Comando(preliminar.Id, corrige: original.Id));

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await Registro.Received(1).RegistrarAsync(
            Arg.Is<AtoNormativoPublicado>(a => !a.AtributosDoTipo.CongelaConfiguracao), Arg.Any<CancellationToken>());
    }
}
