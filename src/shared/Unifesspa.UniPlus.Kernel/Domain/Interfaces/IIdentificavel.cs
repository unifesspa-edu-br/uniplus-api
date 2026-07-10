namespace Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Contrato mínimo de identidade: uma entidade persistida com um
/// <see cref="Id"/> <see cref="Guid"/> v7 (ADR-0032), ordenável temporalmente.
/// Base comum de <see cref="Entities.EntityBase"/> e de
/// <see cref="IForensicEntity"/> — as duas famílias de entidade do projeto
/// (mutável com soft-delete opt-in vs. append-only forense) compartilham só
/// isto: um Id ordenável.
/// </summary>
/// <remarks>
/// Existe para que utilitários agnósticos ao ciclo de vida — a paginação keyset
/// (ADR-0089), que ordena e fatia por <see cref="Id"/> — operem sobre ambas as
/// famílias sem acoplar-se a <see cref="Entities.EntityBase"/>, que carrega
/// muito mais do que o keyset precisa.
/// </remarks>
public interface IIdentificavel
{
    /// <summary>Identificador único da entidade (UUID v7, ordenável por tempo).</summary>
    Guid Id { get; }
}
