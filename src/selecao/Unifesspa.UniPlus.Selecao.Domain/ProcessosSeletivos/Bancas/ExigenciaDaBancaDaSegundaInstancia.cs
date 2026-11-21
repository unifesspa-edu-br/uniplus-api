namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A banca de 2ª instância que o recurso exige e a que sobra. O recurso que admite instância superior exige, na fase, banca da
/// fase com o papel de julgar a 2ª instância e, na etapa, banca com esse papel na etapa ou na fase em que ela acontece; a banca
/// de 1ª instância não supre a falta. A banca de 2ª instância só é prevista onde há recurso que ela julgue: na etapa, o da própria
/// etapa; na fase, o da fase e o das etapas dela que não têm banca de 2ª instância própria. As mensagens citam a fase pelo código,
/// a etapa pela posição, a banca pelo rótulo do tipo e pela posição entre as do mesmo tipo e o papel pelo rótulo fixo.
/// </summary>
internal static class ExigenciaDaBancaDaSegundaInstancia
{
    private const string OQueAFaseJulga =
        "A banca de 2ª instância da fase julga o recurso da fase e os das etapas dela que não têm banca de 2ª instância própria.";

    private const string OQueAFaseAdmitiriaParaJulgar =
        "declare que o recurso da fase, ou o de uma etapa dela sem banca de 2ª instância própria, admite instância superior.";

    /// <summary>A etapa só precisa de banca de 2ª instância quando um recurso dela admite instância superior e nem ela nem a fase a têm.</summary>
    internal static bool EtapaSemBancaQueJulgueASegundaInstancia(bool admiteInstanciaSuperior, bool temBancaPropria, bool faseTemBanca) =>
        admiteInstanciaSuperior && !temBancaPropria && !faseTemBanca;

    internal static DomainError FaseSemBancaDaSegundaInstancia(string codigoDaFase) =>
        new(
            BancaErrorCodes.FaseSemBancaDaSegundaInstancia,
            $"A fase '{codigoDaFase}' admite recurso em 2ª instância, e nenhuma banca prevista nela tem o papel '{PapelQueJulga}'. Acrescente à fase uma banca com esse papel, ou declare que o recurso não admite instância superior.");

    internal static DomainError EtapaSemBancaDaSegundaInstancia(int posicao, string? codigoDaFase) =>
        new(
            BancaErrorCodes.EtapaSemBancaDaSegundaInstancia,
            $"A etapa na posição {posicao + 1} admite recurso em 2ª instância, e nem ela nem {DaFase(codigoDaFase)}, em que ela acontece, têm banca com o papel '{PapelQueJulga}'. Acrescente uma banca com esse papel à etapa ou à fase, ou declare que o recurso não admite instância superior.");

    /// <summary>A banca da fase sem recurso para julgar porque nenhum recurso da fase nem das etapas dela admite instância superior.</summary>
    internal static DomainError FaseComBancaSemNenhumRecursoQueAdmita(string codigoDaFase, BancaDeclarada banca) =>
        new(
            BancaErrorCodes.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar,
            $"A fase '{codigoDaFase}' prevê a banca '{banca.Citacao}', com o papel '{PapelQueJulga}', e nenhum recurso da fase ou das etapas dela admite instância superior. {OQueAFaseJulga} Retire essa banca, ou {OQueAFaseAdmitiriaParaJulgar}");

    /// <summary>A banca da fase sem recurso para julgar porque cada etapa com recurso que admite instância superior já tem a sua banca.</summary>
    internal static DomainError FaseComBancaSoComEtapasQueTemBancaPropria(string codigoDaFase, BancaDeclarada banca) =>
        new(
            BancaErrorCodes.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar,
            $"A fase '{codigoDaFase}' prevê a banca '{banca.Citacao}', com o papel '{PapelQueJulga}', e não há recurso para ela julgar: o recurso da fase não admite instância superior, e cada etapa da fase com recurso que admite instância superior já tem a sua banca de 2ª instância, que o julga. {OQueAFaseJulga} Retire essa banca; ou retire a banca de 2ª instância de uma dessas etapas, para que a da fase julgue o recurso dela; ou {OQueAFaseAdmitiriaParaJulgar}");

    internal static DomainError EtapaComBancaSemRecursoParaJulgar(int posicao, BancaDeclarada banca) =>
        new(
            BancaErrorCodes.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar,
            $"A etapa na posição {posicao + 1} prevê a banca '{banca.Citacao}', com o papel '{PapelQueJulga}', e nenhum recurso da etapa admite instância superior. A banca de 2ª instância da etapa julga só os recursos dela; a do recurso da fase fica na própria fase. Retire essa banca, ou declare que um recurso da etapa admite instância superior.");

    internal static DomainError FaseComMaisDeUmaBanca(string codigo, int quantidade, BancaDeclarada segunda) =>
        new(
            BancaErrorCodes.FaseComMaisDeUmaBancaDaSegundaInstancia,
            $"A fase '{codigo}' prevê {quantidade} bancas com o papel '{PapelQueJulga}', e a 2ª instância é julgada por uma única banca na fase. Mantenha uma e retire as demais, a começar pela banca '{segunda.Citacao}'.");

    internal static DomainError EtapaComMaisDeUmaBanca(int posicao, int quantidade, BancaDeclarada segunda) =>
        new(
            BancaErrorCodes.EtapaComMaisDeUmaBancaDaSegundaInstancia,
            $"A etapa na posição {posicao + 1} prevê {quantidade} bancas com o papel '{PapelQueJulga}', e a 2ª instância é julgada por uma única banca na etapa. Mantenha uma e retire as demais, a começar pela banca '{segunda.Citacao}'.");

    private static string DaFase(string? codigoDaFase) =>
        codigoDaFase is null ? "a fase" : $"a fase '{codigoDaFase}'";

    private static string PapelQueJulga => PapelDaBanca.JulgaASegundaInstancia.Rotulo();
}
