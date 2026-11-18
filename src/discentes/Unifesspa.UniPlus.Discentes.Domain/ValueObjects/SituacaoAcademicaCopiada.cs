namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Situação acadêmica do discente no SIGAA. O <see cref="Qualificador"/> é o campo
/// "vínculo" que o SIGAA associa à situação (nomenclatura do sistema de origem), não a
/// situação do <c>VinculoDiscente</c> em si.
/// </summary>
public sealed record SituacaoAcademicaCopiada
{
    public IdDaSituacaoNoSigaa Id { get; }
    public DescricaoDaSituacao Descricao { get; }
    public QualificadorDaSituacao? Qualificador { get; }

    private SituacaoAcademicaCopiada(
        IdDaSituacaoNoSigaa id,
        DescricaoDaSituacao descricao,
        QualificadorDaSituacao? qualificador)
    {
        Id = id;
        Descricao = descricao;
        Qualificador = qualificador;
    }

    public static Result<SituacaoAcademicaCopiada> Criar(int id, string? descricao, string? qualificador) =>
        IdDaSituacaoNoSigaa.Criar(id).Encadear(idDaSituacao =>
            DescricaoDaSituacao.Criar(descricao).Encadear(descricaoDaSituacao =>
                QualificadorDaSituacao.Criar(qualificador).Compor(qualificadorDaSituacao =>
                    new SituacaoAcademicaCopiada(idDaSituacao, descricaoDaSituacao, qualificadorDaSituacao))));
}
