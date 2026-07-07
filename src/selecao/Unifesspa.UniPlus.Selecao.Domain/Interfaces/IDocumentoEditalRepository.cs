namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="DocumentoEdital"/> — independente de
/// <see cref="IProcessoSeletivoRepository"/> porque o documento não é
/// entidade filha do agregado <see cref="ProcessoSeletivo"/> (ver comentário
/// da entidade).
/// </summary>
public interface IDocumentoEditalRepository : IRepository<DocumentoEdital>
{
}
