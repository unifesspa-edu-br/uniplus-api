namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Domain.ValueObjects;
using Kernel.Results;

/// <summary>
/// Entrada de uma exigência documental, usada por
/// <see cref="DefinirDocumentosExigidosCommand"/>. O handler resolve
/// <see cref="TipoDocumentoId"/> contra o módulo Configuração e congela os
/// atributos vigentes por valor (snapshot-copy, ADR-0061) — o cliente não os
/// declara diretamente.
/// </summary>
public sealed record ItemDocumentoExigidoInput(
    Guid ExigidoNaFaseId,
    Guid TipoDocumentoId,
    string Aplicabilidade,
    bool Obrigatorio,
    string? ConsequenciaIndeferimento,
    Guid? GrupoSatisfacaoId);

/// <summary>
/// Substitui integralmente a coleção de documentos exigidos do processo (Story #554,
/// PR-a — núcleo: fase, snapshot-copy do tipo de documento, aplicabilidade GERAL/
/// CONDICIONAL, obrigatoriedade, consequência de indeferimento e grupo de satisfação
/// como campos de transporte). O gatilho DNF (PR-b), a base legal (PR-c) e a idade/
/// formato/tamanho (PR-d) chegam em tasks-irmãs.
/// </summary>
public sealed record DefinirDocumentosExigidosCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemDocumentoExigidoInput> Itens,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
