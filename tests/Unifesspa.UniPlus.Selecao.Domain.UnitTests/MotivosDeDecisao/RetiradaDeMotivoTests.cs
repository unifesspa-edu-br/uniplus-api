namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A retirada de um motivo de uma fase ou etapa: no rascunho e na inclusão ainda pendente da sessão de retificação, sim; o
/// que já consta do edital publicado não sai (UNI-REQ-0124).
/// </summary>
public sealed class RetiradaDeMotivoTests
{
    private const string Homologacao = "HOMOLOGACAO_INSCRICOES";

    private static MotivoDeDecisao Motivo() =>
        ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", SentidoDoMotivo.Desfavoravel);

    private static Result<MotivoDoProcesso> Retirar(
        ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhaAtual, LugarDaDecisao lugar, MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Retirar(
            processo, escolhaAtual, lugar, origem.Id, processo.Rascunho is null ? PrecondicaoIfMatch.Ausente : PrecondicaoIfMatch.Curinga);

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

    [Fact(DisplayName = "A escolha ausente é recusada antes da precondição, mesmo no processo publicado sem sessão")]
    public void EscolhaAusente_PrecedeAMutacaoBloqueada()
    {
        ProcessoSeletivo publicado = ProcessoParaMotivos.Publicado();

        Retirar(publicado, [], ProcessoParaMotivos.LugarDaFase(publicado, Homologacao), Motivo()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEscolhido);
    }

    [Fact(DisplayName = "A escolha presente num processo publicado sem sessão de retificação não é retirada")]
    public void PublicadoSemSessao_Bloqueia()
    {
        ProcessoSeletivo publicado = ProcessoParaMotivos.Publicado();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(publicado, Homologacao);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = ProcessoParaMotivos.Escolha(publicado, lugar, origem, versaoDeEntrada: 1);

        Retirar(publicado, [escolhido], lugar, origem).Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Retirar motivo já publicado é recusado, mesmo sob retificação")]
    public void Publicado_Retirar_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso publicado = ProcessoParaMotivos.Escolha(processo, lugar, origem, versaoDeEntrada: processo.Rascunho!.NumeroVersaoBase);

        Retirar(processo, [publicado], lugar, origem).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao);
    }

    [Fact(DisplayName = "Sob retificação, a inclusão ainda pendente pode sair antes do fechamento e avança a revisão da sessão")]
    public void Retificacao_RetiraInclusaoPendente()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso pendente = ProcessoParaMotivos.Escolha(processo, lugar, origem, versaoDeEntrada: processo.Rascunho!.NumeroDaVersaoEmPreparo);
        int revisao = processo.Rascunho.Revisao;

        Retirar(processo, [pendente], lugar, origem).IsSuccess.Should().BeTrue();
        processo.Rascunho.Revisao.Should().Be(revisao + 1);
    }

    [Fact(DisplayName = "A escolha de versão posterior à em preparo, que sobrou de sessão descartada, também sai")]
    public void Retificacao_RetiraOrfao()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso orfao = ProcessoParaMotivos.Escolha(processo, lugar, origem, versaoDeEntrada: processo.Rascunho!.NumeroDaVersaoEmPreparo + 1);

        Retirar(processo, [orfao], lugar, origem).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A recusa da retirada não avança a revisão da sessão")]
    public void Recusa_NaoAvancaARevisao()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso publicado = ProcessoParaMotivos.Escolha(processo, lugar, origem, versaoDeEntrada: 1);
        int revisao = processo.Rascunho!.Revisao;

        Retirar(processo, [publicado], lugar, origem).IsFailure.Should().BeTrue();
        processo.Rascunho.Revisao.Should().Be(revisao);
    }
}
