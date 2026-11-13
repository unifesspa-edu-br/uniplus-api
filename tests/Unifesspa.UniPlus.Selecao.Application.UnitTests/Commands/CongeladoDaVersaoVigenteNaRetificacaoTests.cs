namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// Os dois caminhos que congelam uma retificação respeitam o que a versão vigente congelou: não desfazem o ciclo
/// recursal já aberto (o preliminar publicado exige o definitivo) e mantêm a classificação do tipo de ato que ela
/// congelou, qualquer que seja a do catálogo de hoje (UNI-REQ-0144).
/// </summary>
public sealed class CongeladoDaVersaoVigenteNaRetificacaoTests
{
    [Fact(DisplayName = "Retificar recusa a configuração que retira o definitivo de uma matéria cujo preliminar já foi publicado")]
    public async Task Retificar_RetiraODefinitivoDeMateriaComPreliminarPublicado_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fechar a retificação recusa a configuração que retira o definitivo de uma matéria cujo preliminar já foi publicado")]
    public async Task Fechar_RetiraODefinitivoDeMateriaComPreliminarPublicado_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.AbrirSessao();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar mantém a classificação que a versão vigente congelou para o tipo: único e congelante no catálogo de hoje não o recusa nem muda o que a versão nova congela")]
    public async Task Retificar_TipoJaCongelado_MantemAClassificacaoDaVersaoVigente()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComResultadoCongeladoComoComumEUnicoNoCatalogo();

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        ConferirClassificacaoCongeladaDaVersaoVigente(cenario);
    }

    [Fact(DisplayName = "Fechar a retificação mantém a classificação que a versão vigente congelou para o tipo: único e congelante no catálogo de hoje não o recusa nem muda o que a versão nova congela")]
    public async Task FecharRetificacao_TipoJaCongelado_MantemAClassificacaoDaVersaoVigente()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComResultadoCongeladoComoComumEUnicoNoCatalogo();
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        ConferirClassificacaoCongeladaDaVersaoVigente(cenario);
    }

    private static void ConferirClassificacaoCongeladaDaVersaoVigente(CenarioDeRetificacaoPublicada cenario) =>
        cenario.EntradaCanonicalizada!.TiposDeAtoCongelados![AtoDaClassificacao].Should().Be(
            new TipoDeAtoCongelado(AtoDaClassificacao, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
}
