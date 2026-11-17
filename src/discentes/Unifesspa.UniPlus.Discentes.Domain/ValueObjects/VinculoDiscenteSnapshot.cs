namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Estado completo de um vínculo discente vindo do SIGAA: quem o discente é
/// (<see cref="Identificacao"/>) e o que o vínculo é academicamente (nível, curso, situação e
/// ingresso). Não é um Builder mutável — todos os campos chegam prontos de uma vez, como uma
/// linha de sincronização.
/// </summary>
public sealed record VinculoDiscenteSnapshot
{
    public IdentificacaoDoDiscente Identificacao { get; }
    public string Nivel { get; }
    public CursoSigaaSnapshot Curso { get; }
    public SituacaoAcademicaSnapshot Situacao { get; }
    public PeriodoIngresso Ingresso { get; }

    private VinculoDiscenteSnapshot(
        IdentificacaoDoDiscente identificacao,
        string nivel,
        CursoSigaaSnapshot curso,
        SituacaoAcademicaSnapshot situacao,
        PeriodoIngresso ingresso)
    {
        Identificacao = identificacao;
        Nivel = nivel;
        Curso = curso;
        Situacao = situacao;
        Ingresso = ingresso;
    }

    public static Result<VinculoDiscenteSnapshot> Criar(
        IdentificacaoDoDiscente identificacao,
        string? nivel,
        CursoSigaaSnapshot curso,
        SituacaoAcademicaSnapshot situacao,
        PeriodoIngresso ingresso)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ArgumentNullException.ThrowIfNull(curso);
        ArgumentNullException.ThrowIfNull(situacao);
        ArgumentNullException.ThrowIfNull(ingresso);

        VinculoDiscenteSnapshot snapshot = new(identificacao, nivel ?? string.Empty, curso, situacao, ingresso);

        DomainError? erro = snapshot.ValidarNivel();

        return erro is null
            ? Result<VinculoDiscenteSnapshot>.Success(snapshot)
            : Result<VinculoDiscenteSnapshot>.Failure(erro);
    }

    /// <summary>
    /// Sobrescreve o <c>ToString()</c> sintetizado do record — que, por padrão, enumeraria
    /// Nome e Matrícula em texto claro em qualquer interpolação ou log deste objeto.
    /// </summary>
    public override string ToString() => $"[VinculoDiscenteSnapshot IdDiscenteSigaa={Identificacao.IdDiscenteSigaa}]";

    private DomainError? ValidarNivel()
    {
        if (string.IsNullOrWhiteSpace(Nivel))
            return new DomainError("VinculoDiscente.NivelVazio", "Nível é obrigatório.");

        if (Nivel.Length > LimitesDaReplica.Nivel)
            return new DomainError(
                "VinculoDiscente.NivelLongo",
                $"Nível excede {LimitesDaReplica.Nivel} caracteres, o máximo que a réplica comporta.");

        return null;
    }
}
