namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A classificação e os critérios de desempate que a leitura do documento adotou, as partes que ela recusou e a origem de cada lista,
/// com a posição em que cada item adotado veio. A classificação nula com recusa é o bloco que a leitura recusou: o bloco ausente do
/// documento não tem recusa, e quem o confere é a regra de conformidade.
/// </summary>
internal sealed record ClassificacaoLida(
    ConfiguracaoClassificacao? Classificacao,
    IReadOnlyList<CriterioDesempate> Criterios,
    IReadOnlyList<ParteRecusada> Recusadas,
    IReadOnlyList<OrigemNoDocumento> Origens)
{
    internal static ClassificacaoLida Ausente { get; } = new(null, [], [], []);
}

/// <summary>
/// Lê a classificação do documento pelas regras de domínio da gravação: as regras vêm do catálogo, as eliminações e os critérios são
/// lidos um a um, o quadro de pesos por área vem dos cadastros e a fábrica acumula o que o bloco consegue provar sozinho. A regra do
/// catálogo que falta, ou que não é do tipo do campo, recusa o bloco inteiro, que fica sem classificação: a fábrica não tem como
/// criá-lo sem a regra de cálculo e a de ordem de alocação.
/// </summary>
internal static class LeituraDaClassificacao
{
    private const string Bloco = "classificacao";

    internal static ClassificacaoLida Ler(ClassificacaoNoDocumento? classificacao, EtapasLidas etapas, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(cadastros);

        return classificacao is null ? ClassificacaoLida.Ausente : LerBloco(classificacao, etapas, cadastros);
    }

    private static ClassificacaoLida LerBloco(ClassificacaoNoDocumento classificacao, EtapasLidas etapas, CadastrosCitadosNoDocumento cadastros)
    {
        ItensLidosDaClassificacao<RegraEliminacao> eliminacoes = LeituraDasRegrasDeEliminacao.Ler(classificacao.RegrasEliminacao, etapas, cadastros);
        ItensLidosDaClassificacao<CriterioDesempate> criterios = LeituraDosCriteriosDeDesempate.Ler(classificacao.CriteriosDeDesempate, etapas, cadastros);
        OrigemNoDocumento origemDasEliminacoes = eliminacoes.Origem(LeituraDasRegrasDeEliminacao.Lista);
        (ConfiguracaoClassificacao? lida, IReadOnlyList<FieldError> causas) = Montar(classificacao, eliminacoes, cadastros);

        return new(
            lida,
            [.. criterios.Itens.Select(static criterio => criterio.Item)],
            [.. RecusadasDoBloco(causas, origemDasEliminacoes), .. eliminacoes.Recusadas, .. criterios.Recusadas],
            [origemDasEliminacoes, criterios.Origem(LeituraDosCriteriosDeDesempate.Lista)]);
    }

    // O índice de uma eliminação que a fábrica aponta é o da declaração, e o ponteiro leva à posição em que a regra veio no documento.
    private static IEnumerable<ParteRecusada> RecusadasDoBloco(IReadOnlyList<FieldError> causas, OrigemNoDocumento origemDasEliminacoes) =>
        PonteiroNoDocumento.Converter(causas.Select(static causa => causa.NoCaminhoDoItem(Bloco)), origemDasEliminacoes)
            .Select(static causa => ParteRecusada.DoBloco(Bloco, causa));

