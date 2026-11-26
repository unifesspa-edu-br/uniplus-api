namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

/// <summary>
/// A retificação recusa o que voltou a pendente na proteção de dados depois de vinculado e o fundamento de isenção
/// habilitado que nenhuma fase nem etapa declara decidir (UNI-REQ-0144, UNI-REQ-0123).
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

    [Fact(DisplayName = "Retificar recusa o processo cujo fundamento de isenção habilitado não tem fase nem etapa que declare o tipo de isenção")]
    public async Task Retificar_FundamentoSemLugarQueDeclare_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComIsencaoHabilitadaSemMotivos();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao, MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar recusa os motivos antes do eixo alterado do fato, porque a configuração se confere antes da operação")]
    public async Task Retificar_RecusaDosMotivos_PrecedeAsRecusasDaOperacao()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { DominioCongelado = TipoDoDado.Opcao });
        cenario.ComIsencaoHabilitadaSemMotivos();

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(
            FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao, MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado, CodigoDaRecusaDoEixoAlterado);
    }
}
