namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using System.Text;

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
        VinculoDiscente.Criar(DadosCopiados(idDeOrigem, cpf, nome));

    /// <summary>
    /// O vínculo pronto para gravar; o resumo é o do <paramref name="conteudo"/> informado, ou
    /// de um conteúdo próprio do vínculo quando não se informa.
    /// </summary>
    public static VinculoSincronizavel Sincronizavel(
        long idDeOrigem,
        string nome = "DISCENTE DE TESTE",
        string? conteudo = null) =>
        new(Novo(idDeOrigem, nome: nome), ResumoDe(conteudo ?? $"conteudo-de-{idDeOrigem}"));

    public static IdDoDiscenteNoSigaa IdNoSigaa(long idDeOrigem) => Valor(IdDoDiscenteNoSigaa.Criar(idDeOrigem));

    private static ResumoSha256 ResumoDe(string conteudo) =>
        ResumoSha256.DoConteudo(Encoding.UTF8.GetBytes(conteudo));

    private static VinculoDiscenteCopiado DadosCopiados(long idDeOrigem, string cpf, string nome)
    {
        IdentificacaoDoDiscente identificacao = Valor(
            IdentificacaoDoDiscente.Criar(idDeOrigem, $"2020{idDeOrigem:D8}", Valor(Cpf.Criar(cpf)), nome));

        return Valor(VinculoDiscenteCopiado.Criar(
            identificacao,
            "G",
            Valor(CursoCopiadoDoSigaa.Criar(42, "CIÊNCIA DA COMPUTAÇÃO", "1269997", 12, "INSTITUTO DE CIENCIAS EXATAS")),
            Valor(SituacaoAcademicaCopiada.Criar(1, "ATIVO", "ATV")),
            Valor(PeriodoIngresso.Criar(2020, 1))));
    }

    private static T Valor<T>(Result<T> resultado)
        where T : class =>
        resultado.Match(valor => valor, erro => throw new InvalidOperationException(erro.Code));
}
