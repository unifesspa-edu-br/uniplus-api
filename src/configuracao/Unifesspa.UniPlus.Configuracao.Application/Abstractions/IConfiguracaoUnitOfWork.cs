namespace Unifesspa.UniPlus.Configuracao.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// UnitOfWork do módulo Configuracao. Especialização sem novos membros de
/// <see cref="IUnitOfWork"/> que dá um tipo de registro próprio ao módulo —
/// garante o isolamento da transação no monólito modular, onde vários módulos
/// coexistem no mesmo container e um <see cref="IUnitOfWork"/> compartilhado
/// colidiria no contêiner de DI (o último registro venceria).
/// </summary>
public interface IConfiguracaoUnitOfWork : IUnitOfWork
{
}
