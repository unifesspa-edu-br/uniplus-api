namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Nodes;

/// <summary>
/// Snapshot congelado vigente de um Processo Seletivo num instante (RN08,
/// ADR-0075/0076/0104). É o contrato de LEITURA que o runtime e os incrementos
/// downstream (inscrição, homologação, classificação) consomem — a configuração
/// CONGELADA, nunca a viva.
/// <para>
/// O documento normativo <b>não é de Seleção</b> (ADR-0103/0105): ele é o ato
/// publicado, e vive no módulo <c>Publicacoes</c>. O que este contrato publica
/// dele é a referência <b>por valor</b> — o par <see cref="AtoId"/> +
/// <see cref="HashEdital"/> (ADR-0061) —, e nada mais. Quem precisar dos atributos
/// documentais (tipo, número, data de publicação, assinante) consulta
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
/// <b>Consistência do ato.</b> O registro em <c>Publicacoes</c> acontece por mensagem
/// durável (ADR-0108), depois do commit da publicação. Entre o 204 e a drenagem do
/// outbox, <see cref="AtoId"/> já é válido e estável aqui, mas o <c>GET</c> do ato
/// pode ainda responder 404. A atomicidade que importa — configuração congelada e
/// requisição do ato na MESMA transação — está garantida: o ato não se perde.
/// </para>
/// <para>
/// A janela normalmente fecha em segundos. Ela <b>não</b> fecha quando o registro é
/// recusado no consumo (tipo removido do catálogo, vaga de linhagem tomada por fora): a
/// mensagem esgota as retentativas e vai para a dead letter, e aí o <see cref="AtoId"/>
/// aponta para um ato que não existirá. Não é silencioso — a dead letter é o alarme, e
/// as conferências prévias dos handlers existem justamente para tornar esse caminho
/// raro. Quem consome este contrato deve tratar o 404 do ato como transitório, não como
/// "certame sem configuração": a configuração está aqui, congelada, e é ela que governa.
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
