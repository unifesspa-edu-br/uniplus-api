namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Os papéis de banca que julgam recurso: o único lugar que diz isso. A recusa de candidatos atendidos na banca que julga
/// recurso e a exigência da votação do recurso decidem por este conjunto, e nunca pelo código do tipo de banca.
/// </summary>
internal static class PapeisQueJulgamRecurso
{
    private static readonly FrozenSet<PapelDaBanca> Papeis = new[] { PapelDaBanca.JulgaAPrimeiraInstancia }.ToFrozenSet();

    internal static bool Contem(PapelDaBanca papel) => Papeis.Contains(papel);
}
