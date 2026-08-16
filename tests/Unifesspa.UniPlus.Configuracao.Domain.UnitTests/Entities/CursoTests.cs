namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CursoTests
{
    private const string Codigo = "ENG_CIVIL";
    private const string Nome = "Engenharia Civil";
    private const string Grau = "Bacharelado";
    private const string NivelEnsino = "Graduação";

    private static Result<Curso> Criar(
        string codigo = Codigo,
        string nome = Nome,
        string grau = Grau,
        string nivelEnsino = NivelEnsino,
        string? grupoAreaEnem = null) =>
        Curso.Criar(codigo, nome, grau, nivelEnsino, grupoAreaEnem);

    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativo com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        Curso curso = Criar(grupoAreaEnem: GrupoCurso.Tecnologica).Value!;

        curso.Id.Should().NotBe(Guid.Empty);
        curso.Codigo.Should().Be(Codigo);
        curso.Nome.Should().Be(Nome);
        curso.Grau.Should().Be(Grau);
        curso.NivelEnsino.Should().Be(NivelEnsino);
        curso.GrupoAreaEnem!.Valor.Should().Be(GrupoCurso.Tecnologica);
        curso.IsDeleted.Should().BeFalse();
    }

    [Theory(DisplayName = "Criar sem grupo de área do ENEM (nulo ou em branco) é aceito — nem todo curso classifica por área")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemGrupoAreaEnem_Aceita(string? grupoAreaEnem)
    {
        Curso curso = Criar(grupoAreaEnem: grupoAreaEnem).Value!;

        curso.GrupoAreaEnem.Should().BeNull();
    }

    [Theory(DisplayName = "Grupo de área do ENEM fora do domínio fechado é rejeitado")]
    [InlineData("Exatas")]
    [InlineData("Tecnologica")]
    [InlineData("HUMANÍSTICA I")]
    public void Criar_GrupoAreaEnemInvalido_Falha(string grupoAreaEnem)
    {
        Result<Curso> resultado = Criar(grupoAreaEnem: grupoAreaEnem);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.GrupoAreaEnemInvalido);
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<Curso> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "Código acima do tamanho máximo é rejeitado")]
    public void Criar_CodigoLongo_Falha()
    {
        Result<Curso> resultado = Criar(codigo: new string('A', 61));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.CodigoTamanho);
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<Curso> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome acima do tamanho máximo é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<Curso> resultado = Criar(nome: new string('N', 201));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.NomeTamanho);
    }

    [Theory(DisplayName = "Grau ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemGrau_Falha(string grau)
    {
        Result<Curso> resultado = Criar(grau: grau);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.GrauObrigatorio);
    }

    [Fact(DisplayName = "Grau acima do tamanho máximo é rejeitado")]
    public void Criar_GrauLongo_Falha()
    {
        Result<Curso> resultado = Criar(grau: new string('G', 61));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.GrauTamanho);
    }

    [Theory(DisplayName = "Nível de ensino ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNivelEnsino_Falha(string nivelEnsino)
    {
        Result<Curso> resultado = Criar(nivelEnsino: nivelEnsino);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.NivelEnsinoObrigatorio);
    }

    [Fact(DisplayName = "Nível de ensino acima do tamanho máximo é rejeitado")]
    public void Criar_NivelEnsinoLongo_Falha()
    {
        Result<Curso> resultado = Criar(nivelEnsino: new string('E', 61));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.NivelEnsinoTamanho);
    }

    [Fact(DisplayName = "Campos são normalizados por Trim na criação")]
    public void Criar_CamposComEspacos_Normaliza()
    {
        Curso curso = Criar(
            codigo: "  ENG_CIVIL  ",
            nome: "  Engenharia Civil  ",
            grau: "  Bacharelado  ",
            nivelEnsino: "  Graduação  ",
            grupoAreaEnem: $"  {GrupoCurso.Tecnologica}  ").Value!;

        curso.Codigo.Should().Be("ENG_CIVIL");
        curso.Nome.Should().Be("Engenharia Civil");
        curso.Grau.Should().Be("Bacharelado");
        curso.NivelEnsino.Should().Be("Graduação");
        curso.GrupoAreaEnem!.Valor.Should().Be(GrupoCurso.Tecnologica);
    }

    [Fact(DisplayName = "Atualizar troca os atributos editáveis, inclusive o código (editável)")]
    public void Atualizar_AlteraAtributos_InclusiveCodigo()
    {
        Curso curso = Criar().Value!;
        Guid idOriginal = curso.Id;

        Result resultado = curso.Atualizar(
            "ENG_CIVIL_NOVO", "Engenharia Civil Integral", "Licenciatura", "Mestrado", GrupoCurso.SaudeEBiologicas);

        resultado.IsSuccess.Should().BeTrue();
        curso.Codigo.Should().Be("ENG_CIVIL_NOVO");
        curso.Nome.Should().Be("Engenharia Civil Integral");
        curso.Grau.Should().Be("Licenciatura");
        curso.NivelEnsino.Should().Be("Mestrado");
        curso.GrupoAreaEnem!.Valor.Should().Be(GrupoCurso.SaudeEBiologicas);
        curso.Id.Should().Be(idOriginal, "o Id é imutável mesmo com o código editável");
    }

    [Fact(DisplayName = "Atualizar removendo o grupo de área do ENEM (nulo) é aceito")]
    public void Atualizar_RemoveGrupoAreaEnem_Aceita()
    {
        Curso curso = Criar(grupoAreaEnem: GrupoCurso.HumanisticaI).Value!;

        Result resultado = curso.Atualizar(Codigo, Nome, Grau, NivelEnsino, null);

        resultado.IsSuccess.Should().BeTrue();
        curso.GrupoAreaEnem.Should().BeNull();
    }

    [Fact(DisplayName = "Atualizar com grupo de área do ENEM inválido é rejeitado sem alterar o agregado")]
    public void Atualizar_GrupoAreaEnemInvalido_Falha()
    {
        Curso curso = Criar(grupoAreaEnem: GrupoCurso.HumanisticaII).Value!;

        Result resultado = curso.Atualizar(Codigo, Nome, Grau, NivelEnsino, "Exatas");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.GrupoAreaEnemInvalido);
        curso.GrupoAreaEnem!.Valor.Should().Be(GrupoCurso.HumanisticaII, "a falha de validação não muta o agregado");
    }

    // ── Nulo não lança (ADR-0125) e acumulação ──────────────────────────────────

    [Fact(DisplayName = "Todos os quatro campos obrigatórios nulos não lançam — devolvem as quatro violações rotuladas")]
    public void Criar_QuatroCamposObrigatoriosNulos_NaoLancaEDevolveAsQuatroViolacoes()
    {
        Result<Curso> resultado = Curso.Criar(null, null, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(4);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(CursoErrorCodes.CodigoObrigatorio);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(CursoErrorCodes.NomeObrigatorio);
        resultado.Errors[2].Field.Should().Be("grau");
        resultado.Errors[2].Error.Code.Should().Be(CursoErrorCodes.GrauObrigatorio);
        resultado.Errors[3].Field.Should().Be("nivelEnsino");
        resultado.Errors[3].Error.Code.Should().Be(CursoErrorCodes.NivelEnsinoObrigatorio);
    }

    [Fact(DisplayName = "Código, nome e grupo de área do ENEM inválidos ao mesmo tempo acumulam as três violações rotuladas")]
    public void Criar_TresCamposInvalidos_AcumulaAsTresViolacoesRotuladas()
    {
        Result<Curso> resultado = Criar(codigo: "", nome: new string('N', 201), grupoAreaEnem: "Exatas");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(CursoErrorCodes.CodigoObrigatorio);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(CursoErrorCodes.NomeTamanho);
        resultado.Errors[2].Field.Should().Be("grupoAreaEnem");
        resultado.Errors[2].Error.Code.Should().Be(CursoErrorCodes.GrupoAreaEnemInvalido);
    }

    [Fact(DisplayName = "Atualizar com código e nível de ensino inválidos acumula as duas violações sem mutar o agregado")]
    public void Atualizar_CodigoENivelEnsinoInvalidos_AcumulaAsDuasViolacoesSemMutar()
    {
        Curso curso = Criar().Value!;

        Result resultado = curso.Atualizar("", Nome, Grau, "", null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        curso.Codigo.Should().Be(Codigo, "falha de validação não pode mutar o agregado");
        curso.NivelEnsino.Should().Be(NivelEnsino);
    }
}
