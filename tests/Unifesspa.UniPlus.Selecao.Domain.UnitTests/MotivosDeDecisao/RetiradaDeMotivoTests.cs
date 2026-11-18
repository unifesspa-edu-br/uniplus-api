namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// A retirada de um motivo de uma fase ou etapa: só no rascunho; o que já consta do edital publicado não sai (UNI-REQ-0124).
/// </summary>
public sealed class RetiradaDeMotivoTests
{
    private const string Homologacao = "HOMOLOGACAO_INSCRICOES";

    private static MotivoDeDecisao Motivo() =>
        ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", SentidoDoMotivo.Desfavoravel);

    private static Result<MotivoDoProcesso> Retirar(
        ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhaAtual, LugarDaDecisao lugar, MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Retirar(processo, escolhaAtual, lugar, origem.Id);

    [Theory(DisplayName = "No rascunho, retirar motivo escolhido na fase ou na etapa é aceito e devolve a escolha a remover")]
    [InlineData(Homologacao, null)]
    [InlineData(null, "Prova objetiva")]
    public void Rascunho_Retira(string? fase, string? etapa)
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao lugar = fase is not null ? ProcessoParaMotivos.LugarDaFase(processo, fase) : ProcessoParaMotivos.LugarDaEtapa(processo, etapa!);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = ProcessoParaMotivos.Escolha(processo, lugar, origem, versaoDeEntrada: 1);

        Retirar(processo, [escolhido], lugar, origem).Value.Should().BeSameAs(escolhido);
    }

    [Fact(DisplayName = "Retirar do lugar tira só a escolha daquele lugar, e a do mesmo motivo em outro lugar fica")]
    public void Retira_SoDoLugarPedido()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso naFase = ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), origem, versaoDeEntrada: 1);
        MotivoDoProcesso naEtapa = ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), origem, versaoDeEntrada: 1);

        Retirar(processo, [naFase, naEtapa], naEtapa.Lugar, origem).Value.Should().BeSameAs(naEtapa);
    }

    [Fact(DisplayName = "Motivo escolhido só em outro lugar é recusado como não escolhido")]
    public void EscolhidoEmOutroLugar_NaoEscolhido()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso naFase = ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), origem, versaoDeEntrada: 1);

        Retirar(processo, [naFase], ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), origem)
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEscolhido);
    }

    [Theory(DisplayName = "O lugar inexistente, fase ou etapa, é recusado como não encontrado, e não como motivo não escolhido")]
    [InlineData(true)]
    [InlineData(false)]
    public void LugarInexistente_NaoEncontrado(bool ehFase)
    {
        LugarDaDecisao lugar = ehFase ? LugarDaDecisao.DaFase(Guid.CreateVersion7()) : LugarDaDecisao.DaEtapa(Guid.CreateVersion7());

        Retirar(ProcessoParaMotivos.Rascunho(), [], lugar, Motivo()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
    }

    [Fact(DisplayName = "O bloqueio do processo publicado é recusado antes da escolha ausente")]
    public void MutacaoBloqueada_PrecedeAEscolhaAusente()
    {
        ProcessoSeletivo publicado = ProcessoParaMotivos.Publicado();

        Retirar(publicado, [], ProcessoParaMotivos.LugarDaFase(publicado, Homologacao), Motivo()).Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "O lugar inexistente é recusado antes do bloqueio do processo publicado")]
    public void LugarInexistente_PrecedeAMutacaoBloqueada()
    {
        Retirar(ProcessoParaMotivos.Publicado(), [], LugarDaDecisao.DaFase(Guid.CreateVersion7()), Motivo()).Error!.Code
            .Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
    }

    [Fact(DisplayName = "A escolha presente num processo publicado não é retirada")]
    public void PublicadoSemSessao_Bloqueia()
    {
        ProcessoSeletivo publicado = ProcessoParaMotivos.Publicado();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(publicado, Homologacao);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = ProcessoParaMotivos.Escolha(publicado, lugar, origem, versaoDeEntrada: 1);

        Retirar(publicado, [escolhido], lugar, origem).Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }
}
