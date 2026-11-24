namespace Unifesspa.UniPlus.Selecao.TestesCompartilhados;

internal sealed record EntradaDeConfrontoDoLimite(
    string Nome, int Valor, ClasseDeConfrontoDoLimite Classe, string? Motivo = null);
