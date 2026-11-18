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
    public NivelDoVinculo Nivel { get; }
    public CursoCopiadoDoSigaa Curso { get; }
    public SituacaoAcademicaCopiada Situacao { get; }
    public PeriodoIngresso Ingresso { get; }

    private VinculoDiscenteCopiado(
        IdentificacaoDoDiscente identificacao,
        NivelDoVinculo nivel,
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

        return NivelDoVinculo.Criar(nivel).Compor(nivelDoVinculo =>
            new VinculoDiscenteCopiado(identificacao, nivelDoVinculo, curso, situacao, ingresso));
    }

    /// <summary>
    /// Sobrescreve o <c>ToString()</c> sintetizado do record — que, por padrão, enumeraria
    /// Nome e Matrícula em texto claro em qualquer interpolação ou log deste objeto.
    /// </summary>
    public override string ToString() =>
        $"[VinculoDiscenteCopiado IdDiscenteSigaa={Identificacao.IdDiscenteSigaa.Valor}]";
}
