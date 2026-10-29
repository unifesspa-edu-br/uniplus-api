namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Snapshot-copy (ADR-0061) da unidade que responde por uma
/// <see cref="Entities.FaseCronograma"/>, congelado ao definir o cronograma. Guarda o id
/// de origem sem chave estrangeira e a cópia de sigla e nome; decisões comparam pelo
/// <see cref="OrigemId"/>, nunca pela sigla (ADR-0129).
/// </summary>
public sealed record UnidadeResponsavelSnapshot
{
    public UnidadeResponsavelSnapshot(Guid origemId, string sigla, string nome)
    {
        if (origemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem da unidade responsável é obrigatório.", nameof(origemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sigla);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        OrigemId = origemId;
        Sigla = sigla.Trim();
        Nome = nome.Trim();
    }

    public Guid OrigemId { get; }
    public string Sigla { get; }
    public string Nome { get; }
}
