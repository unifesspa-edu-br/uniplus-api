namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Nodes;

/// <summary>
/// Snapshot congelado vigente de um Processo Seletivo num instante (congelamento dos parâmetros na publicação, UNI-REQ-0019,
/// ADR-0075/0076/0104). É o contrato de LEITURA que o runtime e os incrementos
/// downstream (inscrição, homologação, classificação) consomem — a configuração
/// CONGELADA, nunca a viva.
/// <para>
/// O documento normativo <b>não é de Seleção</b> (ADR-0103/0105): ele é o ato
/// publicado, e vive no módulo <c>Publicacoes</c>. O que este contrato publica
/// dele é a referência <b>por valor</b> — o par <see cref="AtoId"/> +
/// <see cref="HashEdital"/> (ADR-0061) —, e nada mais. Quem precisar dos atributos
/// documentais (tipo, número, data do ato, assinante) consulta
/// <c>GET /api/publicacoes/atos/{atoId}</c>, que é a fonte única deles. Republicá-los
/// aqui seria manter a posse do documento por outra porta, e obrigaria a Seleção a
/// saber o que um ato <i>é</i> — o acoplamento que a ADR-0103 desfez.
/// </para>
/// <para>
/// <see cref="SnapshotPublicacaoId"/> é a referência forense DURÁVEL da
/// <c>VersaoConfiguracao</c> que governa o ato: por ADR-0075 o ato grava esse id
/// para recarregar exatamente a mesma configuração em re-avaliações futuras. É o
/// mesmo identificador que o <c>ProcessoPublicadoEvent</c> já carrega no Kafka. Os
/// nomes <c>SnapshotPublicacaoId</c> e <see cref="HashEdital"/> são os históricos:
/// o mecanismo mudou, o contrato publicado não. <see cref="HashConfiguracao"/> dá a
/// identidade endereçável por conteúdo (integridade/ETag).
/// </para>
/// <para>
/// <b>Consistência do ato.</b> O ato nasce em <c>Publicacoes</c> na mesma transação que congela a
/// configuração, pela porta de escrita do registro central (ADR-0170): a versão e o ato existem
/// juntos, ou nenhum dos dois, e o <c>GET</c> do <see cref="AtoId"/> responde assim que a publicação confirma.
/// </para>
/// </summary>
public sealed record SnapshotVigenteDto(
    Guid SnapshotPublicacaoId,
    Guid AtoId,
    string SchemaVersion,
    string AlgoritmoHash,
    string HashConfiguracao,
    string HashEdital,
    JsonNode Configuracao);
