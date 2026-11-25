namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Snapshot-copy (ADR-0061) da unidade que responde por uma
/// <see cref="Entities.FaseCronograma"/>, congelado ao definir o cronograma. Guarda o id
/// de origem sem chave estrangeira e a cópia de código, sigla e nome; decisões comparam pelo
/// <see cref="OrigemId"/>, nunca pela sigla (ADR-0129).
/// </summary>
public sealed record UnidadeResponsavelSnapshot
{
    public UnidadeResponsavelSnapshot(Guid origemId, string codigo, string sigla, string nome)
    {
        if (origemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem da unidade responsável é obrigatório.", nameof(origemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(sigla);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        OrigemId = origemId;
        Codigo = codigo;
        Sigla = sigla.Trim();
        Nome = nome.Trim();
    }

    private UnidadeResponsavelSnapshot() { }

    public Guid OrigemId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Sigla { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
}
