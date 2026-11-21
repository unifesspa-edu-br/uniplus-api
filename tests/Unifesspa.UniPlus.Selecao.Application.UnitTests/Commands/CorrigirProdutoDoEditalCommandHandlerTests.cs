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

public sealed class CorrigirProdutoDoEditalCommandHandlerTests
{
    private readonly CenarioDaPublicacaoDoProdutoDoEdital _cenario = new();

    [Fact(DisplayName = "A correção do item sem motivo é recusada no campo do motivo, antes de travar ou consultar o processo")]
    public async Task CorrecaoSemMotivo_RecusaAntesDeQualquerLeitura()
    {
        PublicarProdutoDoEditalCommand semMotivo = _cenario.Comando(Guid.CreateVersion7(), corrige: Guid.CreateVersion7()) with { MotivoDaCorrecao = null };

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Publicar(semMotivo);

        resposta.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "motivoDaCorrecao" && e.Error.Code == AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio);
        mensagens.Should().BeEmpty();
        await _cenario.Processos.DidNotReceive().TravarAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A errata de uma lista publicada é aceita, leva o item e a política confirmada, e a porta recebe o ato corrigido")]
    public async Task Errata_DeItemPublicado_EAceita()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        AtoDoProcesso original = _cenario.Publicado(Materia, preliminar.Id);
        AtoDoProcesso? gravado = null;
        await _cenario.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(preliminar.Id, corrige: original.Id));

        resposta.IsSuccess.Should().BeTrue();
        gravado!.ItemDoEdital.Should().Be(_cenario.ItensDoCronograma[preliminar.Id].Chave);
        gravado.AtoCorrigidoId.Should().Be(original.Id);
        gravado.PoliticaDeDivulgacaoConfirmadaPorContaId.Should().Be(ContaId);
        await _cenario.Registro.Received(1).RegistrarAsync(
            Arg.Is<AtoNormativoPublicado>(a => a.Retificacao != null && a.Retificacao.AtoRetificadoId == original.Id), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A correção de um resultado sem a confirmação da política é recusada")]
    public async Task Correcao_SemPolitica_ERecusada()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        AtoDoProcesso original = _cenario.Publicado(Materia, preliminar.Id);

        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id, politica: false, corrige: original.Id), AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);
    }

    [Fact(DisplayName = "A correção que aponta ato de outro item é recusada")]
    public async Task Correcao_DeAtoDeOutroItem_ERecusada()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = _cenario.ParNaMesmaFase();
        AtoDoProcesso doPreliminar = _cenario.Publicado(Materia, preliminar.Id);
        _cenario.Publicado(Materia, definitivo.Id);

        await _cenario.DeveRecusar(_cenario.Comando(definitivo.Id, corrige: doPreliminar.Id), AtoDoProcessoErrorCodes.AtoCorrigidoNaoEhDoProduto);
    }

    [Fact(DisplayName = "A correção do preliminar depois do definitivo publicado é recusada, mandando corrigir o definitivo, que continua corrigível")]
    public async Task Correcao_DoPreliminarDepoisDoDefinitivo_ERecusada()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = _cenario.ParNaMesmaFase();
        AtoDoProcesso doPreliminar = _cenario.Publicado(Materia, preliminar.Id);

        Result<AtoPublicadoDto> antes = await Corrigir(preliminar, doPreliminar);
        antes.IsSuccess.Should().BeTrue("antes do definitivo, a correção do preliminar é aceita");

        AtoDoProcesso doDefinitivo = _cenario.Publicado(Materia, definitivo.Id);

        Result<AtoPublicadoDto> depois = await Corrigir(preliminar, doPreliminar);
        depois.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo);
        depois.Error.Message.Should().Contain("Corrija o resultado definitivo");

        Result<AtoPublicadoDto> corrigeODefinitivo = await Corrigir(definitivo, doDefinitivo);
        corrigeODefinitivo.IsSuccess.Should().BeTrue("a correção do definitivo é aceita");
    }

    private async Task<Result<AtoPublicadoDto>> Corrigir(ProdutoDaFase produto, AtoDoProcesso atoCorrigido)
    {
        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(produto.Id, corrige: atoCorrigido.Id));
        return resposta;
    }

    private (ProdutoDaFase PreliminarA, ProdutoDaFase PreliminarB, ProdutoDaFase DefinitivoC) DoisCiclosComODefinitivoDoPrimeiro()
    {
        (ProdutoDaFase preliminarA, ProdutoDaFase definitivoC) = Par();
        (ProdutoDaFase preliminarB, ProdutoDaFase definitivoD) = Par();
        _cenario.Cronograma(Fase("FASE_A", 1, preliminarA, definitivoC), Fase("FASE_B", 2, preliminarB, definitivoD));
        return (preliminarA, preliminarB, definitivoC);
    }

    [Fact(DisplayName = "O definitivo só fecha o ciclo dele: o preliminar do mesmo tipo em outro ciclo, ainda aberto, continua corrigível")]
    public async Task Correcao_DoPreliminarDeOutroCicloAberto_EAceita()
    {
        (ProdutoDaFase preliminarA, ProdutoDaFase preliminarB, ProdutoDaFase definitivoC) = DoisCiclosComODefinitivoDoPrimeiro();
        AtoDoProcesso doA = _cenario.Publicado(Materia, preliminarA.Id);
        AtoDoProcesso doB = _cenario.Publicado(Materia, preliminarB.Id);
        _cenario.Publicado(Materia, definitivoC.Id);

        await _cenario.DeveRecusar(_cenario.Comando(preliminarA.Id, corrige: doA.Id), AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo);
        Result<AtoPublicadoDto> outroCiclo = await Corrigir(preliminarB, doB);
        outroCiclo.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Ligar congelaConfiguracao no catálogo depois da publicação não muda a classe da correção do resultado")]
    public async Task Correcao_DepoisDeLigarCongelaNoCatalogo_MantemAClasseCongelada()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        AtoDoProcesso original = _cenario.Publicado(Materia, preliminar.Id);
        _cenario.Tipo(Materia, resultado: true, congela: true);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(preliminar.Id, corrige: original.Id));

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await _cenario.Registro.Received(1).RegistrarAsync(
            Arg.Is<AtoNormativoPublicado>(a => !a.AtributosDoTipo.CongelaConfiguracao), Arg.Any<CancellationToken>());
    }
}
