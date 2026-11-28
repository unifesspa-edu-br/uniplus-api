namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do conjunto básico da inscrição na forma do domínio, com o formato e a proteção que o catálogo dá a cada um.</summary>
internal static class ItemBasicoDeTeste
{
    /// <summary>O formato que o catálogo dá a cada texto do conjunto básico.</summary>
    private static readonly Dictionary<string, string> FormatoDosTextos = new(StringComparer.Ordinal)
    {
        ["NOME"] = "LIVRE",
        ["NOME_SOCIAL"] = "NOME_PESSOA",
        ["NOME_MAE"] = "NOME_PESSOA",
        ["NOME_PAI"] = "NOME_PESSOA",
        ["CPF"] = "CPF",
        ["EMAIL"] = "EMAIL",
        ["TELEFONE"] = "TELEFONE",
        ["RG_NUMERO"] = "LIVRE",
        ["RG_ORGAO_EMISSOR_OUTRO"] = "LIVRE",
        ["PASSAPORTE_NUMERO"] = "LIVRE",
        ["RNM_NUMERO"] = "LIVRE",
        ["NOME_COMUNIDADE"] = "LIVRE",
    };

    /// <summary>A classificação de proteção de dados que o catálogo dá a cada fato do conjunto básico.</summary>
    private static readonly Dictionary<string, string> ClassificacaoDosFatos = new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = "PUBLICO",
        ["CPF"] = "IDENTIFICADOR",
        ["RG_NUMERO"] = "IDENTIFICADOR",
        ["PASSAPORTE_NUMERO"] = "IDENTIFICADOR",
        ["RNM_NUMERO"] = "IDENTIFICADOR",
        ["IDENTIDADE_GENERO"] = "SENSIVEL",
        ["ORIENTACAO_SEXUAL"] = "SENSIVEL",
    };

    /// <summary>O item básico na forma do domínio, a partir da entrada do conjunto básico.</summary>
    internal static FatoColetado Basico(FatoColetadoInput entrada) =>
        FatoColetadoDeTeste.Coletando(entrada.FatoCodigo, entrada.Ordem.GetValueOrDefault())
            .ComRotulo(entrada.Apresentacao!.Rotulo!)
            .DoTipo(CatalogoDe<TipoDeCampo>.Obter(entrada.Apresentacao.TipoDeCampo!))
            .ComObrigatoriedade(ObrigatoriedadeDaEntrada(entrada))
            .ExibidoQuando(ExibicaoDaEntrada(entrada))
            .ComCopia(CopiaDoCatalogoDoBasico(entrada))
            .NaSecao(entrada.PaginaCodigo)
            .ComAjuda(entrada.Apresentacao.Ajuda)
            .ComRestricoes([.. (entrada.Regras!.Restricoes ?? []).Select(static r => EntradaDeRegras.Restricao(r).Value!)])
            .Construir();

    private static Obrigatoriedade ObrigatoriedadeDaEntrada(FatoColetadoInput entrada) =>
        EntradaDeRegras.Obrigatoriedade(
            entrada.Regras!.Obrigatoriedade!.Tipo, EntradaDeRegras.Predicado(entrada.Regras.Obrigatoriedade.Predicado).Value)!;

    private static CopiaDoItemNoCatalogo CopiaDoCatalogoDoBasico(FatoColetadoInput entrada) =>
        ProtecaoDoFatoDeTeste.Copia(
            ClassificacaoDosFatos.GetValueOrDefault(entrada.FatoCodigo, "PESSOAL"),
            entrada.FatoCodigo.EndsWith("_UF", StringComparison.Ordinal) ? Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemValoresColeta.UnidadesFederativas : default,
            FormatoDosTextos.GetValueOrDefault(entrada.FatoCodigo));

    private static PredicadoDnf? ExibicaoDaEntrada(FatoColetadoInput entrada) =>
        entrada.Regras!.Exibicao is not { Count: > 0 } clausulas
            ? null
            : PredicadoDnf.CriarDeCondicoesAgrupadas(
                [.. clausulas.SelectMany((clausula, indice) => clausula.Select(c =>
                    (indice, CondicaoDnf.Criar(c.Fato, CatalogoDe<Operador>.Obter(c.Operador!), c.Valor).Value!)))]).Value;
}
