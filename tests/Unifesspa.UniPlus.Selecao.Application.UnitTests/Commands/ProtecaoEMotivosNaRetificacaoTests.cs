namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

/// <summary>
/// A retificação recusa o que voltou a pendente na proteção de dados depois de vinculado e o fundamento de
/// isenção habilitado sem motivos escolhidos (UNI-REQ-0144).
/// </summary>
public sealed class ProtecaoEMotivosNaRetificacaoTests
{
    [Fact(DisplayName = "Retificar recusa o tipo da exigência que voltou a pendente na proteção de dados")]
    public async Task Retificar_TipoPendente_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComTipoDaExigenciaPendente();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.TipoDocumentoComProtecaoPendente");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar recusa o fato citado que voltou a pendente na proteção de dados")]
    public async Task Retificar_FatoPendente_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        await cenario.ComFatoCitadoPendenteAsync();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.FatoComProtecaoPendente");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar de uma vez recusa o processo cujo fundamento de isenção habilitado não tem motivos escolhidos")]
    public async Task Retificar_SemCoberturaDosMotivos_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComIsencaoHabilitadaSemMotivos();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().OnlyContain(c => c == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente)
            .And.HaveCount(2, "falta um motivo favorável e um desfavorável para o fundamento");
        eventos.Should().BeEmpty();
    }
}
