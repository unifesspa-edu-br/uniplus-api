namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Domain.ValueObjects;
using Kernel.Results;

/// <summary>
/// Entrada de uma condição do gatilho DNF (Story #554, PR-b), usada por
/// <see cref="ItemDocumentoExigidoInput"/>. <see cref="Operador"/>/<see cref="Valor"/>
/// seguem o mesmo formato flat de wire de <c>CriterioDesempateInput</c> (tokens
/// canônicos <see cref="Domain.Enums.OperadorCodigo"/>; <see cref="Valor"/> é texto,
/// interpretado como JSON quando possível).
/// </summary>
public sealed record CondicaoGatilhoInput(int Clausula, string Fato, string Operador, string Valor);

/// <summary>
/// Entrada de uma exigência documental, usada por
/// <see cref="DefinirDocumentosExigidosCommand"/>. O handler resolve
/// <see cref="TipoDocumentoId"/> contra o módulo Configuração e congela os
/// atributos vigentes por valor (snapshot-copy, ADR-0061) — o cliente não os
/// declara diretamente. <see cref="Condicoes"/> vazia é coerente com GERAL e com
/// CONDICIONAL "exigida de ninguém" (CA-01).
/// </summary>
public sealed record ItemDocumentoExigidoInput(
    Guid ExigidoNaFaseId,
    Guid TipoDocumentoId,
    string Aplicabilidade,
    bool Obrigatorio,
    string? ConsequenciaIndeferimento,
    Guid? GrupoSatisfacaoId,
    IReadOnlyList<CondicaoGatilhoInput> Condicoes);

/// <summary>
/// Substitui integralmente a coleção de documentos exigidos do processo (Story #554 —
/// núcleo da PR-a: fase, snapshot-copy do tipo de documento, aplicabilidade GERAL/
/// CONDICIONAL, obrigatoriedade, consequência de indeferimento e grupo de satisfação;
/// gatilho DNF dinâmico/multivalorado da PR-b). A base legal (PR-c) e a idade/formato/
/// tamanho (PR-d) chegam em tasks-irmãs.
/// </summary>
public sealed record DefinirDocumentosExigidosCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemDocumentoExigidoInput> Itens,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
