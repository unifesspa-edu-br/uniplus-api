namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class PendenciasDaDistribuicaoDeVagas
{
    /// <summary>
    /// Os cursos de um processo ofertam o mesmo conjunto de cotas da lei. A opção pelas cotas é do
    /// candidato no processo, e não por curso: num processo em que um curso oferta cota e outro não,
    /// quem optou pelas cotas perderia, no curso sem cota, a ação afirmativa que lá lhe caberia.
    /// </summary>
    internal static DomainError? PendenciaDasCotasDaLeiEntreOfertas(ConfiguracaoDeclarada configuracao)
    {
        List<HashSet<string>> cotasPorOferta = [.. configuracao.Vagas.DistribuicaoVagas.Select(static d => new HashSet<string>(
            d.Modalidades.Where(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada).Select(static m => m.Codigo),
            StringComparer.Ordinal))];
        return cotasPorOferta.Count > 1 && cotasPorOferta.Skip(1).Any(c => !c.SetEquals(cotasPorOferta[0]))
            ? new DomainError(
                ProcessoSeletivoErrorCodes.CotasDaLeiDiferentesEntreOfertas,
                "Os cursos do processo ofertam conjuntos diferentes de cotas da lei; todos os cursos de um processo ofertam as mesmas cotas.")
            : null;
    }
}
