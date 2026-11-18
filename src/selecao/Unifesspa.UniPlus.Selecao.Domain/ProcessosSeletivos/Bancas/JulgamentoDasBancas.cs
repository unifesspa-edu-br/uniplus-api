namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Quem julga o quê: o tipo de banca que julga os recursos.</summary>
public static class JulgamentoDasBancas
{
    public static bool EhDeRecursos(string codigoDoTipo) =>
        string.Equals(codigoDoTipo, TipoBancaCodigo.AnaliseDeRecursos, StringComparison.Ordinal);
}
