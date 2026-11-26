namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>Dados entendidos e recusas da declaração de um item de exigência.</summary>
public sealed record DeclaracaoDaRegraDeComprovacao(
    FormaDeComprovacao? Forma, bool TemDocumento, int QuantidadeDeItens, int? QuantidadeMinima,
    string? ConsequenciaDoDescumprimento, DistincaoDeclarada? Distincao, IReadOnlyList<FieldError> Recusas)
{
    public static DeclaracaoDaRegraDeComprovacao Interpretar(string? forma, bool temDocumento, int quantidadeDeItens, int? quantidadeMinima,
        string? consequenciaDoDescumprimento = null, bool temBaseLegal = false, DistincaoDeclarada? distincao = null)
    {
        FormaDeComprovacao? entendido = CatalogoDe<FormaDeComprovacao>.TentarObter(forma, out FormaDeComprovacao formaReconhecida) ? formaReconhecida : null;
        List<FieldError> recusas = RecusarDeclaracao(entendido, temDocumento, quantidadeDeItens, quantidadeMinima);
        recusas.AddRange(RecusarConsequenciaEBase(entendido, consequenciaDoDescumprimento, temBaseLegal));
        if (entendido == FormaDeComprovacao.Documento && distincao is not null)
        {
            recusas.AddRange(ConferenciaDaDistincao.Conferir(distincao, quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao));
        }
        int? quantidade = entendido is FormaDeComprovacao.Documento or FormaDeComprovacao.QuantidadeMinimaDeItens
            ? quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao : quantidadeMinima;
        return new(entendido, temDocumento, quantidadeDeItens, quantidade, CombinacaoDoGrupo.NormalizarConsequencia(consequenciaDoDescumprimento), distincao, recusas);
    }

    private static List<FieldError> RecusarDeclaracao(FormaDeComprovacao? forma, bool temDocumento, int quantidadeDeItens, int? quantidadeMinima)
    {
        List<FieldError> recusas = [];
        if (forma is null)
        {
            recusas.Add(new("forma", new DomainError("RegraDeComprovacao.FormaInvalida",
                "A forma de comprovação não é reconhecida. Escolha \"DOCUMENTO\", \"TODOS_OS_ITENS\" ou \"QUANTIDADE_MINIMA_DE_ITENS\".")));
        }
        if (forma == FormaDeComprovacao.Documento && !temDocumento)
        {
            recusas.Add(new("documento", new DomainError("RegraDeComprovacao.DocumentoObrigatorio",
                "A regra de documento não informa qual documento é exigido. Escolha o documento no catálogo.")));
        }
        recusas.AddRange(RecusarCamposIncompativeis(forma, temDocumento, quantidadeDeItens > 0));
        recusas.AddRange(RecusarQuantidade(forma, quantidadeDeItens, quantidadeMinima));
        return recusas;
    }

    private static IEnumerable<FieldError> RecusarConsequenciaEBase(FormaDeComprovacao? forma, string? consequenciaDoDescumprimento, bool temBaseLegal)
    {
        if (forma is not { } formaValida)
        {
            yield break;
        }

        if (CombinacaoDoGrupo.RecusaDaConsequencia(formaValida, consequenciaDoDescumprimento) is { } consequenciaRecusada)
        {
            yield return new("consequenciaDoDescumprimento", consequenciaRecusada);
        }
        if (CombinacaoDoGrupo.RecusaDaBaseLegal(formaValida, consequenciaDoDescumprimento, temBaseLegal) is { } baseRecusada)
        {
            yield return new("basesLegais", baseRecusada);
        }
    }

    private static IEnumerable<FieldError> RecusarQuantidade(FormaDeComprovacao? forma, int quantidadeDeItens, int? quantidadeMinima)
    {
        if (forma is FormaDeComprovacao.TodosOsItens or FormaDeComprovacao.QuantidadeMinimaDeItens && ComposicaoDoGrupo.RecusaSeVazio(quantidadeDeItens) is { } vazio)
        {
            yield return new("itens", vazio);
        }
        DomainError? quantidade = QuantidadeRecusada(forma, quantidadeDeItens, quantidadeMinima);
        if (quantidade is not null)
        {
            yield return new("quantidadeMinima", quantidade);
        }
    }

    private static DomainError? QuantidadeRecusada(FormaDeComprovacao? forma, int quantidadeDeItens, int? quantidadeMinima) => forma switch
    {
        FormaDeComprovacao.Documento => RegraDeComprovacao.RecusaDaQuantidadeDeApresentacoes(quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao),
        FormaDeComprovacao.TodosOsItens => CombinacaoDoGrupo.RecusaDaQuantidade(FormaDeComprovacao.TodosOsItens, quantidadeMinima, quantidadeDeItens),
        FormaDeComprovacao.QuantidadeMinimaDeItens => CombinacaoDoGrupo.RecusaDaQuantidade(
            FormaDeComprovacao.QuantidadeMinimaDeItens, quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao, quantidadeDeItens),
        _ => null,
    };

    private static IEnumerable<FieldError> RecusarCamposIncompativeis(FormaDeComprovacao? forma, bool temDocumento, bool temItens)
    {
        if (forma is FormaDeComprovacao.TodosOsItens or FormaDeComprovacao.QuantidadeMinimaDeItens && temDocumento)
        {
            yield return new("documento", new DomainError("RegraDeComprovacao.DocumentoProibidoEmGrupo",
                "Este grupo reúne exigências e não pode informar um documento próprio. Retire o documento do grupo e inclua-o como item separado."));
        }
        if (forma == FormaDeComprovacao.Documento && temItens)
        {
            yield return new("itens", new DomainError("RegraDeComprovacao.ItensProibidosEmRegraDeDocumento",
                "A regra de documento não pode reunir outros itens. Retire os itens reunidos ou escolha um grupo."));
        }
    }
}
