namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// A inclusão de um motivo numa fase ou etapa (UNI-REQ-0285): livre no rascunho; depois da publicação o processo não
/// aceita escolha direta, e a retificação inclui pelo documento (UNI-REQ-0124).
/// </summary>
public sealed class InclusaoDeMotivoTests
{
    private const string Homologacao = "HOMOLOGACAO_INSCRICOES";

    private static MotivoDeDecisao Desfavoravel() =>
        ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "SEM_RESULTADO_INEP", SentidoDoMotivo.Desfavoravel);

    private static object CopiaEsperadaNoRascunho(MotivoDeDecisao origem, LugarDaDecisao lugar) => new
    {
        MotivoDeDecisaoOrigemId = origem.Id,
        Lugar = lugar,
        Tipo = TipoMotivoDeDecisao.HomologacaoDasInscricoes,
        Descricao = origem.Descricao,
        Subclassificacao = (string?)null,
        Sentido = SentidoDoMotivo.Desfavoravel,
        JustificativaDaInclusao = (string?)null,
    };

    [Fact(DisplayName = "No rascunho, a inclusão copia por valor o motivo do cadastro, no lugar e na versão 1")]
    public void Rascunho_Inclui_CopiaPorValor()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);
        MotivoDeDecisao origem = Desfavoravel();

        MotivoDeDecisaoDoProcesso copia = Incluir(processo, lugar, origem).Value!;

        copia.Should().BeEquivalentTo(CopiaEsperadaNoRascunho(origem, lugar));
        copia.Codigo.Valor.Should().Be("SEM_RESULTADO_INEP");
        copia.Vigencia.VersaoDoEditalDeEntrada.Should().Be(1);
        copia.Vigencia.VersaoDoEditalDeSaida.Should().BeNull();
    }

    [Fact(DisplayName = "A inclusão na etapa guarda a etapa como lugar")]
    public void Rascunho_NaEtapa_GuardaAEtapa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva");

        Incluir(processo, lugar, Desfavoravel()).Value!.Lugar.Should().Be(lugar);
    }

    [Fact(DisplayName = "Alterar o cadastro depois da escolha mantém a descrição copiada no processo")]
    public void Copia_OrigemAlterada_ConservaADescricaoEscolhida()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = Desfavoravel();
        string descricaoEscolhida = origem.Descricao;
        MotivoDeDecisaoDoProcesso copia = Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), origem).Value!;

        origem.Atualizar("Outra redação do motivo.").IsSuccess.Should().BeTrue();

        origem.Descricao.Should().NotBe(descricaoEscolhida);
        copia.Descricao.Should().Be(descricaoEscolhida);
    }

    [Fact(DisplayName = "A fase que o processo não tem é recusada como não encontrada")]
    public void FaseInexistente_Recusa()
    {
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(Guid.CreateVersion7());

        Incluir(ProcessoParaMotivos.Rascunho(), lugar, Desfavoravel()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
    }

    [Fact(DisplayName = "A etapa que o processo não tem é recusada como não encontrada, e o id de fase não vale como etapa")]
    public void EtapaInexistente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        Guid idDeFase = processo.CronogramaFases.First().FaseCanonicaOrigemId;

        Incluir(processo, LugarDaDecisao.DaEtapa(Guid.CreateVersion7()), Desfavoravel()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
        Incluir(processo, LugarDaDecisao.DaEtapa(idDeFase), Desfavoravel()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
    }

    [Fact(DisplayName = "O lugar inexistente é recusado antes do bloqueio do processo publicado")]
    public void LugarInexistente_PrecedeAMutacaoBloqueada()
    {
        ProcessoSeletivo publicado = ProcessoParaMotivos.Publicado();
        LugarDaDecisao existente = ProcessoParaMotivos.LugarDaFase(publicado, Homologacao);

        Incluir(publicado, LugarDaDecisao.DaFase(Guid.CreateVersion7()), Desfavoravel()).Error!.Code
            .Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
        Incluir(publicado, existente, Desfavoravel()).Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Motivo desativado não entra em nova escolha")]
    public void MotivoInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        origem.Desativar();

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), origem).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoInativo);
    }

    [Fact(DisplayName = "O mesmo motivo não é escolhido duas vezes no mesmo lugar, fase ou etapa")]
    public void MesmoMotivoNoMesmoLugar_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = Desfavoravel();

        foreach (LugarDaDecisao lugar in new[] { ProcessoParaMotivos.LugarDaFase(processo, Homologacao), ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva") })
        {
            MotivoDeDecisaoDoProcesso primeiro = Incluir(processo, lugar, origem).Value!;

            Incluir(processo, lugar, origem, escolhaAtual: [primeiro]).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JaEscolhido);
        }
    }

    [Fact(DisplayName = "O mesmo motivo pode ser escolhido em mais de um lugar: na fase e nas etapas")]
    public void MesmoMotivoEmLugaresDistintos_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = Desfavoravel();
        MotivoDeDecisaoDoProcesso naFase = Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), origem).Value!;
        MotivoDeDecisaoDoProcesso naEtapa = Incluir(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), origem, escolhaAtual: [naFase]).Value!;

        Incluir(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva"), origem, escolhaAtual: [naFase, naEtapa]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Processo publicado não inclui motivo")]
    public void Publicado_NaoInclui()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Publicado();

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), Desfavoravel())
            .Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    private static Result<MotivoDeDecisaoDoProcesso> Incluir(
        ProcessoSeletivo processo,
        LugarDaDecisao lugar,
        MotivoDeDecisao origem,
        IReadOnlyCollection<MotivoDeDecisaoDoProcesso>? escolhaAtual = null) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(processo, lugar, escolhaAtual ?? [], origem));
}
