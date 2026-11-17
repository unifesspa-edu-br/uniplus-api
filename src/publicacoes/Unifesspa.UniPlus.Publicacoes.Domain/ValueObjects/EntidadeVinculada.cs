namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// A entidade de outro domínio de que o ato trata: o par opaco (tipo, id) que Publicações guarda sem
/// interpretar (ADR-0105).
/// </summary>
/// <param name="Tipo">Rótulo opaco do tipo da entidade, na forma canônica de <see cref="TipoDaEntidadeVinculada"/>.</param>
/// <param name="Id">Identificador da entidade, opaco para o módulo (sem chave estrangeira).</param>
public readonly record struct EntidadeVinculada(string Tipo, Guid Id)
{
    /// <summary>
    /// O par como o registro o grava: o rótulo na forma canônica, sem espaços nas pontas, e o id não
    /// vazio. Só a gravação confere; na leitura o par é filtro opaco.
    /// </summary>
    internal EntidadeVinculada Conferida() => new(TipoDaEntidadeVinculada.Conferido(Tipo), IdConferido(Id));

    private static Guid IdConferido(Guid entidadeId) =>
        entidadeId == Guid.Empty
            ? throw new ArgumentException("Identificador da entidade vinculada não pode ser vazio.", nameof(entidadeId))
            : entidadeId;
}
