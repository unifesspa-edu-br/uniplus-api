namespace Unifesspa.UniPlus.Kernel.Domain.Entities;

using Interfaces;

// Base opt-in de soft-delete (issue #629): só entidades que precisam de
// exclusão lógica derivam daqui. EntityBase carrega apenas identidade,
// timestamps e domain events — sem colunas de soft-delete. O critério de
// opt-in é a interface ISoftDeletable: o SoftDeleteInterceptor itera
// Entries<ISoftDeletable>() e a convenção AplicarFiltroGlobalSoftDelete
// aplica `e => !e.IsDeleted` a todo tipo que a implementa. Esta classe
// existe só para carregar a implementação dos membros uma única vez,
// evitando duplicá-la nas entidades soft-deletable.
public abstract class SoftDeletableEntity : EntityBase, ISoftDeletable
{
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    // O instante é parâmetro, não relógio lido aqui: o caller (SoftDeleteInterceptor
    // ou repositório) provê deletedAt a partir do TimeProvider injetado, mantendo
    // o domínio determinístico e a leitura de relógio concentrada em Infrastructure.
    public void MarkAsDeleted(string deletedBy, DateTimeOffset deletedAt)
    {
        IsDeleted = true;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
    }
}
