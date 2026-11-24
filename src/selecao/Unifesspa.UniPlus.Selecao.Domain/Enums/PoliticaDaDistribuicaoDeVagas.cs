namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public static class PoliticaDaDistribuicaoDeVagas
{
    public static bool EhRegraFederal(string codigo) =>
        CatalogoDe<RegraDeDistribuicaoDeVagas>.TentarObter(codigo, out RegraDeDistribuicaoDeVagas regra)
        && regra is RegraDeDistribuicaoDeVagas.Lei12711 or RegraDeDistribuicaoDeVagas.Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia;

    public static bool EhQuadroFixo(string codigo) =>
        CatalogoDe<RegraDeDistribuicaoDeVagas>.TentarObter(codigo, out RegraDeDistribuicaoDeVagas regra)
        && regra is RegraDeDistribuicaoDeVagas.Institucional or RegraDeDistribuicaoDeVagas.PorAcrescimoIndigenaEQuilombola or RegraDeDistribuicaoDeVagas.ComAmplaConcorrenciaEPessoaComDeficiencia;

    public static string? ModoDeMontagemDe(string codigo) => EhRegraFederal(codigo)
        ? ModoDeMontagemDoQuadroDeVagas.LeiDeCotas.Codigo()
        : EhQuadroFixo(codigo) ? ModoDeMontagemDoQuadroDeVagas.QuadroFixadoNoEdital.Codigo() : null;

    public static IReadOnlyList<string>? RolFechado(string codigo) =>
        CatalogoDe<RegraDeDistribuicaoDeVagas>.TentarObter(codigo, out RegraDeDistribuicaoDeVagas regra) ? RolFechado(regra) : null;

    private static IReadOnlyList<string>? RolFechado(RegraDeDistribuicaoDeVagas regra) => regra switch
    {
        RegraDeDistribuicaoDeVagas.Lei12711 => ModalidadesFederaisLei12711.CodigosComAc,
        RegraDeDistribuicaoDeVagas.Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia => [.. ModalidadesFederaisLei12711.CodigosComAc, ModalidadesFederaisLei12711.AcPcd],
        RegraDeDistribuicaoDeVagas.PorAcrescimoIndigenaEQuilombola => ["AC_I", "AC_Q"],
        RegraDeDistribuicaoDeVagas.ComAmplaConcorrenciaEPessoaComDeficiencia => [ModalidadesFederaisLei12711.Ac, ModalidadesFederaisLei12711.AcPcd],
        _ => null,
    };
}
