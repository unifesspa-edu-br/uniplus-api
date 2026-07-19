namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Entrada de uma condição do gatilho DNF (Story #554, PR #896), usada por
/// <see cref="ItemDocumentoExigidoInput"/>. <see cref="Operador"/>/<see cref="Valor"/>
/// seguem o mesmo formato flat de wire de <c>CriterioDesempateInput</c> (tokens
/// canônicos <see cref="Domain.Enums.OperadorCodigo"/>; <see cref="Valor"/> é texto,
/// interpretado como JSON quando possível).
/// </summary>
public sealed record CondicaoGatilhoInput(int Clausula, string Fato, string Operador, string Valor);

/// <summary>
/// Entrada de uma base legal (Story #554, PR #898, issue #549, ADR-0074), usada por
/// <see cref="ItemDocumentoExigidoInput"/>. <see cref="Abrangencia"/>/<see cref="Status"/>
/// são tokens canônicos (<see cref="Domain.Enums.TipoAbrangenciaCodigo"/>/
/// <see cref="Domain.Enums.StatusBaseLegalCodigo"/>). A validação de <b>gate</b> (≥1
/// RESOLVIDO por exigência que determina resultado) é da publicação, não deste PUT —
/// aqui só a forma de cada item é validada.
/// </summary>
public sealed record BaseLegalInput(string Referencia, string Abrangencia, string Status, string? Observacao);

/// <summary>
/// Entrada da idade máxima de emissão (Story #554, PR #900, issue #893), usada por
/// <see cref="ItemDocumentoExigidoInput"/>. Tudo-nulo (os 5 campos) é a variante "regra
/// ausente" — mesmo padrão do comando de <c>DefinirReferenciaTemporalFatos</c> (PR #896), mas
/// aninhado por item em vez de flat no comando, porque aqui a regra é 1 por exigência, não
/// 1 por processo. <see cref="Unidade"/>/<see cref="ReferenciaTipo"/> são tokens canônicos
/// (<see cref="Domain.Enums.UnidadeIdadeCodigo"/>/<see cref="Domain.Enums.ReferenciaTipoIdadeEmissaoCodigo"/>).
/// </summary>
public sealed record IdadeMaximaEmissaoInput(
    int? Valor, string? Unidade, string? ReferenciaTipo, DateOnly? Data, Guid? ReferenciaFaseId);

/// <summary>
/// Entrada de uma exigência documental, usada por
/// <see cref="DefinirDocumentosExigidosCommand"/>. O handler resolve
/// <see cref="TipoDocumentoId"/> contra o módulo Configuração e congela os
/// atributos vigentes por valor (snapshot-copy, ADR-0061) — o cliente não os
/// declara diretamente. <see cref="Condicoes"/> vazia é coerente com GERAL e com
/// CONDICIONAL "exigida de ninguém" (CA-01). <see cref="BasesLegais"/> vazia é um estado
/// válido na escrita — só vira pendência na publicação, quando a exigência determina
/// resultado (PR #898, CA-02). <see cref="IdadeMaximaEmissao"/>/<see cref="TamanhoMaximoBytes"/>
/// (PR #900, issue #893) são aviso, não bloqueio de presença — congelados por chamada,
/// sem gate de publicação.
/// </summary>
/// <remarks>
/// <see cref="FormatosPermitidos"/> (Story #918) é um valor JSON polimórfico — o mesmo
/// tratamento já usado por <see cref="CondicaoGatilhoInput.Valor"/>/<c>CondicaoDnf.Valor</c>
/// (ADR-0111): a string <c>"QUALQUER"</c> OU um array de <c>{formato, tamanhoMaximoBytesMax}</c>,
/// nunca um DTO discriminado próprio. Campo agora OBRIGATÓRIO — <see langword="null"/>
/// estrutural (propriedade ausente do JSON, ou <c>null</c> explícito) é distinto de "veio,
/// mas em forma inválida": o handler produz <c>FormatosPermitidos.Obrigatorio</c> no
/// primeiro caso, <c>FormatosPermitidos.FormaInvalida</c> no segundo.
/// </remarks>
public sealed record ItemDocumentoExigidoInput(
    Guid ExigidoNaFaseId,
    Guid TipoDocumentoId,
    string Aplicabilidade,
    bool Obrigatorio,
    string? ConsequenciaIndeferimento,
    Guid? GrupoSatisfacaoId,
    IReadOnlyList<CondicaoGatilhoInput> Condicoes,
    IReadOnlyList<BaseLegalInput> BasesLegais,
    IdadeMaximaEmissaoInput? IdadeMaximaEmissao,
    JsonElement? FormatosPermitidos,
    int? TamanhoMaximoBytes);

/// <summary>
/// Substitui integralmente a coleção de documentos exigidos do processo (Story #554 —
/// núcleo da PR #895: fase, snapshot-copy do tipo de documento, aplicabilidade GERAL/
/// CONDICIONAL, obrigatoriedade, consequência de indeferimento e grupo de satisfação;
/// gatilho DNF dinâmico/multivalorado da PR #896; base legal 1:N da PR #898; idade de emissão/
/// formato/tamanho da PR #900).
/// </summary>
public sealed record DefinirDocumentosExigidosCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemDocumentoExigidoInput> Itens,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
