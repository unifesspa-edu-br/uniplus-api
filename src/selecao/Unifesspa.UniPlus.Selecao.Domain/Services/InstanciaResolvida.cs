namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Linq;
using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// O estado resolvido de UMA instância de entidade (Story #922), com seus próprios estados
/// e status descendentes — nunca compartilhados com outra instância nem com os dicionários
/// globais. <see cref="StatusDescendentes"/> alimenta <see cref="ValueObjects.StatusPorEntidade"/>
/// no resultado final — sem ele, uma folha sem consequência configurada fica invisível
/// por-instância (só o agregado da raiz é visto).
/// </summary>
internal sealed record InstanciaResolvida(
    string EntidadeId,
    EstadoSatisfacao Estado,
    Dictionary<Guid, EstadoSatisfacao> EstadosDescendentes,
    Dictionary<Guid, StatusResolucaoExigencia> StatusDescendentes,
    IReadOnlySet<Guid> NosEmissaoSuprimida);
