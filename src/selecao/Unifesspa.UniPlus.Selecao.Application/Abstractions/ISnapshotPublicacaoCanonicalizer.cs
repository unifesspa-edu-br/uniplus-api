namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

/// <summary>
/// Bytes canônicos + metadados de um snapshot de publicação, prontos para
/// <c>ProcessoSeletivo.Publicar</c> congelar (ADR-0100). Não carrega hash —
/// <c>VersaoConfiguracao.Abrir</c> deriva o hash dos bytes internamente
/// (revisão de plano, evita divergência entre bytes e hash persistidos).
/// </summary>
#pragma warning disable CA1819 // Properties should not return arrays — bytes canônicos, sem value-equality de record aplicável.
public sealed record SnapshotCanonico(byte[] Bytes, string SchemaVersion, string AlgoritmoHash);
#pragma warning restore CA1819

/// <summary>
/// Informação do ato de retificação (ADR-0103) acrescentada ao envelope como um
/// bloco adicional (<c>retificacao</c>) além dos 17 blocos canônicos.
/// <see langword="null"/> na publicação de abertura — o envelope de abertura
/// mantém exatamente os 17 blocos, sem o bloco de retificação.
/// </summary>
public sealed record RetificacaoInfo(Guid EditalRetificadoId, string Motivo);

/// <summary>
/// Entrada <b>única e explícita</b> da canonicalização (ADR-0109 D6).
/// </summary>
/// <remarks>
/// <para>
/// O canonicalizador é uma <b>projeção pura</b>: função total desta entrada
/// para bytes. Não lê repositório, não lê relógio, não é assíncrono — o que
/// não é alcançável a partir do agregado <b>entra por aqui</b>, montado pelo
/// handler (Application), que é quem tem os repositórios.
/// </para>
/// <para>
/// É esta a extensão que os blocos ainda não construídos vão usar: o catálogo
/// de obrigatoriedades legais e o quadro de vagas não pertencem ao agregado
/// <see cref="ProcessoSeletivo"/>. Injetar um repositório no canonicalizador —
/// ou no domínio — inverteria a dependência (ADR-0042). Acrescentar um campo a
/// este record, não.
/// </para>
/// </remarks>
/// <param name="Processo">Agregado com a configuração viva a congelar.</param>
/// <param name="Dados">Dados documentais do ato (número, período, documento).</param>
/// <param name="HashDocumento">SHA-256 do PDF confirmado do ato.</param>
/// <param name="Retificacao">Presente apenas quando o ato é de retificação.</param>
/// <param name="Conformidade">
/// Veredicto da conformidade legal (Story #853 §3.4), montado pelo handler a partir do
/// catálogo <c>ObrigatoriedadeLegal</c> — o canonicalizador não injeta repositório
/// (ADR-0042). Só regras aprovadas chegam aqui: se qualquer uma reprovasse, o handler já
/// teria recusado a transição antes de canonicalizar.
/// </param>
public sealed record EntradaCanonicalizacao(
    ProcessoSeletivo Processo,
    DadosEdital Dados,
    string HashDocumento,
    RetificacaoInfo? Retificacao = null,
    ResultadoConformidade? Conformidade = null);

/// <summary>
/// Porta da projeção canônica do envelope de congelamento (ADR-0100, ADR-0109).
/// Projeta a configuração viva do <see cref="ProcessoSeletivo"/> num payload de
/// <b>17 chaves</b> — hoje <b>13 blocos reais + 4 stubs</b>
/// <c>{"status":"nao_construido"}</c> para as dimensões que a Feature #40 ainda
/// não implementou — e devolve os bytes que <c>VersaoConfiguracao.Abrir</c>
/// persiste como base do hash. Quando a entrada carrega
/// <see cref="EntradaCanonicalizacao.Retificacao"/>, acrescenta o 18º bloco
/// <c>retificacao</c> preservando os 17 anteriores intactos (ADR-0103).
/// </summary>
/// <remarks>
/// A assinatura recebe <b>um único</b> parâmetro por decisão (ADR-0109 D6):
/// acrescentar dado ao envelope não pode significar mudar a assinatura da porta
/// a cada story. O contrato de pureza é travado por ArchTest.
/// </remarks>
public interface ISnapshotPublicacaoCanonicalizer
{
    SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada);
}
