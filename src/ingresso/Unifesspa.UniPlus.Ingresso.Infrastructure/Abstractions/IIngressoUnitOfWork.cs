namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Unidade de trabalho específica do módulo Ingresso. Especializa o
/// <see cref="IUnitOfWork"/> compartilhado para permitir coexistência de
/// múltiplos módulos num mesmo container DI (cada módulo registra e injeta a
/// própria interface, evitando colisão do registro genérico de
/// <see cref="IUnitOfWork"/>).
/// </summary>
public interface IIngressoUnitOfWork : IUnitOfWork
{
}
