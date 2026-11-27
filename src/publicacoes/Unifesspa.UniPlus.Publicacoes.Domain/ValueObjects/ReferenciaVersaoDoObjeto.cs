namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Referência, <b>por valor</b>, à versão do objeto a que um ato se refere
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
/// versão do objeto (ato autônomo, sem objeto versionado) — nesse caso a
/// referência simplesmente não existe (a propriedade fica nula no agregado).
/// </para>
/// <para>
/// Só se valida o <b>formato</b> do hash aqui. Que a versão <c>id</c> exista de
/// fato com aquele <c>hash</c> é responsabilidade de quem resolve a referência
/// no módulo de origem — Publicações a recebe já resolvida, por valor.
/// </para>
/// </remarks>
public sealed record ReferenciaVersaoDoObjeto
{
    private ReferenciaVersaoDoObjeto(Guid id, string hash)
    {
        Id = id;
        Hash = hash;
    }

    public Guid Id { get; }
    public string Hash { get; }

    /// <summary>
    /// A referência do par declarado, completa ou ausente (ambos nulos). Acumula as recusas do
    /// identificador e do hash, todas com o código de versão do objeto incompleta, cada uma apontando o
    /// campo que falta ou está errado.
    /// </summary>
    internal static ParteConferida<ReferenciaVersaoDoObjeto> DoPar(Guid? id, string? hash)
    {
        FieldError[] recusas = [.. new[] { RecusaDoId(id, hash), RecusaDoHash(id, hash) }.OfType<FieldError>()];

        return recusas.Length > 0
            ? ParteConferida<ReferenciaVersaoDoObjeto>.Recusada(recusas)
            : ParteConferida<ReferenciaVersaoDoObjeto>.Aceita(id is null ? null : new ReferenciaVersaoDoObjeto(id.Value, hash!));
    }

    private static FieldError? RecusaDoId(Guid? id, string? hash) =>
        (id, hash) switch
        {
            (null, not null) => Incompleta("versaoDoObjetoId", "Informe o identificador da versão do objeto, junto com o hash."),
            (var vazio, _) when vazio == Guid.Empty => Incompleta("versaoDoObjetoId", "O identificador da versão do objeto não pode ser vazio."),
            _ => null,
        };

    private static FieldError? RecusaDoHash(Guid? id, string? hash) =>
        (id, hash) switch
        {
            (not null, null) => Incompleta("versaoDoObjetoHash", "Informe o hash da versão do objeto, junto com o identificador."),
            (_, not null) when !HashSha256.TemFormatoValido(hash) =>
                Incompleta("versaoDoObjetoHash", "O hash da versão do objeto deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres)."),
            _ => null,
        };

    private static FieldError Incompleta(string campo, string mensagem) =>
        new(campo, new DomainError(AtoNormativoErrorCodes.VersaoDoObjetoIncompleta, mensagem));
}
