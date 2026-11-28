namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
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
/// catálogo que falta, ou que não é do tipo do campo, deixa o bloco sem classificação: a fábrica não tem como criá-lo sem a regra de
/// cálculo e a de ordem de alocação. Os campos que não dependem da regra ausente continuam conferidos, e as recusas se somam às dela.
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
            [.. RecusadasDoBloco(causas), .. eliminacoes.Recusadas, .. criterios.Recusadas],
            [origemDasEliminacoes, criterios.Origem(LeituraDosCriteriosDeDesempate.Lista)]);
    }

    // O índice de uma eliminação que a conferência aponta é a posição dela no documento, as recusadas inclusive: já é a posição de origem.
    private static IEnumerable<ParteRecusada> RecusadasDoBloco(IReadOnlyList<FieldError> causas) =>
        PonteiroNoDocumento.Converter(causas.Select(static causa => causa.NoCaminhoDoItem(Bloco)), OrigemNoDocumento.Identidade)
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
        ReferenciaRegra? regraCalculo = calculo.IsSuccess ? calculo.Value : null;
        LeituraDoQuadroDePesos quadro = LeituraDoQuadroDePesos.Ler(
            regraCalculo, classificacao.BaseadoEmEnem ?? false, classificacao.ResolucaoPesoAreaEnem, cadastros);

        // Os campos são conferidos nas posições do documento, com ou sem a regra do catálogo: o que a regra ausente impede é só montar a fábrica.
        FieldError[] causas =
        [
            .. quadro.RecusasJuntoCom(ConfiguracaoClassificacao.ConferirCampos(CamposDoDocumento(classificacao, regraCalculo, eliminacoes, quadro))),
            .. foraDoCatalogo,
        ];

        return causas.Length > 0
            ? (null, causas)
            : Criar(classificacao, new(calculo.Value!, arredondamento?.Value, ordem.Value!), eliminacoes, quadro);
    }

    private static CamposDaClassificacao CamposDoDocumento(
        ClassificacaoNoDocumento classificacao,
        ReferenciaRegra? regraCalculo,
        ItensLidosDaClassificacao<RegraEliminacao> eliminacoes,
        LeituraDoQuadroDePesos quadro) => new()
        {
            RegraCalculo = regraCalculo,
            ArredondamentoDeclarado = classificacao.RegraArredondamento is not null,
            CasasArredondamento = classificacao.CasasArredondamento,
            Eliminacoes = EliminacoesNasPosicoesDoDocumento(classificacao, eliminacoes),
            BaseadoEmEnem = classificacao.BaseadoEmEnem,
            ResolucaoPesoAreaEnem = quadro.Resolucao,
            QuadroPesoAreaEnem = quadro.Grupos,
            EscalaNotaFinal = classificacao.EscalaNotaFinal,
        };

    // A quantidade conta todas as eliminações do documento, as recusadas inclusive: a conferência da classificação importada
    // não pode perder a incompatibilidade só porque a eliminação foi recusada antes de ter parâmetros.
    private static EliminacoesDeclaradas EliminacoesNasPosicoesDoDocumento(
        ClassificacaoNoDocumento classificacao, ItensLidosDaClassificacao<RegraEliminacao> eliminacoes)
    {
        Dictionary<int, ParametrosRegraEliminacao> adotadas = eliminacoes.Itens.ToDictionary(
            static item => item.PosicaoNoDocumento, static item => item.Item.Parametros);

        return EliminacoesDeclaradas.NasPosicoes(
            [.. classificacao.RegrasEliminacao.Select(regra => adotadas.GetValueOrDefault(regra.PosicaoNoDocumento))]);
    }

    private static (ConfiguracaoClassificacao? Classificacao, IReadOnlyList<FieldError> Causas) Criar(
        ClassificacaoNoDocumento classificacao,
        RegrasDoCatalogo regras,
        ItensLidosDaClassificacao<RegraEliminacao> eliminacoes,
        LeituraDoQuadroDePesos quadro)
    {
        Result<ConfiguracaoClassificacao> criada = ConfiguracaoClassificacao.Criar(Declaracao(classificacao, regras, eliminacoes, quadro));

        return criada.IsSuccess ? (criada.Value, []) : (null, criada.Errors);
    }

    private static DeclaracaoDaClassificacao Declaracao(
        ClassificacaoNoDocumento classificacao, RegrasDoCatalogo regras, ItensLidosDaClassificacao<RegraEliminacao> eliminacoes, LeituraDoQuadroDePesos quadro) => new()
        {
            RegraCalculo = regras.Calculo,
            RegraArredondamento = regras.Arredondamento,
            CasasArredondamento = classificacao.CasasArredondamento,
            RegraOrdemAlocacao = regras.OrdemDeAlocacao,
            RegrasEliminacao = [.. eliminacoes.Itens.Select(static regra => regra.Item)],
            BaseadoEmEnem = classificacao.BaseadoEmEnem,
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
