namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json.Serialization;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Cria a raiz do agregado <c>ProcessoSeletivo</c> em rascunho (CA-01 da
/// Story #758). Validado pelo middleware FluentValidation do Wolverine via
/// <c>CriarProcessoSeletivoCommandValidator</c>.
/// </summary>
/// <param name="TipoProcessoOrigemId">Id de um tipo de processo ativo em Configuração, resolvido e congelado na criação.</param>
/// <param name="OrigemCandidatos">De onde vêm os candidatos (Story #851 §3.4) — NOT NULL, exigido na criação.</param>
/// <param name="UnidadeAdministradoraOrigemId">Quem responde pelo certame (CA-04 da Feature #40; issue #849) — NOT NULL, exigido na criação, resolvido via <c>IUnidadeReader</c>.</param>
[method: JsonConstructor]
public sealed record CriarProcessoSeletivoCommand(
    string Nome,
    Guid TipoProcessoOrigemId,
    OrigemCandidatos OrigemCandidatos,
    Guid UnidadeAdministradoraOrigemId) : ICommand<Result<Guid>>
{
    /// <summary>Construtor de fixtures; o contrato HTTP canônico recebe somente o Id de origem.</summary>
    public CriarProcessoSeletivoCommand(
        string nome,
        TipoProcessoSnapshot? tipoProcesso,
        OrigemCandidatos origemCandidatos,
        Guid unidadeAdministradoraOrigemId)
        : this(nome, tipoProcesso?.OrigemId ?? Guid.Empty, origemCandidatos, unidadeAdministradoraOrigemId)
    {
    }
}
