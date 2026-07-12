namespace Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// SPIKE #820 — requisição durável de registro de ato normativo: a mensagem que um
/// domínio (Seleção ao publicar um Edital, Ingresso ao convocar uma chamada)
/// enfileira, na MESMA transação em que publica, para que Publicações registre o
/// ato correspondente.
/// </summary>
/// <remarks>
/// <para>
/// O <see cref="AtoId"/> é decidido pelo DOMÍNIO que publica, não por Publicações.
/// É o que torna a reentrega idempotente: a fila é at-least-once, e um segundo
/// processamento tenta gravar o MESMO id — que a chave primária recusa. Sem isso,
/// uma reentrega criaria um segundo ato, e o segundo ocuparia a vaga de linhagem do
/// objeto (ADR-0107) contra a linhagem do primeiro.
/// </para>
/// <para>
/// É também o que permite ao domínio referenciar o ato ANTES de ele existir
/// fisicamente: <c>VersaoConfiguracao.AtoCriadorId</c> é referência por valor, sem
/// chave estrangeira (ADR-0061) — o modelo já previa que o ato viveria noutro módulo.
/// </para>
/// </remarks>
public sealed record RegistrarAtoNormativoRequisicao(
    Guid AtoId,
    string Orgao,
    string Serie,
    int Ano,
    string? Numero,
    string TipoCodigo,
    DateOnly DataPublicacao,
    string DocumentoHash,
    string Assinante,
    Guid? VersaoInvocadaId,
    string? VersaoInvocadaHash,
    Guid? AtoRetificadoId,
    string? MotivoRetificacao,
    IReadOnlyList<VinculoEntidadeRequisicao> Vinculos);

/// <summary>
/// Entidade de outro domínio a que o ato se liga (ADR-0105). O par é opaco para
/// Publicações: sem lista de valores permitidos e sem chave estrangeira.
/// </summary>
public sealed record VinculoEntidadeRequisicao(string EntidadeTipo, Guid EntidadeId);
