namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A unidade que possui o processo, o escopo em que as permissões do processo são concedidas. O rascunho e o processo
/// publicado têm o mesmo identificador, então a unidade vem da linha do processo ou, se ele ainda não nasceu, da do
/// rascunho.
/// </summary>
public interface IUnidadeDonaDoProcesso
{
    /// <summary>A unidade dona; nula quando não há processo nem rascunho com o identificador.</summary>
    Task<Guid?> DoProcessoAsync(Guid id, CancellationToken cancellationToken = default);
}
