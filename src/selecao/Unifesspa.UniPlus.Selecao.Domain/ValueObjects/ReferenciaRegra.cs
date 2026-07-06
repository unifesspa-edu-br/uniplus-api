namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Referência imutável a uma regra do <c>rol_de_regras</c> pela sua identidade
/// content-addressable — <c>(codigo, versao, hash)</c>. É o que cada dimensão
/// da configuração do Processo Seletivo embute ao aplicar uma regra e o que o
/// snapshot de publicação congela (RN08): o par <c>(codigo, versao)</c> aponta
/// a regra na biblioteca versionada e o <c>hash</c> prova que a definição
/// aplicada não mudou depois (lei muda → nova versão, não retroage a editais
/// publicados — reprodutibilidade por construção).
/// </summary>
/// <remarks>
/// <para>
/// A referência carrega apenas a <em>identidade</em> da regra. Os <em>args
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
    /// Cria a referência validando que <paramref name="codigo"/> e
    /// <paramref name="versao"/> não são vazios e que <paramref name="hash"/>
    /// tem o formato de um SHA-256 hex minúsculo (64 chars).
    /// </summary>
    public static Result<ReferenciaRegra> Criar(string codigo, string versao, string hash)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<ReferenciaRegra>.Failure(new DomainError(
                "ReferenciaRegra.CodigoObrigatorio", "Código da regra é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versao))
        {
            return Result<ReferenciaRegra>.Failure(new DomainError(
                "ReferenciaRegra.VersaoObrigatoria", "Versão da regra é obrigatória."));
        }

        if (!HashCanonicalComputer.IsValidHashShape(hash))
        {
            return Result<ReferenciaRegra>.Failure(new DomainError(
                "ReferenciaRegra.HashInvalido",
                "Hash da regra deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres)."));
        }

        return Result<ReferenciaRegra>.Success(
            new ReferenciaRegra(codigo.Trim(), versao.Trim(), hash));
    }
}
