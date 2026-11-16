namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

/// <summary>Os códigos de recusa das condições da retificação (UNI-REQ-0211, UNI-REQ-0212).</summary>
public static class RetificacaoErrorCodes
{
    /// <summary>A fase concluída mudou: o que ela declara fica integral.</summary>
    public const string FaseConcluidaAlterada = "Retificacao.FaseConcluidaAlterada";

    /// <summary>A fase que já começou teve o início alterado.</summary>
    public const string InicioDeFaseIniciadaAlterado = "Retificacao.InicioDeFaseIniciadaAlterado";

    /// <summary>O formulário que já recebeu envio de candidato saiu do documento.</summary>
    public const string FormularioComEnvioNaoSai = "Retificacao.FormularioComEnvioNaoSai";

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
}
