namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A comparação, sem acesso a dados, entre o valor que uma permissão tinha e o do catálogo que entra: o que mudou e como a
/// base de cada concessão acompanha a mudança.
/// </summary>
public static class ComparacaoComOCatalogo
{
    /// <summary>A mudança de <paramref name="anterior"/> para o valor do catálogo novo; nulo quando nenhum dos eixos mudou.</summary>
    public static MudancaDaPermissao? MudancaDe(BaseDaConcessao anterior, LinhaDoRetratoDoCatalogo nova)
    {
        ArgumentNullException.ThrowIfNull(anterior);
        ArgumentNullException.ThrowIfNull(nova);

        MudancaDaPermissao mudanca = new(anterior.Classificacao, nova.Classificacao, anterior.DeUnidade, nova.DeUnidade);

        return mudanca.Mudou ? mudanca : null;
    }

    /// <summary>
    /// A base que a concessão passa a ter com a mudança do catálogo: a classe desce junto quando o catálogo desce abaixo
    /// dela, e o escopo fica com o anterior do catálogo quando ele muda, para que a mudança suspenda até a liberação.
    /// </summary>
    public static BaseDaConcessao BaseDaConciliacao(BaseDaConcessao atual, MudancaDaPermissao mudanca)
    {
        ArgumentNullException.ThrowIfNull(atual);
        ArgumentNullException.ThrowIfNull(mudanca);

        return new BaseDaConcessao(
            atual.Classificacao <= mudanca.ClassificacaoNova ? atual.Classificacao : mudanca.ClassificacaoNova,
            mudanca.DeUnidadeAnterior != mudanca.DeUnidadeNovo ? mudanca.DeUnidadeAnterior : atual.DeUnidade);
    }
}
