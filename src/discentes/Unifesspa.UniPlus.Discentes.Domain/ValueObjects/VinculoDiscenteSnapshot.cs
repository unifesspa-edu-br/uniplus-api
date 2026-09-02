namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Parameter Object do estado completo de um vínculo discente vindo do SIGAA.
/// Não é um Builder mutável — todos os campos chegam prontos de uma vez, como
/// uma linha de sincronização.
/// </summary>
public sealed record VinculoDiscenteSnapshot
{

    public long IdDiscenteSigaa { get; }
    public string Matricula { get; }
    public Cpf Cpf { get; }
    public string Nome { get; }
    public string Nivel { get; }
    public CursoSigaaSnapshot Curso { get; }
    public SituacaoAcademicaSnapshot Situacao { get; }
    public PeriodoIngresso Ingresso { get; }

    private VinculoDiscenteSnapshot(
        long idDiscenteSigaa,
        string matricula,
        Cpf cpf,
        string nome,
        string nivel,
        CursoSigaaSnapshot curso,
        SituacaoAcademicaSnapshot situacao,
        PeriodoIngresso ingresso)
    {
        IdDiscenteSigaa = idDiscenteSigaa;
        Matricula = matricula;
        Cpf = cpf;
        Nome = nome;
        Nivel = nivel;
        Curso = curso;
        Situacao = situacao;
        Ingresso = ingresso;
    }

    public static Result<VinculoDiscenteSnapshot> Criar(
        long idDiscenteSigaa,
        string? matricula,
        Cpf cpf,
        string? nome,
        string? nivel,
        CursoSigaaSnapshot curso,
        SituacaoAcademicaSnapshot situacao,
        PeriodoIngresso ingresso)
    {
        if (idDiscenteSigaa <= 0)
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError("VinculoDiscente.IdSigaaInvalido", "Id SIGAA deve ser positivo."));

        if (string.IsNullOrWhiteSpace(matricula))
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError("VinculoDiscente.MatriculaVazia", "Matrícula é obrigatória."));

        if (!ApenasDigitos(matricula))
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError(
                "VinculoDiscente.MatriculaNaoNumerica",
                "Matrícula precisa ser composta apenas por dígitos."));

        if (matricula.Length > LimitesDaReplica.Matricula)
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError(
                "VinculoDiscente.MatriculaLonga",
                $"Matrícula excede {LimitesDaReplica.Matricula} caracteres, o máximo que a réplica comporta."));

        ArgumentNullException.ThrowIfNull(cpf);

        if (string.IsNullOrWhiteSpace(nome))
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError("VinculoDiscente.NomeVazio", "Nome é obrigatório."));

        if (nome.Length > LimitesDaReplica.Nome)
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError(
                DiscentesErrorCodes.VinculoDiscente.NomeLongo,
                $"Nome excede {LimitesDaReplica.Nome} caracteres, o máximo que a réplica comporta."));

        if (string.IsNullOrWhiteSpace(nivel))
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError("VinculoDiscente.NivelVazio", "Nível é obrigatório."));

        if (nivel.Length > LimitesDaReplica.Nivel)
            return Result<VinculoDiscenteSnapshot>.Failure(new DomainError(
                "VinculoDiscente.NivelLongo",
                $"Nível excede {LimitesDaReplica.Nivel} caracteres, o máximo que a réplica comporta."));

        ArgumentNullException.ThrowIfNull(curso);
        ArgumentNullException.ThrowIfNull(situacao);
        ArgumentNullException.ThrowIfNull(ingresso);

        return Result<VinculoDiscenteSnapshot>.Success(
            new VinculoDiscenteSnapshot(idDiscenteSigaa, matricula, cpf, nome, nivel, curso, situacao, ingresso));
    }

    /// <summary>
    /// Diz se o texto é composto apenas por dígitos, que é o formato de matrícula que a
    /// origem promete.
    /// </summary>
    public static bool ApenasDigitos(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);

        return valor.Length > 0 && !valor.AsSpan().ContainsAnyExceptInRange('0', '9');
    }

    /// <summary>
    /// Sobrescreve o <c>ToString()</c> sintetizado do record — que, por padrão, enumeraria
    /// Nome e Matrícula em texto claro em qualquer interpolação ou log deste objeto.
    /// </summary>
    public override string ToString() => $"[VinculoDiscenteSnapshot IdDiscenteSigaa={IdDiscenteSigaa}]";
}
