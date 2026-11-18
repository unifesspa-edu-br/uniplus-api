namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As partes de um vínculo válido, para os testes que partem dele e alteram só o que querem
/// provar. Dados fictícios.
/// </summary>
internal static class VinculoDeTeste
{
    public const string CpfValido = "33287151002";

    public static Cpf CpfDeTeste() => Valor(Cpf.Criar(CpfValido));

    public static IdentificacaoDoDiscente Identificacao() =>
        Valor(IdentificacaoDoDiscente.Criar(50231, "20142600001", CpfDeTeste(), "ANA EXEMPLO DE SOUZA"));

    public static CursoCopiadoDoSigaa Curso() =>
        Valor(CursoCopiadoDoSigaa.Criar(20, "DIREITO", "12078", 64, "INSTITUTO DE DIREITO"));

    public static SituacaoAcademicaCopiada Situacao() =>
        Valor(SituacaoAcademicaCopiada.Criar(3, "CONCLUÍDO", null));

    public static PeriodoIngresso Ingresso() => Valor(PeriodoIngresso.Criar(2014, 2));

    public static VinculoDiscenteCopiado DadosCopiados() =>
        Valor(VinculoDiscenteCopiado.Criar(Identificacao(), "G", Curso(), Situacao(), Ingresso()));

    public static T Valor<T>(Result<T> resultado)
        where T : class =>
        resultado.Match(valor => valor, erro => throw new InvalidOperationException(erro.Code));
}
