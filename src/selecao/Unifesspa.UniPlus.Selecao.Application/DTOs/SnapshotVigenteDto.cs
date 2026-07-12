namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Nodes;

/// <summary>
/// Snapshot congelado vigente de um Processo Seletivo num instante (RN08,
/// Story #759 T6 #787, ADR-0075/0076). É o contrato de LEITURA que o runtime e
/// os incrementos downstream (inscrição, homologação, classificação) consomem
/// — a configuração CONGELADA, nunca a viva.
/// <para>
/// <see cref="SnapshotPublicacaoId"/> é a referência forense DURÁVEL da
/// <c>VersaoConfiguracao</c> que governa o ato: por ADR-0075 o ato grava esse
/// id para poder recarregar exatamente a mesma configuração em re-avaliações
/// futuras. É o mesmo identificador público que o <c>ProcessoPublicadoEvent</c>
/// já carrega no Kafka — não um id interno vazado. O nome do campo é o
/// histórico: o contrato HTTP publicado não muda com a ADR-0104, só o
/// mecanismo que o resolve. O id do <c>Edital</c> (entidade interna do
/// agregado) permanece encapsulado; <see cref="HashConfiguracao"/> dá a
/// identidade endereçável por conteúdo (verificação de integridade/ETag), e
/// <see cref="HashEdital"/> é o hash do documento do ato que congelou a versão.
/// </para>
/// </summary>
public sealed record SnapshotVigenteDto(
    Guid SnapshotPublicacaoId,
    DateTimeOffset DataPublicacao,
    string Natureza,
    string SchemaVersion,
    string AlgoritmoHash,
    string HashConfiguracao,
    string HashEdital,
    JsonNode Configuracao);
