using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

namespace Unifesspa.UniPlus.Discentes.Domain.Entities;


public class VinculoDiscente
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
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

    public VinculoDiscente(
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

    public override string ToString() =>
        $"Discente: {Nome} | Matrícula: {Matricula} | CPF: {this.Cpf.Mascarado}";
}

