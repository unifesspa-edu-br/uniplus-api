namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Snapshot-copy (ADR-0061) da unidade que costuma responder por uma
/// <see cref="Entities.FaseCanonica"/>, usada como valor inicial ao montar o cronograma de um
/// processo. Guarda o id de origem sem chave estrangeira e a cópia de sigla e nome, resolvidos
/// pelo <c>IUnidadeReader</c> ao cadastrar ou editar a fase. Orientativa: o processo escolhe a
/// sua própria unidade responsável e nunca lê esta cópia.
/// </summary>
public sealed record UnidadeSugerida
{
    public UnidadeSugerida(Guid origemId, string sigla, string nome)
    {
        if (origemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem da unidade sugerida é obrigatório.", nameof(origemId));
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
