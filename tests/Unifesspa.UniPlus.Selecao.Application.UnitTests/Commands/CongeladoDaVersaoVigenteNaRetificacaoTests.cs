namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A retificação respeita o que a versão vigente congelou: não desfaz o ciclo recursal já aberto (o preliminar
/// publicado exige o definitivo) e mantém a classificação do tipo de ato que ela congelou, qualquer que seja a do
/// catálogo de hoje (UNI-REQ-0144).
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

    [Fact(DisplayName = "Retificar mantém a classificação que a versão vigente congelou para o tipo: único e congelante no catálogo de hoje não o recusa nem muda o que a versão nova congela")]
    public async Task Retificar_TipoJaCongelado_MantemAClassificacaoDaVersaoVigente()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComResultadoCongeladoComoComumEUnicoNoCatalogo();

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        ConferirClassificacaoCongeladaDaVersaoVigente(cenario);
    }

    private static void ConferirClassificacaoCongeladaDaVersaoVigente(CenarioDeRetificacaoPublicada cenario) =>
        cenario.EntradaCanonicalizada!.DadosDeCatalogo.TiposDeAtoCongelados![AtoDaClassificacao].Should().Be(
            new TipoDeAtoCongelado(AtoDaClassificacao, Nome: "Nome do ato", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
}
