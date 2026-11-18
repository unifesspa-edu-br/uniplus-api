namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A lista completa e ordenada das recusas de cada construção: tudo violado ao mesmo
/// tempo, e cada linha corrige a recusa anterior para revelar a seguinte.
/// </summary>
public sealed class OrdemDasRecusasDoVinculoTests
{
    public static readonly TheoryData<string, long, string, string> Identificacao = new()
    {
        { "VinculoDiscente.IdSigaaInvalido", 0, "", "" },
        { "VinculoDiscente.MatriculaVazia", 1, "", "" },
        { "VinculoDiscente.MatriculaNaoNumerica", 1, "12A", "" },
        { "VinculoDiscente.MatriculaLonga", 1, "123456789012345678901", "" },
        { "VinculoDiscente.NomeVazio", 1, "123", "" },
        { "VinculoDiscente.NomeLongo", 1, "123", "A251" },
    };

    public static readonly TheoryData<string, int, string, string, int, string> Curso = new()
    {
        { "Curso.IdInvalido", 0, "", "E21", 0, "" },
        { "Curso.NomeVazio", 1, "", "E21", 0, "" },
        { "Curso.UnidadeIdInvalido", 1, "N251", "E21", 0, "" },
        { "Curso.UnidadeIdInvalido", 1, "N251", "123", 0, "UNIDADE" },
        { "Curso.UnidadeNomeVazio", 1, "N251", "E21", 1, "" },
        { "Curso.NomeLongo", 1, "N251", "E21", 1, "UNIDADE" },
        { "Curso.CodigoEmecLongo", 1, "DIREITO", "E21", 1, "U251" },
        { "Curso.UnidadeNomeLongo", 1, "DIREITO", "123", 1, "U251" },
    };

    public static readonly TheoryData<string, int, string, string?> Situacao = new()
    {
        { "SituacaoAcademica.IdInvalido", 0, "", "Q101" },
        { "SituacaoAcademica.DescricaoVazia", 1, "", "Q101" },
        { "SituacaoAcademica.DescricaoLonga", 1, "D251", "Q101" },
        { "SituacaoAcademica.VinculoLongo", 1, "ATIVO", "Q101" },
    };

    [Theory]
    [MemberData(nameof(Identificacao))]
    public void Identificacao_recusa_na_ordem_dos_campos(string codigo, long id, string matricula, string nome)
    {
        Result<IdentificacaoDoDiscente> resultado = IdentificacaoDoDiscente.Criar(
            id, matricula, VinculoDeTeste.CpfDeTeste(), Expandir(nome));

        resultado.Error!.Code.Should().Be(codigo);
    }

    [Theory]
    [MemberData(nameof(Curso))]
    public void Curso_recusa_na_ordem_dos_campos(
        string codigo,
        int id,
        string nome,
        string codigoEmec,
        int unidadeId,
        string unidadeNome)
    {
        Result<CursoCopiadoDoSigaa> resultado = CursoCopiadoDoSigaa.Criar(
            id, Expandir(nome), Expandir(codigoEmec), unidadeId, Expandir(unidadeNome));

        resultado.Error!.Code.Should().Be(codigo);
    }

    [Theory]
    [MemberData(nameof(Situacao))]
    public void Situacao_recusa_na_ordem_dos_campos(string codigo, int id, string descricao, string? qualificador)
    {
        Result<SituacaoAcademicaCopiada> resultado = SituacaoAcademicaCopiada.Criar(
            id, Expandir(descricao), Expandir(qualificador));

        resultado.Error!.Code.Should().Be(codigo);
    }

    /// <summary>
    /// Texto com o tamanho codificado: a letra inicial e o total de caracteres, como em
    /// <c>N251</c>; qualquer outro texto passa como está.
    /// </summary>
    private static string? Expandir(string? marca) =>
        marca is { Length: > 1 } && char.IsAsciiLetter(marca[0]) && int.TryParse(marca.AsSpan(1), out int tamanho)
            ? new string(marca[0], tamanho)
            : marca;
}
