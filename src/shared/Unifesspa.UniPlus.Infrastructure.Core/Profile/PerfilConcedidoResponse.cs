namespace Unifesspa.UniPlus.Infrastructure.Core.Profile;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Perfil de acesso que a pessoa tem por concessão vigente, no escopo em que foi concedido.
/// </summary>
/// <param name="ConcessaoId">Identificador da concessão.</param>
/// <param name="PerfilId">Identificador do perfil concedido.</param>
/// <param name="Nome">Nome do perfil.</param>
/// <param name="EscopoTipo">Alcance da concessão: <c>UNIDADE</c> ou <c>INSTITUICAO</c>, com o rótulo para exibição.</param>
/// <param name="UnidadeId">Unidade do escopo; nula quando o escopo é a instituição.</param>
/// <param name="UnidadeSigla">Sigla da unidade; nula no escopo institucional e quando a unidade não existe mais.</param>
/// <param name="UnidadeNome">Nome da unidade; nulo no escopo institucional e quando a unidade não existe mais.</param>
/// <param name="ValidaAte">Fim da vigência; nulo quando a concessão não tem prazo.</param>
public sealed record PerfilConcedidoResponse(
    Guid ConcessaoId,
    Guid PerfilId,
    string Nome,
    CodigoComRotulo<TipoDeEscopo> EscopoTipo,
    Guid? UnidadeId,
    string? UnidadeSigla,
    string? UnidadeNome,
    DateTimeOffset? ValidaAte);
