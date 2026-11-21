namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A cópia do tipo que a banca já gravada mantém quando a declaração a substitui sem trocar o tipo. Quem declara as bancas só lê
/// o tipo no cadastro para a banca nova ou para a que troca de tipo: o tipo que saiu do cadastro não afeta a banca que já
/// tem a cópia.
/// </summary>
public static class CopiaMantidaDaBanca
{
    /// <summary>
    /// A cópia da banca da fase de mesmo id e mesmo tipo; ausente quando a declaração não substitui banca da fase ou troca de tipo.
    /// </summary>
    public static TipoDeBancaCopiado? De(IEnumerable<BancaRequerida> gravadas, Guid? idDaBancaQueSubstitui, Guid tipoBancaId)
    {
        ArgumentNullException.ThrowIfNull(gravadas);

        return Achar(gravadas.Select(static banca => (banca.Id, banca.Tipo)), idDaBancaQueSubstitui, tipoBancaId);
    }

    /// <summary>
    /// A cópia da banca da etapa de mesmo id e mesmo tipo; ausente quando a declaração não substitui banca da etapa ou troca de tipo.
    /// </summary>
    public static TipoDeBancaCopiado? De(IEnumerable<BancaDaEtapa> gravadas, Guid? idDaBancaQueSubstitui, Guid tipoBancaId)
    {
        ArgumentNullException.ThrowIfNull(gravadas);

        return Achar(gravadas.Select(static banca => (banca.Id, banca.Tipo)), idDaBancaQueSubstitui, tipoBancaId);
    }

    private static TipoDeBancaCopiado? Achar(IEnumerable<(Guid Id, TipoDeBancaCopiado Tipo)> gravadas, Guid? idDaBancaQueSubstitui, Guid tipoBancaId) =>
        idDaBancaQueSubstitui is { } id
            ? gravadas.Where(banca => banca.Id == id && banca.Tipo.OrigemId == tipoBancaId).Select(static banca => banca.Tipo).FirstOrDefault()
            : null;
}
