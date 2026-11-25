namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;

internal static class InsercaoDeVersaoConfiguracao
{
    private static readonly string HashValido = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    /// <summary>
    /// INSERT cru na tabela de versões — o caminho que o agregado nunca toma, e
    /// que os guard rails do banco precisam recusar sozinhos.
    /// </summary>
    internal static string Criar(
        Guid processoId,
        int numeroVersao,
        Guid atoCriadorId,
        Guid? atoCriadorRetificaId,
        DateTimeOffset? vigenteAPartirDe = null)
    {
        string retifica = atoCriadorRetificaId is { } alvo
            ? $"'{alvo}'"
            : "NULL";
        string vigencia = vigenteAPartirDe is { } instante
            ? $"'{instante.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}'::timestamptz"
            : "now()";

        return MontarInsert(processoId, numeroVersao, atoCriadorId, retifica, vigencia);
    }

    // Bytes canônicos e jsonb mínimos e coerentes entre si ('{}' em UTF-8 é
    // 0x7b7d): o que estes testes atacam é a cadeia de versões, não o
    // conteúdo congelado — mas gravar bytes que não correspondem ao jsonb
    // plantaria uma evidência incoerente na tabela de prova.
    private static string MontarInsert(Guid processoId, int numeroVersao, Guid atoCriadorId, string retifica, string vigencia) =>
        $$"""
            INSERT INTO selecao.versoes_configuracao (
                id, processo_seletivo_id, numero_versao, vigente_a_partir_de,
                versao_do_formato, algoritmo_hash,
                bytes_da_configuracao, conteudo_da_configuracao, hash_configuracao,
                ato_criador_id, ato_criador_hash, ato_criador_retifica_id, ator_usuario_conta_id)
            VALUES (
                '{{Guid.CreateVersion7()}}', '{{processoId}}', {{numeroVersao.ToString(CultureInfo.InvariantCulture)}}, {{vigencia}},
                '1.0', 'canonical-json/sha256@v1',
                '\x7b7d'::bytea, '{}'::jsonb, '{{HashValido}}',
                '{{atoCriadorId}}', '{{HashValido}}', {{retifica}}, 'insert-cru')
            """;
}
