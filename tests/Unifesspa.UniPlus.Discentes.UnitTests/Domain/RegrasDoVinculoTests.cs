namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas dos valores de entrada do vínculo e a ordem em que elas valem: a primeira
/// regra violada, na ordem declarada, é a que se devolve.
/// </summary>
public sealed class RegrasDoVinculoTests
{
    public static readonly TheoryData<string, long, string, string> RecusasDaIdentificacao = new()
    {
        { "VinculoDiscente.IdSigaaInvalido", 0, "123", "ANA" },
        { "VinculoDiscente.IdSigaaInvalido", 0, "", "" },
        { "VinculoDiscente.MatriculaVazia", 1, "", "ANA" },
        { "VinculoDiscente.MatriculaVazia", 1, "  ", "" },
        { "VinculoDiscente.MatriculaNaoNumerica", 1, "12A", "ANA" },
        { "VinculoDiscente.MatriculaNaoNumerica", 1, "12A", "" },
        { "VinculoDiscente.MatriculaLonga", 1, new string('9', LimitesDaReplica.Matricula + 1), "ANA" },
        { "VinculoDiscente.NomeVazio", 1, "123", " " },
        { "VinculoDiscente.NomeLongo", 1, "123", Texto(LimitesDaReplica.Nome + 1) },
    };

    public static readonly TheoryData<string, int, string, string?, int, string> RecusasDoCurso = new()
    {
        { "Curso.IdInvalido", 0, "DIREITO", null, 64, "INSTITUTO" },
        { "Curso.IdInvalido", 0, "", null, 0, "" },
        { "Curso.NomeVazio", 20, " ", null, 64, "INSTITUTO" },
        { "Curso.NomeVazio", 20, "", null, 0, "" },
        { "Curso.UnidadeIdInvalido", 20, "DIREITO", null, 0, "INSTITUTO" },
        { "Curso.UnidadeIdInvalido", 20, "DIREITO", null, -1, "" },
        { "Curso.UnidadeNomeVazio", 20, "DIREITO", null, 64, " " },
        { "Curso.NomeLongo", 20, Texto(LimitesDaReplica.NomeDoCurso + 1), null, 64, "INSTITUTO" },
        { "Curso.CodigoEmecLongo", 20, "DIREITO", Texto(LimitesDaReplica.CodigoEmecDoCurso + 1), 64, "INSTITUTO" },
        { "Curso.UnidadeNomeLongo", 20, "DIREITO", null, 64, Texto(LimitesDaReplica.NomeDaUnidade + 1) },
    };

    [Theory]
    [MemberData(nameof(RecusasDaIdentificacao))]
    public void Identificacao_recusa_a_primeira_regra_violada(string codigo, long id, string matricula, string nome)
    {
        Result<IdentificacaoDoDiscente> resultado = IdentificacaoDoDiscente.Criar(
            id, matricula, VinculoDeTeste.CpfDeTeste(), nome);

        resultado.Error!.Code.Should().Be(codigo);
    }

    [Fact]
    public void Identificacao_aceita_matricula_e_nome_no_limite()
    {
        Result<IdentificacaoDoDiscente> resultado = IdentificacaoDoDiscente.Criar(
            1, new string('9', LimitesDaReplica.Matricula), VinculoDeTeste.CpfDeTeste(), Texto(LimitesDaReplica.Nome));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Identificacao_exige_o_cpf()
    {
        Action semCpf = () => IdentificacaoDoDiscente.Criar(1, "123", cpf: null!, "ANA");

        semCpf.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("", false)]
    [InlineData("12A", false)]
    [InlineData("12 3", false)]
    public void Matricula_so_aceita_digitos(string valor, bool esperado)
    {
        IdentificacaoDoDiscente.ApenasDigitos(valor).Should().Be(esperado);
    }

    [Theory]
    [InlineData("VinculoDiscente.NivelVazio", " ")]
    [InlineData("VinculoDiscente.NivelLongo", "GRADUA")]
    public void DadosCopiados_recusa_o_nivel_invalido(string codigo, string nivel)
    {
        Result<VinculoDiscenteCopiado> resultado = VinculoDiscenteCopiado.Criar(
            VinculoDeTeste.Identificacao(),
            nivel,
            VinculoDeTeste.Curso(),
            VinculoDeTeste.Situacao(),
            VinculoDeTeste.Ingresso());

        resultado.Error!.Code.Should().Be(codigo);
    }

    [Theory]
    [MemberData(nameof(RecusasDoCurso))]
    public void Curso_recusa_a_primeira_regra_violada(
        string codigo,
        int id,
        string nome,
        string? codigoEmec,
        int unidadeId,
        string unidadeNome)
    {
        Result<CursoCopiadoDoSigaa> resultado = CursoCopiadoDoSigaa.Criar(
            id, nome, codigoEmec, unidadeId, unidadeNome);

        resultado.Error!.Code.Should().Be(codigo);
    }

    [Theory]
    [InlineData("SituacaoAcademica.IdInvalido", 0, "ATIVO", null)]
    [InlineData("SituacaoAcademica.IdInvalido", 0, "", null)]
    [InlineData("SituacaoAcademica.DescricaoVazia", 1, "  ", null)]
    public void Situacao_recusa_a_primeira_regra_violada(string codigo, int id, string descricao, string? vinculo)
    {
        Result<SituacaoAcademicaCopiada> resultado = SituacaoAcademicaCopiada.Criar(id, descricao, vinculo);

        resultado.Error!.Code.Should().Be(codigo);
    }

    [Fact]
    public void Situacao_recusa_texto_alem_do_que_a_replica_comporta()
    {
        SituacaoAcademicaCopiada.Criar(1, Texto(LimitesDaReplica.DescricaoDaSituacao + 1), null)
            .Error!.Code.Should().Be("SituacaoAcademica.DescricaoLonga");
        SituacaoAcademicaCopiada.Criar(1, "ATIVO", Texto(LimitesDaReplica.VinculoDaSituacao + 1))
            .Error!.Code.Should().Be("SituacaoAcademica.VinculoLongo");
    }

    [Fact]
    public void Curso_e_situacao_aceitam_textos_no_limite()
    {
        CursoCopiadoDoSigaa.Criar(
            1,
            Texto(LimitesDaReplica.NomeDoCurso),
            Texto(LimitesDaReplica.CodigoEmecDoCurso),
            1,
            Texto(LimitesDaReplica.NomeDaUnidade)).IsSuccess.Should().BeTrue();
        SituacaoAcademicaCopiada.Criar(
            1,
            Texto(LimitesDaReplica.DescricaoDaSituacao),
            Texto(LimitesDaReplica.VinculoDaSituacao)).IsSuccess.Should().BeTrue();
    }

    private static string Texto(int tamanho) => new('A', tamanho);
}
