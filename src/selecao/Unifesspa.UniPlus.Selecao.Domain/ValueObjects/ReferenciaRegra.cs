namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Referência imutável a uma regra do <c>regras_do_catalogo</c> pela sua identidade
/// content-addressable — <c>(codigo, versao, hash)</c>. É o que cada dimensão
/// da configuração do Processo Seletivo embute ao aplicar uma regra e o que o
/// snapshot de publicação congela (congelamento dos parâmetros na publicação, UNI-REQ-0019): o par <c>(codigo, versao)</c> aponta
/// a regra na biblioteca versionada e o <c>hash</c> prova que a definição
/// aplicada não mudou depois (lei muda → nova versão, não retroage a editais
/// publicados — reprodutibilidade por construção).
/// </summary>
/// <remarks>
/// <para>
/// A referência carrega apenas a <em>identidade</em> da regra. Os <em>parâmetros
/// aplicados</em> (ex.: <c>fator=1,20</c> de um <c>BONUS-MULTIPLICATIVO</c>,
/// <c>etapa_ref</c> de um <c>DESEMPATE-MAIOR-NOTA-ETAPA</c>) são tipados junto
/// da dimensão que consome a regra e validados contra o <c>esquema_args</c> da
/// versão referenciada — não moram aqui.
/// </para>
/// <para>
/// O <c>hash</c> é validado quanto ao formato (SHA-256 hex minúsculo, 64
/// chars); a existência efetiva da regra <c>(codigo, versao)</c> com aquele
/// <c>hash</c> é responsabilidade do handler que resolve a referência contra o
/// leitor do catálogo, não deste value object.
/// </para>
/// </remarks>
public sealed record ReferenciaRegra
{
    private ReferenciaRegra(string codigo, string versao, string hash)
    {
        Codigo = codigo;
        Versao = versao;
        Hash = hash;
    }

    public string Codigo { get; }
    public string Versao { get; }
    public string Hash { get; }

    /// <summary>
    /// Monta a referência a partir de uma identidade já conferida: <paramref name="codigo"/> e
    /// <paramref name="versao"/> não vazios e <paramref name="hash"/> no formato de um SHA-256
    /// hexadecimal minúsculo (64 caracteres).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A identidade viola o formato: a referência vem de uma regra do catálogo ou de um envelope cuja leitura
    /// já conferiu os três campos, e valor fora do formato aqui é defeito de quem chama.
    /// </exception>
    public static ReferenciaRegra De(string codigo, string versao, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        if (!HashCanonicalComputer.IsValidHashShape(hash))
        {
            throw new ArgumentException("O hash da regra deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).", nameof(hash));
        }

        return new ReferenciaRegra(codigo.Trim(), versao.Trim(), hash);
    }
}
