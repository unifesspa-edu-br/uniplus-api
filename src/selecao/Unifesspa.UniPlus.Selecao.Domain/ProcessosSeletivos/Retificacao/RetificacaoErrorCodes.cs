namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

/// <summary>Os códigos de recusa das condições da retificação (UNI-REQ-0211, UNI-REQ-0212).</summary>
public static class RetificacaoErrorCodes
{
    /// <summary>A fase concluída mudou: o que ela declara fica integral.</summary>
    public const string FaseConcluidaAlterada = "Retificacao.FaseConcluidaAlterada";

    /// <summary>O término da fase sem definitivo, que já concluiu, foi prorrogado depois que uma fase posterior começou.</summary>
    public const string ProrrogacaoComFasePosteriorIniciada = "Retificacao.ProrrogacaoComFasePosteriorIniciada";

    /// <summary>A fase que já começou teve o início alterado.</summary>
    public const string InicioDeFaseIniciadaAlterado = "Retificacao.InicioDeFaseIniciadaAlterado";

    /// <summary>O formulário que já recebeu envio de candidato saiu do documento.</summary>
    public const string FormularioComEnvioNaoSai = "Retificacao.FormularioComEnvioNaoSai";

    /// <summary>O formulário obrigatório que já recebeu envio passou a facultativo.</summary>
    public const string FormularioObrigatorioComEnvioNaoViraFacultativo = "Retificacao.FormularioObrigatorioComEnvioNaoViraFacultativo";

    /// <summary>A oferta de curso em que há inscrição enviada saiu do documento.</summary>
    public const string OfertaComInscricaoNaoSai = "Retificacao.OfertaComInscricaoNaoSai";

    /// <summary>A modalidade em que há inscrição enviada saiu da oferta.</summary>
    public const string ModalidadeConcorridaNaoSai = "Retificacao.ModalidadeConcorridaNaoSai";

    /// <summary>O valor da taxa mudou, ou o processo passou de gratuito a cobrado ou de cobrado a gratuito, havendo inscrição enviada.</summary>
    public const string TaxaComInscricaoNaoMuda = "Retificacao.TaxaComInscricaoNaoMuda";

    /// <summary>Os critérios de isenção da taxa mudaram havendo pedido de isenção feito.</summary>
    public const string CriteriosDeIsencaoComPedidoNaoMudam = "Retificacao.CriteriosDeIsencaoComPedidoNaoMudam";

    /// <summary>A etapa que já começou teve o início alterado.</summary>
    public const string InicioDeEtapaIniciadaAlterado = "Retificacao.InicioDeEtapaIniciadaAlterado";

    /// <summary>O término da fase de inscrição foi antecipado; prorrogar é aceito.</summary>
    public const string TerminoDaInscricaoAntecipado = "Retificacao.TerminoDaInscricaoAntecipado";

    /// <summary>O término de uma fase em andamento foi antecipado.</summary>
    public const string TerminoDeFaseEmAndamentoAntecipado = "Retificacao.TerminoDeFaseEmAndamentoAntecipado";

    /// <summary>O término da janela de isenção foi antecipado: a janela não se reduz.</summary>
    public const string JanelaDeIsencaoReduzida = "Retificacao.JanelaDeIsencaoReduzida";

    /// <summary>O término da janela de isenção já encerrada foi alterado: a janela encerrada não reabre.</summary>
    public const string JanelaDeIsencaoEncerradaReaberta = "Retificacao.JanelaDeIsencaoEncerradaReaberta";

    /// <summary>A retificação muda a regra ou os dados de um resultado preliminar publicado, e o bloco não declara o novo ciclo.</summary>
    public const string CicloNovoNaoDeclarado = "Retificacao.CicloNovoNaoDeclarado";

    /// <summary>O bloco declara novo ciclo para um item sem resultado preliminar publicado e com o ciclo ainda aberto.</summary>
    public const string CicloNovoDeItemSemPreliminarAberto = "Retificacao.CicloNovoDeItemSemPreliminarAberto";

    /// <summary>O documento usa, de outro formulário, fato que ele não coletou em todas as versões publicadas.</summary>
    public const string FatoAusenteDeVersaoPublicada = "Retificacao.FatoAusenteDeVersaoPublicada";

    /// <summary>O fato que o formulário de inscrição já coleta saiu do formulário ou mudou de papel, de item para campo de grupo ou de um grupo para outro.</summary>
    public const string FatoJaColetadoRemovido = "Retificacao.FatoJaColetadoRemovido";

    /// <summary>Um eixo do fato já coletado, ou a operação do agregado de grupo já usado, mudou em relação ao que a versão publicada congelou.</summary>
    public const string EixoDoFatoJaColetadoAlterado = "Retificacao.EixoDoFatoJaColetadoAlterado";
}
