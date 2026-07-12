namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Registra um ato publicado. O tipo é resolvido no catálogo vigente na
/// <paramref name="DataPublicacao"/> e seus atributos de consequência
/// (<c>congela_configuracao</c>, <c>efeito_irreversivel</c>) são copiados por
/// valor server-side — o cliente não os informa, então não há como divergirem
/// do catálogo (AC5).
/// </summary>
/// <remarks>
/// <para>O par <see cref="VersaoInvocadaId"/>/<see cref="VersaoInvocadaHash"/> é
/// recebido por valor, completo ou ausente (AC7): Publicações não resolve a
/// versão de configuração — ela vive noutro módulo e chega já resolvida.</para>
/// <para>O par <see cref="AtoRetificadoId"/>/<see cref="MotivoRetificacao"/> torna
/// o registro uma retificação (ADR-0103), quando presente — simétrico (um existe
/// se e somente se o outro existe). É o mesmo comando de publicar: retificar é
/// publicar um ato que emenda outro.</para>
/// <para><see cref="AtoId"/> é o id que o DOMÍNIO que publica decidiu para o ato, quando
/// o registro chega por mensagem durável (ADR-0108) — é o que torna a reentrega da fila
/// idempotente. Fica FORA do wire HTTP (<c>JsonIgnore</c>): quem publica pelo endpoint é o
/// operador em Publicações, não há domínio remoto a quem o id pertença, e aceitá-lo do
/// cliente deixaria forjar a identidade de um ato. Ausente, Publicações o gera.</para>
/// <para><see cref="Vinculos"/> liga o ato às entidades de que ele trata (ADR-0105).
/// É o domínio quem os informa — <c>Selecao</c> ao publicar um edital, <c>Ingresso</c>
/// ao convocar uma chamada —, e Publicações os guarda sem interpretar. Ausente ou vazio
/// num ato que não trata de objeto algum, o que é legítimo.</para>
/// <para>Uma retificação <b>herda</b> os vínculos do ato que emenda, e não precisa
/// repeti-los: quem emenda um ato trata do mesmo objeto que ele, e uma retificação que
/// não os herdasse sumiria da consulta do certame — que passaria a exibir a versão
/// superada, escondendo a que a emenda. Declarar entidades novas continua permitido: o
/// ato retificador pode passar a tratar de objeto que o retificado não declarava.</para>
/// </remarks>
public sealed record RegistrarAtoNormativoCommand(
    string Orgao,
    string Serie,
    int Ano,
    string? Numero,
    string TipoCodigo,
    DateOnly DataPublicacao,
    string DocumentoHash,
    string Assinante,
    Guid? VersaoInvocadaId = null,
    string? VersaoInvocadaHash = null,
    Guid? AtoRetificadoId = null,
    string? MotivoRetificacao = null,
    IReadOnlyList<VinculoEntidadeInput>? Vinculos = null,
    [property: JsonIgnore] Guid? AtoId = null,
    [property: JsonIgnore] AtributosDoTipoAto? AtributosDoTipo = null) : ICommand<Result<RegistrarAtoNormativoResult>>;

/// <summary>
/// Entidade de outro domínio a que o ato se liga. O par é <b>opaco</b> para
/// Publicações: <paramref name="EntidadeTipo"/> não tem lista de valores permitidos
/// (só forma canônica UPPER_SNAKE), e <paramref name="EntidadeId"/> não tem chave
/// estrangeira — o módulo não conhece, e não deve conhecer, o que há do outro lado.
/// </summary>
public sealed record VinculoEntidadeInput(string EntidadeTipo, Guid EntidadeId);
