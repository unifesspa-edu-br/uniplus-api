namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

public static class PendenciasDaDistribuicaoDeVagas
{
    /// <summary>
    /// Os cursos de um processo ofertam o mesmo conjunto de cotas da lei. A opção pelas cotas é do
    /// candidato no processo, e não por curso: num processo em que um curso oferta cota e outro não,
    /// quem optou pelas cotas perderia, no curso sem cota, a ação afirmativa que lá lhe caberia.
    /// </summary>
    internal static DomainError? PendenciaDasCotasDaLeiEntreOfertas(EstadoDaConfiguracao estado)
    {
        List<HashSet<string>> cotasPorOferta = [.. estado.DistribuicaoVagas.Select(static d => new HashSet<string>(
            d.Modalidades.Where(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada).Select(static m => m.Codigo),
            StringComparer.Ordinal))];
        return cotasPorOferta.Count > 1 && cotasPorOferta.Skip(1).Any(c => !c.SetEquals(cotasPorOferta[0]))
            ? new DomainError(
                ProcessoSeletivoErrorCodes.CotasDaLeiDiferentesEntreOfertas,
                "Os cursos do processo ofertam conjuntos diferentes de cotas da lei; todos os cursos de um processo ofertam as mesmas cotas.")
            : null;
    }

    /// <summary>
    /// A reserva de vagas para pessoa com deficiência de ampla concorrência é de quem não optou pelas
    /// cotas da lei (UNI-REQ-0142). Num processo que oferta essa reserva e alguma cota da lei, quem
    /// optou pelas cotas só tem reserva de pessoa com deficiência se o processo também ofertar a cota
    /// da lei para ela.
    /// </summary>
    internal static DomainError? PendenciaDaReservaPcdSemCotaPcdDaLei(EstadoDaConfiguracao estado)
    {
        HashSet<string> ofertadas = new(RegrasDaDistribuicaoDeVagas.ModalidadesOfertadas(estado).Select(static m => m.Codigo), StringComparer.Ordinal);
        bool ofertaCotaDaLei = RegrasDaDistribuicaoDeVagas.ModalidadesOfertadas(estado).Exists(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada);
        return ofertaCotaDaLei
            && ofertadas.Contains(ModalidadesFederaisLei12711.AcPcd)
            && !ofertadas.Contains(ModalidadesFederaisLei12711.LiPcd)
            && !ofertadas.Contains(ModalidadesFederaisLei12711.LbPcd)
            ? new DomainError(
                ProcessoSeletivoErrorCodes.ReservaPcdSemCotaPcdDaLei,
                $"O processo oferta {ModalidadesFederaisLei12711.AcPcd} e cota da lei, mas nenhuma cota da lei para pessoa com deficiência; "
                + $"oferte {ModalidadesFederaisLei12711.LiPcd} ou {ModalidadesFederaisLei12711.LbPcd}, para que quem optou pelas cotas tenha reserva.")
            : null;
    }
}
