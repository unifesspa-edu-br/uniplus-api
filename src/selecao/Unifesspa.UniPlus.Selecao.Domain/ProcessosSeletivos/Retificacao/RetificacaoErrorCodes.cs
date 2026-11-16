namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

/// <summary>Os códigos de recusa das condições da retificação (UNI-REQ-0211, UNI-REQ-0212).</summary>
public static class RetificacaoErrorCodes
{
    /// <summary>A fase concluída mudou: o que ela declara fica integral.</summary>
    public const string FaseConcluidaAlterada = "Retificacao.FaseConcluidaAlterada";

    /// <summary>A fase que já começou teve o início alterado.</summary>
    public const string InicioDeFaseIniciadaAlterado = "Retificacao.InicioDeFaseIniciadaAlterado";

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
