using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

namespace Unifesspa.UniPlus.Discentes.Domain.Entities;


public class VinculoDiscente
{
    public Guid Id { get; private set; }
    public long IdDiscenteSigaa { get; private set; }
    public string Matricula { get; private set; } = null!;
    public Cpf Cpf { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string Nivel { get; private set; } = null!;

    // Dados do Curso
    public int CursoId { get; private set; }
    public string CursoNome { get; private set; } = null!;
    public string? CursoCodigoEmec { get; private set; }
    public int CursoUnidadeId { get; private set; }
    public string CursoUnidadeNome { get; private set; } = null!;

    // Dados da Situação
    public int SituacaoId { get; private set; }
    public string SituacaoDescricao { get; private set; } = null!;
    public string? SituacaoVinculo { get; private set; }

    // Período
    public int AnoIngresso { get; private set; }
    public int PeriodoIngresso { get; private set; }

    private VinculoDiscente() { }

    private VinculoDiscente(
        Guid id,
        long idDiscenteSigaa,
        string matricula,
        Cpf cpf,
        string nome,
        string nivel,
        int cursoId,
        string cursoNome,
        string? cursoCodigoEmec,
        int cursoUnidadeId,
        string cursoUnidadeNome,
        int situacaoId,
        string situacaoDescricao,
        string? situacaoVinculo,
        int anoIngresso,
        int periodoIngresso)
    {

        if (id == Guid.Empty)
            throw new ArgumentException("O identificador não pode ser vazio.", nameof(id));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idDiscenteSigaa);

        ArgumentException.ThrowIfNullOrWhiteSpace(matricula);
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(nivel);

        ArgumentException.ThrowIfNullOrWhiteSpace(cursoNome);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursoUnidadeNome);
        ArgumentException.ThrowIfNullOrWhiteSpace(situacaoDescricao);

        Id = id;
        IdDiscenteSigaa = idDiscenteSigaa;
        Matricula = matricula;
        Cpf = cpf;
        Nome = nome;
        Nivel = nivel;
        CursoId = cursoId;
        CursoNome = cursoNome;
        CursoCodigoEmec = cursoCodigoEmec;
        CursoUnidadeId = cursoUnidadeId;
        CursoUnidadeNome = cursoUnidadeNome;
        SituacaoId = situacaoId;
        SituacaoDescricao = situacaoDescricao;
        SituacaoVinculo = situacaoVinculo;
        AnoIngresso = anoIngresso;
        PeriodoIngresso = periodoIngresso;
    }
    /// <summary>
    /// Factory Method para criação de novos vínculos discentes com garantia de UUIDv7.
    /// </summary>
    public static VinculoDiscente Criar(
        long idDiscenteSigaa,
        string matricula,
        Cpf cpf,
        string nome,
        string nivel,
        int cursoId,
        string cursoNome,
        string? cursoCodigoEmec,
        int cursoUnidadeId,
        string cursoUnidadeNome,
        int situacaoId,
        string situacaoDescricao,
        string? situacaoVinculo,
        int anoIngresso,
        int periodoIngresso)
    {
        Guid id = Guid.CreateVersion7();

        return new VinculoDiscente(
            id,
            idDiscenteSigaa,
            matricula,
            cpf,
            nome,
            nivel,
            cursoId,
            cursoNome,
            cursoCodigoEmec,
            cursoUnidadeId,
            cursoUnidadeNome,
            situacaoId,
            situacaoDescricao,
            situacaoVinculo,
            anoIngresso,
            periodoIngresso);
    }
    internal static VinculoDiscente Reidratar(
    Guid id,
    long idDiscenteSigaa,
    string matricula,
    Cpf cpf,
    string nome,
    string nivel,
    int cursoId,
    string cursoNome,
    string? cursoCodigoEmec,
    int cursoUnidadeId,
    string cursoUnidadeNome,
    int situacaoId,
    string situacaoDescricao,
    string? situacaoVinculo,
    int anoIngresso,
    int periodoIngresso)
    {
        return new VinculoDiscente(
            id,
            idDiscenteSigaa,
            matricula,
            cpf,
            nome,
            nivel,
            cursoId,
            cursoNome,
            cursoCodigoEmec,
            cursoUnidadeId,
            cursoUnidadeNome,
            situacaoId,
            situacaoDescricao,
            situacaoVinculo,
            anoIngresso,
            periodoIngresso);
    }
    /// <summary>
    /// Retorna uma representação técnica e opaca sem exposição de PII (Nome, Matrícula, CPF).
    /// </summary>
    public override string ToString() => $"[VinculoDiscente Id={Id}, IdDiscenteSigaa={IdDiscenteSigaa}]";
}
