namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// A retificação lê a precedência entre fases do cadastro só quando o cronograma do processo mudou desde a versão publicada que
/// ela retifica.
/// </summary>
public sealed class PrecedenciaNaRetificacaoTests
{
    [Fact(DisplayName = "Retificar com o cronograma diferente do da versão publicada lê a precedência do cadastro")]
    public async Task Retificar_CronogramaDiferente_LeAPrecedencia()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComCronogramaPublicadoDiferenteDoDoProcesso();
        IPrecedenciaFaseReader leitor = PrecedenciasDeTeste.SemArestas();
        cenario.Servicos.ComPrecedencias(leitor);

        _ = await cenario.RetificarAsync();

        await leitor.Received(1).ListarVivasAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retificar com o cronograma igual ao da versão publicada não lê a precedência do cadastro")]
    public async Task Retificar_CronogramaIgual_NaoLeAPrecedencia()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComCronogramaPublicadoIgualAoDoProcesso();
        IPrecedenciaFaseReader leitor = PrecedenciasDeTeste.SemArestas();
        cenario.Servicos.ComPrecedencias(leitor);

        _ = await cenario.RetificarAsync();

        await leitor.DidNotReceive().ListarVivasAsync(Arg.Any<CancellationToken>());
    }
}
