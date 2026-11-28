namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fada;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A declaração do Fator de Atenuação das Desigualdades de Acesso (FADA) contra a configuração dele: o processo que o aplica precisa
/// da configuração, e o que não o aplica não pode tê-la. Quem recebe o pedido ou o documento só sabe se há configuração, e a
/// mesma recusa vale para a raiz e para a leitura.
/// </summary>
public static class DeclaracaoDoFada
{
    /// <summary>A recusa do pedido que não informa se o processo aplica o FADA.</summary>
    public static FieldError RecusaDaAusencia() => new(
        "aplica",
        new DomainError(CodigosDeValidacao.Obrigatorio, "Informe se o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA)."));

    /// <summary>A recusa da declaração que contradiz a configuração, ou nulo quando as duas concordam.</summary>
    public static DomainError? Recusa(bool aplica, bool temConfiguracao) => (aplica, temConfiguracao) switch
    {
        (true, false) => new DomainError(
            ProcessoSeletivoErrorCodes.FadaAplicaSemConfiguracao,
            "O processo que aplica o Fator de Atenuação das Desigualdades de Acesso (FADA) precisa da configuração do FADA."),
        (false, true) => new DomainError(
            ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao,
            "O processo que não aplica o Fator de Atenuação das Desigualdades de Acesso (FADA) não pode ter a configuração do FADA."),
        _ => null,
    };
}
