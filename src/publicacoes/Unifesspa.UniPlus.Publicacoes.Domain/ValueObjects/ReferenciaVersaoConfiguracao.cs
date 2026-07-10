namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Referência, <b>por valor</b>, à versão de configuração que governou um ato
/// no instante em que foi publicado — o par <c>{id, hash}</c> (ADR-0075). O
/// <c>id</c> aponta a versão no módulo que a produziu (Seleção); o <c>hash</c>
/// prova que a definição aplicada não mudou depois. Não há chave estrangeira
/// cruzando módulo (ADR-0061/0105): a versão vive noutro schema, e a integridade
/// referencial é substituída pela prova criptográfica do hash.
/// </summary>
/// <remarks>
/// <para>
/// O par é <b>completo ou ausente</b>: um identificador sem hash não prova nada,
/// então a factory recusa qualquer uma das metades isolada. Um ato pode não ter
/// versão invocada (ato autônomo, sem vínculo com configuração) — nesse caso a
/// referência simplesmente não existe (a propriedade fica nula no agregado).
/// </para>
/// <para>
/// Só se valida o <b>formato</b> do hash aqui. Que a versão <c>id</c> exista de
/// fato com aquele <c>hash</c> é responsabilidade de quem resolve a referência
/// no módulo de origem — Publicações a recebe já resolvida, por valor.
/// </para>
/// </remarks>
public sealed record ReferenciaVersaoConfiguracao
{
    private ReferenciaVersaoConfiguracao(Guid id, string hash)
    {
        Id = id;
        Hash = hash;
    }

    public Guid Id { get; }
    public string Hash { get; }

    /// <summary>
    /// Cria a referência validando que <paramref name="id"/> não é
    /// <see cref="Guid.Empty"/> e que <paramref name="hash"/> tem o formato de um
    /// SHA-256 hex minúsculo (64 caracteres).
    /// </summary>
    public static Result<ReferenciaVersaoConfiguracao> Criar(Guid id, string hash)
    {
        if (id == Guid.Empty)
        {
            return Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                "ReferenciaVersaoConfiguracao.IdObrigatorio",
                "Identificador da versão de configuração é obrigatório."));
        }

        if (!HashSha256.TemFormatoValido(hash))
        {
            return Result<ReferenciaVersaoConfiguracao>.Failure(new DomainError(
                "ReferenciaVersaoConfiguracao.HashInvalido",
                "Hash da versão de configuração deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres)."));
        }

        return Result<ReferenciaVersaoConfiguracao>.Success(
            new ReferenciaVersaoConfiguracao(id, hash));
    }
}
