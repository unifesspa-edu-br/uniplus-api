namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

/// <summary>
/// Os dois caminhos que congelam uma retificação recusam o que voltou a pendente na proteção de dados depois
/// de vinculado e o fundamento de isenção habilitado sem motivos escolhidos (UNI-REQ-0144). Quem fecha a
/// sessão e é recusado não perde a sessão editorial.
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

    [Fact(DisplayName = "Fechar a retificação recusa o tipo da exigência e o fato citado que voltaram a pendente")]
    public async Task Fechar_ItensPendentes_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.AbrirSessao();
        cenario.ComTipoDaExigenciaPendente();
        await cenario.ComFatoCitadoPendenteAsync();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            "ProcessoSeletivo.TipoDocumentoComProtecaoPendente", "ProcessoSeletivo.FatoComProtecaoPendente");
        eventos.Should().BeEmpty();
        cenario.Processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
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

    [Fact(DisplayName = "Fechar a retificação recusa o processo cujo fundamento de isenção habilitado não tem motivos escolhidos")]
    public async Task Fechar_SemCoberturaDosMotivos_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.AbrirSessao();
        cenario.ComIsencaoHabilitadaSemMotivos();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().OnlyContain(c => c == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
        eventos.Should().BeEmpty();
        cenario.Processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação com recusa anterior não chega à cobertura dos motivos")]
    public async Task Fechar_RecusaAnterior_PrecedeACobertura()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { DominioCongelado = "CATEGORICO" });
        cenario.AbrirSessao();
        cenario.ComIsencaoHabilitadaSemMotivos();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be(CodigoDaRecusaDoEixoAlterado, "o portão da cobertura é o último");
    }
}
