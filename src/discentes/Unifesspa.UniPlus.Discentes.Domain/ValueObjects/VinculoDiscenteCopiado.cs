namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Estado completo de um vínculo discente vindo do SIGAA: quem o discente é
/// (<see cref="Identificacao"/>) e o que o vínculo é academicamente (nível, curso, situação e
/// ingresso). Todos os campos chegam juntos na sincronização.
/// </summary>
public sealed record VinculoDiscenteCopiado
{
    public IdentificacaoDoDiscente Identificacao { get; }
    public string Nivel { get; }
    public CursoCopiadoDoSigaa Curso { get; }
    public SituacaoAcademicaCopiada Situacao { get; }
    public PeriodoIngresso Ingresso { get; }

    private VinculoDiscenteCopiado(
        IdentificacaoDoDiscente identificacao,
        string nivel,
        CursoCopiadoDoSigaa curso,
        SituacaoAcademicaCopiada situacao,
        PeriodoIngresso ingresso)
    {
        Identificacao = identificacao;
        Nivel = nivel;
        Curso = curso;
        Situacao = situacao;
        Ingresso = ingresso;
    }

    public static Result<VinculoDiscenteCopiado> Criar(
        IdentificacaoDoDiscente identificacao,
        string? nivel,
        CursoCopiadoDoSigaa curso,
        SituacaoAcademicaCopiada situacao,
        PeriodoIngresso ingresso)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ArgumentNullException.ThrowIfNull(curso);
        ArgumentNullException.ThrowIfNull(situacao);
        ArgumentNullException.ThrowIfNull(ingresso);

        VinculoDiscenteCopiado dadosCopiados = new(identificacao, nivel ?? string.Empty, curso, situacao, ingresso);

        DomainError? erro = dadosCopiados.ValidarNivel();

        return erro is null
            ? Result<VinculoDiscenteCopiado>.Success(dadosCopiados)
            : Result<VinculoDiscenteCopiado>.Failure(erro);
    }

    /// <summary>
    /// Sobrescreve o <c>ToString()</c> sintetizado do record — que, por padrão, enumeraria
    /// Nome e Matrícula em texto claro em qualquer interpolação ou log deste objeto.
    /// </summary>
    public override string ToString() => $"[VinculoDiscenteCopiado IdDiscenteSigaa={Identificacao.IdDiscenteSigaa}]";

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
