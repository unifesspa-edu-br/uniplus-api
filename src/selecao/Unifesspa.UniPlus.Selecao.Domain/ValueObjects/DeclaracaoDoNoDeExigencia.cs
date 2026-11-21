namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>Dados entendidos e recusas da declaração de um item de exigência.</summary>
public sealed record DeclaracaoDoNoDeExigencia(
    TipoNo Tipo, bool TemDocumento, int QuantidadeDeFilhos, int? QuantidadeMinima,
    string? Consequencia, DistincaoDeclarada? Distincao, IReadOnlyList<FieldError> Recusas)
{
    public static DeclaracaoDoNoDeExigencia Interpretar(string? tipo, bool temDocumento, int quantidadeDeFilhos, int? quantidadeMinima,
        string? consequencia = null, bool temBaseLegal = false, DistincaoDeclarada? distincao = null)
    {
        TipoNo entendido = InterpretarTipo(tipo);
        List<FieldError> recusas = RecusarDeclaracao(entendido, temDocumento, quantidadeDeFilhos, quantidadeMinima);
        recusas.AddRange(RecusarConsequenciaEBase(entendido, consequencia, temBaseLegal));
        if (entendido == TipoNo.Folha && distincao is not null)
        {
            recusas.AddRange(ConferenciaDaDistincao.Conferir(distincao, quantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao));
        }
        int? quantidade = entendido is TipoNo.Folha or TipoNo.GrupoOu
            ? quantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao : quantidadeMinima;
        return new(entendido, temDocumento, quantidadeDeFilhos, quantidade, CombinacaoDoGrupo.NormalizarConsequencia(consequencia), distincao, recusas);
    }

    private static TipoNo InterpretarTipo(string? tipo) => tipo switch
    {
        "FOLHA" => TipoNo.Folha,
        "E" => TipoNo.GrupoE,
        "OU" => TipoNo.GrupoOu,
        _ => TipoNo.Nenhum,
    };

    private static List<FieldError> RecusarDeclaracao(TipoNo tipo, bool temDocumento, int quantidadeDeFilhos, int? quantidadeMinima)
    {
        List<FieldError> recusas = [];
        if (tipo == TipoNo.Nenhum)
        {
            recusas.Add(new("tipo", new DomainError("NoExigencia.TipoInvalido",
                "O tipo informado não é reconhecido. Escolha \"FOLHA\" para um documento, \"E\" para exigir todos os itens ou \"OU\" para exigir uma quantidade deles.")));
        }
        if (tipo == TipoNo.Folha && !temDocumento)
        {
            recusas.Add(new("documento", new DomainError("NoExigencia.DocumentoObrigatorioEmFolha",
                "Este item é um documento (\"FOLHA\"), mas não informa qual documento é exigido. Escolha o documento no catálogo.")));
        }
        recusas.AddRange(RecusarCamposIncompativeis(tipo, temDocumento, quantidadeDeFilhos > 0));
        recusas.AddRange(RecusarQuantidade(tipo, quantidadeDeFilhos, quantidadeMinima));
        return recusas;
    }

    private static IEnumerable<FieldError> RecusarConsequenciaEBase(TipoNo tipo, string? consequencia, bool temBaseLegal)
    {
        if (CombinacaoDoGrupo.RecusaDaConsequencia(tipo, consequencia) is { } consequenciaRecusada)
        {
            yield return new("consequencia", consequenciaRecusada);
        }
        if (CombinacaoDoGrupo.RecusaDaBaseLegal(tipo, consequencia, temBaseLegal) is { } baseRecusada)
        {
            yield return new("basesLegais", baseRecusada);
        }
    }

    private static IEnumerable<FieldError> RecusarQuantidade(TipoNo tipo, int quantidadeDeFilhos, int? quantidadeMinima)
    {
        if (tipo is TipoNo.GrupoE or TipoNo.GrupoOu && ComposicaoDoGrupo.RecusaSeVazio(quantidadeDeFilhos) is { } vazio)
        {
            yield return new("filhos", vazio);
        }
        DomainError? quantidade = QuantidadeRecusada(tipo, quantidadeDeFilhos, quantidadeMinima);
        if (quantidade is not null)
        {
            yield return new("quantidadeMinima", quantidade);
        }
    }

    private static DomainError? QuantidadeRecusada(TipoNo tipo, int quantidadeDeFilhos, int? quantidadeMinima) => tipo switch
    {
        TipoNo.Folha => NoExigencia.RecusaDaQuantidadeDaFolha(quantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao),
        TipoNo.GrupoE => CombinacaoDoGrupo.RecusaDaQuantidade(tipo, quantidadeMinima, quantidadeDeFilhos),
        TipoNo.GrupoOu when quantidadeDeFilhos > 0 => CombinacaoDoGrupo.RecusaDaQuantidade(
            tipo, quantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao, quantidadeDeFilhos),
        _ => null,
    };

    private static IEnumerable<FieldError> RecusarCamposIncompativeis(TipoNo tipo, bool temDocumento, bool temFilhos)
    {
        if (tipo is TipoNo.GrupoE or TipoNo.GrupoOu && temDocumento)
        {
            yield return new("documento", new DomainError("NoExigencia.DocumentoProibidoEmGrupo",
                "Este grupo reúne exigências e não pode informar um documento próprio. Retire o documento do grupo e inclua-o como item separado."));
        }
        if (tipo == TipoNo.Folha && temFilhos)
        {
            yield return new("filhos", new DomainError("NoExigencia.FilhosProibidosEmFolha",
                "Este item é um documento (\"FOLHA\") e não pode reunir outros itens. Retire os itens reunidos ou escolha um grupo."));
        }
    }
}
