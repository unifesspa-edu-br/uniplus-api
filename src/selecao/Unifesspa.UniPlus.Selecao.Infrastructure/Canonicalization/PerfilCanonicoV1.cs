namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O perfil com que todo envelope das versões <c>1.0</c> a <c>1.4</c> foi congelado —
/// <c>canonical-json/sha256@v1</c>. <b>Congelado</b>: descreve o que já está publicado, e por
/// isso não muda mais.
/// </summary>
/// <remarks>
/// <para>
/// As regras, tais como são (ADR-0100):
/// </para>
/// <list type="number">
///   <item>chaves de todo objeto reordenadas recursivamente por comparação ordinal; arrays
///   preservam a ordem, que é semântica;</item>
///   <item><c>null</c> explícito é <b>preservado</b> — o envelope distingue “campo ausente”
///   de “campo presente e vazio”, e blocos publicados dependem disso (o número do ato ainda
///   não atribuído, por exemplo);</item>
///   <item>serialização por <c>Utf8JsonWriter</c> sem indentação e <b>sem encoder próprio</b>,
///   o que significa o encoder padrão do runtime: escapa aspas, contrabarra e controles nas
///   formas curtas, e escapa em <c>\uXXXX</c> com <b>hex maiúsculo</b> tudo o que estiver fora
///   do latim básico — inclusive os caracteres sensíveis a HTML (<c>&lt;</c>, <c>&gt;</c>,
///   <c>&amp;</c>, <c>'</c>, <c>+</c>) e toda letra acentuada;</item>
///   <item>digest SHA-256, hex minúsculo, sobre os bytes assim produzidos.</item>
/// </list>
/// <para>
/// O item 3 nunca foi decisão deliberada — é o comportamento padrão do serializador, e é
/// exatamente por isso que ele precisa estar escrito e coberto por vetores fixos. Um perfil
/// posterior pode preferir literais UTF-8 e hex minúsculo; o que ele não pode é <b>ser este</b>.
/// </para>
/// <para>
/// A implementação delega a <see cref="HashCanonicalComputer"/>, que continua sendo o código
/// v1 compartilhado. A consequência prática, e o motivo de este tipo existir: um perfil novo
/// se escreve <b>ao lado</b>, com serialização própria — nunca editando aquele computador, que
/// hoje está no caminho dos bytes de todo envelope já publicado.
/// </para>
/// </remarks>
public sealed class PerfilCanonicoV1 : IPerfilCanonico
{
    /// <summary>
    /// Instância única — o perfil não tem estado, e os encoders congelados (<c>static</c> por
    /// construção) referenciam esta constante em vez de receberem o perfil por parâmetro: a
    /// versão <c>1.1</c> emite sob o perfil <c>v1</c> por definição, não por configuração.
    /// </summary>
    public static readonly PerfilCanonicoV1 Instancia = new();

    private PerfilCanonicoV1()
    {
    }

    public string Algoritmo => "canonical-json/sha256@v1";

    public byte[] Serializar(JsonObject payload) => HashCanonicalComputer.ComputeSnapshotBytes(payload);

    public string HashHex(byte[] bytes) => HashCanonicalComputer.ComputeSha256Hex(bytes);
}
