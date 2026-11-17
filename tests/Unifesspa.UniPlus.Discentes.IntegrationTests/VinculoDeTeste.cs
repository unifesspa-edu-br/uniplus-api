namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Vínculos válidos para os testes de persistência. Dados fictícios.
/// </summary>
internal static class VinculoDeTeste
{
    public const string CpfValido = "52998224725";
    public const string OutroCpfValido = "11144477735";

    public static VinculoDiscente Novo(long idDeOrigem, string cpf = CpfValido, string nome = "DISCENTE DE TESTE") =>
        VinculoDiscente.Criar(Snapshot(idDeOrigem, cpf, nome));

    public static VinculoSincronizavel Sincronizavel(
        long idDeOrigem,
        string nome = "DISCENTE DE TESTE",
        string? resumo = null) =>
        new(Novo(idDeOrigem, nome: nome), resumo ?? $"resumo-de-{idDeOrigem}");

    private static VinculoDiscenteSnapshot Snapshot(long idDeOrigem, string cpf, string nome)
    {
        IdentificacaoDoDiscente identificacao = Valor(
            IdentificacaoDoDiscente.Criar(idDeOrigem, $"2020{idDeOrigem:D8}", Valor(Cpf.Criar(cpf)), nome));

        return Valor(VinculoDiscenteSnapshot.Criar(
            identificacao,
            "G",
            Valor(CursoSigaaSnapshot.Criar(42, "CIÊNCIA DA COMPUTAÇÃO", "1269997", 12, "INSTITUTO DE CIENCIAS EXATAS")),
            Valor(SituacaoAcademicaSnapshot.Criar(1, "ATIVO", "ATV")),
            Valor(PeriodoIngresso.Criar(2020, 1))));
    }

    private static T Valor<T>(Result<T> resultado)
        where T : class =>
        resultado.Match(valor => valor, erro => throw new InvalidOperationException(erro.Code));
}
