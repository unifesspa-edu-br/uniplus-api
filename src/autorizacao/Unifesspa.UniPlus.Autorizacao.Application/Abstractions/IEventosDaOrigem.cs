namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Eventos que a origem de uma concessão (o pedido aprovado ou a instalação) acrescenta à
/// trilha dela: pedido, aprovações, documento. Cada origem implementa a sua porta; a trilha
/// da concessão é montada na leitura, sem tabela de eventos.
/// </summary>
public interface IEventosDaOrigem
{
    /// <summary>A origem que esta implementação conhece.</summary>
    TipoDeOrigem Tipo { get; }

    Task<IReadOnlyList<EventoDaTrilhaDto>> ListarAsync(Guid origemId, CancellationToken cancellationToken);
}
