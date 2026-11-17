namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// O tipo do ato como o catálogo o descrevia no instante do registro, copiado por valor
/// (ADR-0075, ADR-0061): o código, o nome e os atributos de consequência. Editar o catálogo
/// depois não reescreve ato nenhum, e o valor nunca é relido para decidir fluxo (ADR-0103).
/// </summary>
/// <param name="Codigo">Código do tipo no catálogo.</param>
/// <param name="Nome">Nome do tipo no registro. Nulo nos atos registrados antes de o nome ser copiado.</param>
/// <param name="CongelaConfiguracao">Se o ato produz nova versão congelada da configuração.</param>
/// <param name="EfeitoIrreversivel">Se a publicação do ato não pode ser desfeita.</param>
/// <param name="UnicoPorObjeto">Se o objeto admite um único ato vivo deste tipo.</param>
public readonly record struct TipoDoAtoNoRegistro(
    string Codigo,
    string? Nome,
    bool CongelaConfiguracao,
    bool EfeitoIrreversivel,
    bool UnicoPorObjeto)
{
    /// <summary>Copia do tipo vigente do catálogo o que o ato guarda por valor.</summary>
    public static TipoDoAtoNoRegistro CopiadoDe(TipoAtoPublicado tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return new TipoDoAtoNoRegistro(
            tipo.Codigo, tipo.Nome, tipo.CongelaConfiguracao, tipo.EfeitoIrreversivel, tipo.UnicoPorObjeto);
    }
}
