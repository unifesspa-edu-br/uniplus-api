namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

public sealed class EixosDoItemNaRetificacaoTests
{
    [Fact(DisplayName = "Retificar mantém o item cujos eixos são iguais aos do envelope publicado")]
    public async Task Retificar_ItemComOsMesmosEixosDoEnvelopePublicado_Aceita()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        VersaoConfiguracao vigente = (await cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, CancellationToken.None))!;
        EnvelopeReidratado publicado = cenario.Servicos.Codecs.Reidratar(vigente).Value!;
        EixosDoItemCongelado eixos = EixosDoItemCongelado.De(publicado.Configuracao.Coleta.Itens.Single(static f => f.FatoCodigo == Fato));

        (Result resposta, _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        resposta.Errors.Should().BeEmpty();
        EixosDoItemCongelado.De(cenario.EntradaCanonicalizada!.Processo.FatosColetados.Single(static f => f.FatoCodigo == Fato))
            .Should().Be(eixos);
    }

    [Fact(DisplayName = "Retificar recusa o item cujo tipo difere do envelope publicado antes de montar ou gravar")]
    public async Task Retificar_TipoDoItemDiferenteDoPublicado_RecusaAntesDeMontar()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { ItemPublicadoComoData = true });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        FieldError causa = resposta.Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
        causa.Error.Message.Should().Contain(Fato).And.Contain("tipo de renderização");
        causa.Field.Should().Be("/formularios/0/itens/28");
        await ConferirAusenciaDeEfeitosAsync(cenario, eventos);
    }

    [Fact(DisplayName = "Retificar acumula a troca do item e do metadado do mesmo fato, nessa ordem")]
    public async Task Retificar_ItemEMetadadoDivergentes_RecusaOsDoisNaOrdem()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(
            new SituacaoDoFatoCitado { ItemPublicadoComoData = true, DominioCongelado = TipoDoDado.Opcao });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(CodigoDaRecusaDoEixoAlterado, CodigoDaRecusaDoEixoAlterado);
        resposta.Errors.Select(static e => e.Field).Should().Equal("/formularios/0/itens/28", null);
        resposta.Errors[0].Error.Message.Should().Contain("tipo de renderização");
        resposta.Errors[1].Error.Message.Should().Contain("domínio");
        await ConferirAusenciaDeEfeitosAsync(cenario, eventos);
    }

    [Fact(DisplayName = "Retificar recusa o não coletável e continua comparando o eixo do outro item")]
    public async Task Retificar_NaoColetavelEOutroItemTrocado_AcumulaAsRecusas()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(
            new SituacaoDoFatoCitado { ItemPublicadoComoData = true, ComItemNovo = true, ItemNovoReclassificadoNoCatalogo = true });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado", CodigoDaRecusaDoEixoAlterado);
        resposta.Errors[0].Error.Message.Should().Contain(FatoNovo);
        resposta.Errors[1].Error.Message.Should().Contain(Fato).And.Contain("tipo de renderização");
        await ConferirAusenciaDeEfeitosAsync(cenario, eventos);
    }

    [Fact(DisplayName = "Retificar o item não coletável e divergente recusa só a coletabilidade")]
    public async Task Retificar_MesmoItemNaoColetavelETrocado_RecusaSoAColetabilidade()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(
            new SituacaoDoFatoCitado { ItemPublicadoComoData = true, FatoCitadoReclassificadoNoCatalogo = true });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado");
        await ConferirAusenciaDeEfeitosAsync(cenario, eventos);
    }

    private static async Task ConferirAusenciaDeEfeitosAsync(CenarioDeRetificacaoPublicada cenario, IEnumerable<object> eventos)
    {
        eventos.Should().BeEmpty();
        cenario.Processo.DomainEvents.Should().BeEmpty();
        cenario.EntradaCanonicalizada.Should().BeNull();
        await cenario.Servicos.NadaFoiGravadoAsync();
    }
}
