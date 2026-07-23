namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

/// <summary>
/// As regras de <b>bytes</b> de uma versão do envelope: como um payload vira a sequência
/// exata de octetos que o certame congela, e qual digest se extrai dela.
/// </summary>
/// <remarks>
/// <para>
/// A <c>schema_version</c> versiona a <b>forma do payload</b> — quais blocos existem, o que
/// cada um carrega. Ela não diz nada sobre como aquele payload vira bytes: ordenação de
/// chaves, tabela de escapes, forma dos números, o que fazer com <c>null</c>. Essas regras
/// são o <b>perfil canônico</b>, identificado à parte (<see cref="Algoritmo"/>) e persistido
/// junto do envelope em <c>versao_configuracao.algoritmo_hash</c>.
/// </para>
/// <para>
/// Duas coisas distintas, dois eixos de versionamento: um bloco novo sobe a schema_version e
/// mantém o perfil; uma regra de escape diferente mantém a forma e <b>troca o perfil</b>. Sem
/// essa separação, a única maneira de mudar as regras de serialização seria editar o código
/// compartilhado por todos os codecs — e um envelope publicado há um ano deixaria de reproduzir
/// os próprios bytes, em silêncio, no dia em que alguém trocasse um <c>Encoder</c>.
/// </para>
/// <para>
/// Um perfil é <b>imutável depois que emite o primeiro envelope</b>. Mudar qualquer regra
/// daqui para frente é criar um perfil NOVO, com identificador novo, e apontar para ele apenas
/// as versões de schema que nascem depois.
/// </para>
/// </remarks>
public interface IPerfilCanonico
{
    /// <summary>
    /// Identificador do perfil, gravado com o envelope — ex.: <c>canonical-json/sha256@v1</c>.
    /// É parte da evidência, não rótulo: é por ele que a reidratação sabe com que regras os
    /// bytes que está lendo foram produzidos.
    /// </summary>
    string Algoritmo { get; }

    /// <summary>
    /// Transforma o payload nos bytes canônicos do perfil.
    /// </summary>
    /// <exception cref="PayloadForaDoPerfilCanonicoException">
    /// Quando o payload viola uma regra que o perfil recusa em vez de normalizar. Perfis mais
    /// estritos que o <c>v1</c> não “consertam” o payload em silêncio — um valor que o perfil
    /// não sabe representar tem de estourar aqui, e não virar bytes que ninguém pediu.
    /// </exception>
    byte[] Serializar(JsonObject payload);

    /// <summary>
    /// Digest hex minúsculo dos bytes <b>já canônicos</b> — sobre a evidência lida, nunca sobre
    /// um payload reconstruído. Separado de <see cref="Serializar"/> de propósito: o gate de
    /// integridade hasheia os bytes que vieram do banco; o gate de forma reserializa e compara.
    /// Fundi-los convidaria a hashear o que se acabou de gerar, que é justamente o que não
    /// prova nada.
    /// </summary>
    string HashHex(byte[] bytes);
}
