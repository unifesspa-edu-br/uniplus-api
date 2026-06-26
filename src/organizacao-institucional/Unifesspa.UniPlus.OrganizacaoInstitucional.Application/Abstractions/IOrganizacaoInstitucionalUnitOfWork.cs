namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Unit of Work específica do módulo OrganizacaoInstitucional. Especializa
/// <see cref="IUnitOfWork"/> para que múltiplos módulos coexistam num processo
/// único sem colisão de registro no container — cada handler injeta a abstração
/// do seu próprio módulo, roteada para o DbContext correspondente.
/// </summary>
public interface IOrganizacaoInstitucionalUnitOfWork : IUnitOfWork
{
}
