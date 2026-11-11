namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A escolha dos motivos pelo processo (UNI-REQ-0285): livre no rascunho; depois da publicação, só
/// pela retificação, com justificativa e antes do início da avaliação daquela decisão, e sem
/// retirar o que foi publicado (UNI-REQ-0124).
/// </summary>
public sealed class EscolhaDosMotivosTests
{
    private static readonly DateTimeOffset AntesDaInscricao = ProcessoParaMotivos.InicioDaInscricao.AddDays(-10);

    [Fact(DisplayName = "No rascunho, a inclusão copia por valor o motivo do cadastro, sem justificativa")]
    public void Rascunho_Inclui_CopiaPorValor()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "SEM_RESULTADO_INEP", SentidoDoMotivo.Desfavoravel);

        Result<MotivoDoProcesso> resultado = Incluir(ProcessoParaMotivos.Rascunho(), origem, justificativa: "ignorada no rascunho");

        MotivoDoProcesso copia = resultado.Value!;
        copia.Should().BeEquivalentTo(new
        {
            MotivoDeDecisaoOrigemId = origem.Id,
            Tipo = TipoMotivoDeDecisao.HomologacaoDasInscricoes,
            Descricao = origem.Descricao,
            Subclassificacao = (string?)null,
            Sentido = SentidoDoMotivo.Desfavoravel,
            Congelado = false,
            JustificativaDaInclusao = (string?)null,
        });
        copia.Codigo.Valor.Should().Be("SEM_RESULTADO_INEP");
    }

    [Fact(DisplayName = "Mudar o cadastro depois não muda a cópia congelada")]
    public void CopiaCongelada_NaoAcompanhaOCadastro()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel);
        MotivoDoProcesso copia = Incluir(ProcessoParaMotivos.Rascunho(), origem).Value!;
        copia.Congelar();

        origem.Atualizar("Outra redação do motivo.");
        copia.Recopiar(origem);

        copia.Descricao.Should().Be("Descrição do motivo RENDA_ACIMA_DO_LIMITE");
    }

    [Fact(DisplayName = "Motivo desativado não entra em nova escolha")]
    public void MotivoInativo_Recusa()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        origem.Desativar();

        Incluir(ProcessoParaMotivos.Rascunho(), origem).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoInativo);
    }

    [Fact(DisplayName = "O mesmo motivo não é escolhido duas vezes")]
    public void JaEscolhido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoDoProcesso primeiro = Incluir(processo, origem).Value!;

        Incluir(processo, origem, escolhidos: [primeiro]).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JaEscolhido);
    }

    [Fact(DisplayName = "Processo publicado sem sessão de retificação não inclui motivo")]
    public void PublicadoSemSessao_ExigeRetificacao()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);

        Incluir(ProcessoParaMotivos.Publicado(), origem, justificativa: "Motivo previsto em norma nova.")
            .Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Na retificação, a inclusão antes do início da avaliação é aceita, guarda a justificativa e avança a revisão da sessão")]
    public void Retificacao_AntesDaAvaliacao_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        int revisao = processo.Rascunho!.Revisao;
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "SEM_RESULTADO_INEP", SentidoDoMotivo.Desfavoravel);

        Result<MotivoDoProcesso> resultado = Incluir(processo, origem, justificativa: "  Retorno do INEP passou a ser exigido.  ", agora: ProcessoParaMotivos.InicioDaHomologacao.AddDays(-1));

        resultado.Value!.JustificativaDaInclusao.Should().Be("Retorno do INEP passou a ser exigido.");
        processo.Rascunho.Revisao.Should().Be(revisao + 1);
    }

    [Theory(DisplayName = "Na retificação, a inclusão sem justificativa é recusada")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Retificacao_SemJustificativa_Recusa(string? justificativa)
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", SentidoDoMotivo.Desfavoravel);

        Incluir(ProcessoParaMotivos.EmRetificacao(), origem, justificativa)
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JustificativaObrigatoria);
    }

    [Fact(DisplayName = "Na retificação, a justificativa acima do tamanho é recusada")]
    public void Retificacao_JustificativaLonga_Recusa()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", SentidoDoMotivo.Desfavoravel);

        Incluir(ProcessoParaMotivos.EmRetificacao(), origem, new string('j', EscolhaDosMotivos.JustificativaMaxLength + 1))
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JustificativaTamanho);
    }

    [Theory(DisplayName = "Na retificação, a inclusão a partir do início da janela da fase da decisão é recusada")]
    [InlineData(0)]
    [InlineData(5)]
    public void Retificacao_AvaliacaoIniciada_Recusa(int diasDepoisDoInicio)
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "SEM_RESULTADO_INEP", SentidoDoMotivo.Desfavoravel);

        Incluir(ProcessoParaMotivos.EmRetificacao(), origem, "Retorno do INEP passou a ser exigido.", ProcessoParaMotivos.InicioDaHomologacao.AddDays(diasDepoisDoInicio))
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.AvaliacaoIniciada);
    }

    [Fact(DisplayName = "A avaliação de outro tipo não impede a inclusão: o início é por decisão")]
    public void Retificacao_OutroTipoIniciado_Aceita()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "SEM_RESULTADO_INEP", SentidoDoMotivo.Desfavoravel);

        // A janela da isenção (fase de inscrição) já começou; a da homologação, não.
        Incluir(ProcessoParaMotivos.EmRetificacao(), origem, "Retorno do INEP passou a ser exigido.", ProcessoParaMotivos.InicioDaInscricao.AddDays(1))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "No rascunho, retirar motivo escolhido é aceito")]
    public void Rascunho_Retira()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDoProcesso escolhido = Incluir(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel)).Value!;

        EscolhaDosMotivos.Retirar(processo, escolhido, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Retirar motivo já publicado é recusado, mesmo sob retificação")]
    public void Publicado_Retirar_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        MotivoDoProcesso escolhido = Incluir(ProcessoParaMotivos.Rascunho(), ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel)).Value!;
        escolhido.Congelar();

        EscolhaDosMotivos.Retirar(processo, escolhido, PrecondicaoIfMatch.Curinga)
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao);
    }

    [Fact(DisplayName = "Sob retificação, a inclusão ainda pendente pode sair antes do fechamento")]
    public void Retificacao_RetiraInclusaoPendente()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        MotivoDeDecisao origem = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", SentidoDoMotivo.Desfavoravel);
        MotivoDoProcesso pendente = Incluir(processo, origem, "Norma nova.").Value!;

        EscolhaDosMotivos.Retirar(processo, pendente, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
    }

    private static Result<MotivoDoProcesso> Incluir(
        ProcessoSeletivo processo,
        MotivoDeDecisao origem,
        string? justificativa = null,
        DateTimeOffset? agora = null,
        IReadOnlyCollection<MotivoDoProcesso>? escolhidos = null) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(
            processo,
            escolhidos ?? [],
            origem,
            justificativa,
            processo.Rascunho is null ? PrecondicaoIfMatch.Ausente : PrecondicaoIfMatch.Curinga,
            agora ?? AntesDaInscricao,
            PrimeiroRegistroDaDecisao: null));
}
