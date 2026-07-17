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
/// Entrada de uma base legal (Story #554, PR-c, issue #549, ADR-0074), usada por
/// <see cref="ItemDocumentoExigidoInput"/>. <see cref="Abrangencia"/>/<see cref="Status"/>
/// são tokens canônicos (<see cref="Domain.Enums.TipoAbrangenciaCodigo"/>/
/// <see cref="Domain.Enums.StatusBaseLegalCodigo"/>). A validação de <b>gate</b> (≥1
/// RESOLVIDO por exigência que determina resultado) é da publicação, não deste PUT —
/// aqui só a forma de cada item é validada.
/// </summary>
public sealed record BaseLegalInput(string Referencia, string Abrangencia, string Status, string? Observacao);

/// <summary>
/// Entrada de uma exigência documental, usada por
/// <see cref="DefinirDocumentosExigidosCommand"/>. O handler resolve
/// <see cref="TipoDocumentoId"/> contra o módulo Configuração e congela os
/// atributos vigentes por valor (snapshot-copy, ADR-0061) — o cliente não os
/// declara diretamente. <see cref="Condicoes"/> vazia é coerente com GERAL e com
/// CONDICIONAL "exigida de ninguém" (CA-01). <see cref="BasesLegais"/> vazia é um estado
/// válido na escrita — só vira pendência na publicação, quando a exigência determina
/// resultado (PR-c, CA-02).
/// </summary>
public sealed record ItemDocumentoExigidoInput(
    Guid ExigidoNaFaseId,
    Guid TipoDocumentoId,
    string Aplicabilidade,
    bool Obrigatorio,
    string? ConsequenciaIndeferimento,
    Guid? GrupoSatisfacaoId,
    IReadOnlyList<CondicaoGatilhoInput> Condicoes,
    IReadOnlyList<BaseLegalInput> BasesLegais);

/// <summary>
/// Substitui integralmente a coleção de documentos exigidos do processo (Story #554 —
/// núcleo da PR-a: fase, snapshot-copy do tipo de documento, aplicabilidade GERAL/
/// CONDICIONAL, obrigatoriedade, consequência de indeferimento e grupo de satisfação;
/// gatilho DNF dinâmico/multivalorado da PR-b; base legal 1:N da PR-c). A idade/formato/
/// tamanho (PR-d) chega em task-irmã.
/// </summary>
public sealed record DefinirDocumentosExigidosCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemDocumentoExigidoInput> Itens,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
