namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>Mensagens dos defeitos de forma das condições do enquadramento.</summary>
public static class MensagensDoEnquadramento
{
    /// <summary>Devolve a mensagem, ou null quando o código não representa um defeito de forma conhecido.</summary>
    public static string? DoDefeitoDeForma(string codigo) => codigo switch
    {
        "CondicaoDnf.FatoObrigatorio" => "Informe o fato da condição.",
        "CondicaoDnf.OperadorInvalido" => "O operador informado não é reconhecido.",
        "CondicaoDnf.FormaIncoerenteComOperador" => "O formato do valor não corresponde ao operador informado.",
        _ => null,
    };
}