    private static (ConfiguracaoClassificacao? Classificacao, IReadOnlyList<FieldError> Causas) Montar(
        ClassificacaoNoDocumento classificacao, ItensLidosDaClassificacao<RegraEliminacao> eliminacoes, CadastrosCitadosNoDocumento cadastros)
    {
        Result<ReferenciaRegra> calculo = Regra(classificacao.RegraCalculo, TipoRegra.RegraCalculo, "regraCalculo", cadastros);
        Result<ReferenciaRegra>? arredondamento = classificacao.RegraArredondamento is null
            ? null
            : Regra(classificacao.RegraArredondamento, TipoRegra.RegraArredondamento, "regraArredondamento", cadastros);
        Result<ReferenciaRegra> ordem = Regra(classificacao.RegraOrdemAlocacao, TipoRegra.RegraOrdemAlocacao, "regraOrdemAlocacao", cadastros);

        FieldError[] foraDoCatalogo = [.. Falhas(calculo), .. arredondamento is null ? [] : Falhas(arredondamento), .. Falhas(ordem)];

        return foraDoCatalogo.Length > 0
            ? (null, foraDoCatalogo)
            : Criar(classificacao, new(calculo.Value!, arredondamento?.Value, ordem.Value!), eliminacoes, cadastros);
    }

    private static (ConfiguracaoClassificacao? Classificacao, IReadOnlyList<FieldError> Causas) Criar(
        ClassificacaoNoDocumento classificacao,
        RegrasDoCatalogo regras,
        ItensLidosDaClassificacao<RegraEliminacao> eliminacoes,
        CadastrosCitadosNoDocumento cadastros)
    {
        LeituraDoQuadroDePesos quadro = LeituraDoQuadroDePesos.Ler(
            regras.Calculo, classificacao.BaseadoEmEnem ?? false, classificacao.ResolucaoPesoAreaEnem, cadastros);
        Result<ConfiguracaoClassificacao> criada = ConfiguracaoClassificacao.Criar(Declaracao(classificacao, regras, eliminacoes, quadro));
        IReadOnlyList<FieldError> causas = quadro.RecusasJuntoCom(criada.IsSuccess ? [] : criada.Errors);

        return causas.Count == 0 ? (criada.Value, []) : (null, causas);
    }

    private static DeclaracaoDaClassificacao Declaracao(
        ClassificacaoNoDocumento classificacao, RegrasDoCatalogo regras, ItensLidosDaClassificacao<RegraEliminacao> eliminacoes, LeituraDoQuadroDePesos quadro) => new()
        {
            RegraCalculo = regras.Calculo,
            RegraArredondamento = regras.Arredondamento,
            CasasArredondamento = classificacao.CasasArredondamento,
            RegraOrdemAlocacao = regras.OrdemDeAlocacao,
            RegrasEliminacao = [.. eliminacoes.Itens.Select(static regra => regra.Item)],
            BaseadoEmEnem = classificacao.BaseadoEmEnem ?? false,
            ResolucaoPesoAreaEnem = quadro.Resolucao,
            QuadroPesoAreaEnem = quadro.Grupos,
            EscalaNotaFinal = classificacao.EscalaNotaFinal,
        };

    private static Result<ReferenciaRegra> Regra(
        ReferenciaDeRegraNoDocumento? citada, TipoRegra esperado, string campo, CadastrosCitadosNoDocumento cadastros)
    {
        RegraDoCatalogoCitada? regra = cadastros.Regra(citada?.Codigo, citada?.Versao);

        return regra switch
        {
            null => Falha(campo, "ConfiguracaoClassificacao.RegraNaoEncontrada", "Regra não encontrada no catálogo de regras."),
            { Tipo: var tipo } when tipo != esperado => Falha(campo, "ConfiguracaoClassificacao.RegraTipoInvalido", "A regra não é do tipo esperado."),
            _ => Result<ReferenciaRegra>.Success(regra.Referencia),
        };
    }

    private static Result<ReferenciaRegra> Falha(string campo, string codigo, string mensagem) =>
        Result<ReferenciaRegra>.ValidationFailure([new(campo, new DomainError(codigo, mensagem))]);

    private static IReadOnlyList<FieldError> Falhas(Result<ReferenciaRegra> regra) => regra.IsFailure ? regra.Errors : [];

    private sealed record RegrasDoCatalogo(ReferenciaRegra Calculo, ReferenciaRegra? Arredondamento, ReferenciaRegra OrdemDeAlocacao);
}
