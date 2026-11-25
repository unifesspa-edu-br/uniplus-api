namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Validação e normalização dos campos editáveis de <see cref="Modalidade"/>: códigos dos enums, tamanhos e
/// coerência entre campos, acumulando toda violação em vez de parar na primeira. Sem I/O.
/// </summary>
public static class ValidacaoDosCamposDaModalidade
{
    private const int DescricaoMaxLength = 300;
    private const int BaseLegalMaxLength = 500;

    /// <summary>
    /// Valida só os campos independentes de coerência cruzada (parse dos enums,
    /// tamanhos), sem I/O — para o handler de atualização falhar rápido antes do
    /// fetch. <b>Não</b> inclui as três invariantes cruzadas nem a guarda do
    /// catálogo legal fixo: a guarda depende do <c>Codigo</c> já persistido, e as
    /// coerências têm de vir depois dela — reportá-las aqui, antes do fetch,
    /// preemptiria <c>EstruturaProtegidaNaoEditavel</c> com um erro de coerência
    /// para um payload que também é internamente incoerente. Só
    /// <see cref="Atualizar"/>, depois do fetch, avalia guarda e coerência na
    /// ordem certa.
    /// </summary>
    public static Result ValidarCamposDoPayload(DeclaracaoDaModalidade declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        CamposValidadosDaModalidade validados = ValidarCamposIndependentes(declaracao);

        return validados.Recusas.Count == 0 ? Result.Success() : Result.ValidationFailure(validados.Recusas);
    }

    /// <summary>
    /// Valida os campos editáveis por completo, acumulando toda violação
    /// independente — os campos que não cruzam entre si (<see cref="ValidarCamposIndependentes"/>)
    /// e as três invariantes cruzadas (RetiradaDeOutraModalidade⟺origem, natureza↔regra, parâmetros por
    /// regra), cada uma só avaliada quando os códigos de que ela depende já foram
    /// reconhecidos (senão o erro reportado seria derivado de um código inválido,
    /// não uma incoerência independente — ex.: reportar "SEGUE_ORDEM_DE_REMANEJAMENTO exigido"
    /// sobre uma <c>NaturezaLegal</c> que nem chegou a resolver para um valor de
    /// domínio). Sem I/O — existe também para o handler de atualização falhar
    /// rápido antes de buscar a modalidade por Id.
    /// </summary>
    public static Result<CamposResolvidosDaModalidade> ValidarCampos(DeclaracaoDaModalidade declaracao, CodigoModalidade? codigo = null)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        CamposValidadosDaModalidade validados = ValidarCamposIndependentes(declaracao);
        List<FieldError> erros = [.. validados.Recusas, .. CoerenciaDosCamposDaModalidade.ValidarCoerencia(validados, codigo)];

        return erros.Count == 0
            ? Result<CamposResolvidosDaModalidade>.Success(validados.Campos)
            : Result<CamposResolvidosDaModalidade>.ValidationFailure(erros);
    }

    /// <summary>
    /// Valida e normaliza os campos editáveis independentes de qualquer coerência cruzada, acumulando toda
    /// violação em vez de parar na primeira: códigos dos enums, normalização de texto e tamanhos. Devolve,
    /// junto das recusas, quais códigos das invariantes cruzadas foram reconhecidos; com a lista de recusas
    /// vazio, os três são sempre verdadeiros. Sem I/O, e separada da coerência cruzada porque
    /// <see cref="Modalidade.Atualizar"/> intercala a guarda do catálogo legal fixo entre as duas.
    /// </summary>
    internal static CamposValidadosDaModalidade ValidarCamposIndependentes(DeclaracaoDaModalidade declaracao)
    {
        string? descricao = NormalizarOpcional(declaracao.Descricao);
        string? baseLegal = NormalizarOpcional(declaracao.BaseLegal);
        string? origem = string.IsNullOrWhiteSpace(declaracao.ComposicaoOrigem) ? null : declaracao.ComposicaoOrigem;
        CodigosResolvidosDaModalidade codigos = CodigosResolvidosDaModalidade.De(declaracao);
        FieldError?[] recusas =
        [
            RecusaDeTamanho(descricao, DescricaoMaxLength, "descricao", ModalidadeErrorCodes.DescricaoTamanho, "Descrição da modalidade"),
            RecusaDeTamanho(baseLegal, BaseLegalMaxLength, "baseLegal", ModalidadeErrorCodes.BaseLegalTamanho, "Base legal da modalidade"),
            .. codigos.Recusas,
            .. RecusasDasReferencias(declaracao),
            RecusaDaRegraDeAplicabilidade(declaracao.RegraDeAplicabilidade),
        ];
        return new([.. recusas.OfType<FieldError>()], codigos.Reconhecidos, CamposResolvidos(declaracao, codigos, descricao, origem, baseLegal));
    }

    private static CamposResolvidosDaModalidade CamposResolvidos(
        DeclaracaoDaModalidade declaracao, CodigosResolvidosDaModalidade codigos, string? descricao, string? origem, string? baseLegal) =>
        new(
            descricao,
            codigos.Natureza,
            codigos.Composicao,
            origem,
            codigos.Regra,
            RemanejamentoParametros.Criar(declaracao.RemanejamentoDestino, declaracao.RemanejamentoPar, declaracao.RemanejamentoFallback),
            declaracao.RegraDeAplicabilidade,
            codigos.Acao,
            baseLegal);

    private static FieldError? RecusaDeTamanho(string? valor, int maximo, string campo, string codigoDaRecusa, string rotulo) =>
        valor is { } texto && texto.Length > maximo
            ? new FieldError(campo, new DomainError(codigoDaRecusa, $"{rotulo} deve ter no máximo {maximo} caracteres."))
            : null;

    private static FieldError? RecusaDeCodigo(string? valor, string campo) =>
        string.IsNullOrWhiteSpace(valor) || CodigoModalidade.EhValido(valor)
            ? null
            : new FieldError(campo, CodigoModalidade.Criar(valor).Error!);

    private static FieldError?[] RecusasDasReferencias(DeclaracaoDaModalidade declaracao) =>
    [
        RecusaDeCodigo(declaracao.ComposicaoOrigem, "composicaoOrigem"),
        RecusaDeCodigo(declaracao.RemanejamentoDestino, "remanejamento.destino"),
        RecusaDeCodigo(declaracao.RemanejamentoPar, "remanejamento.par"),
        RecusaDeCodigo(declaracao.RemanejamentoFallback, "remanejamento.fallback"),
    ];

    private static FieldError? RecusaDaRegraDeAplicabilidade(PredicadoDnf? regra) =>
        regra is null
            ? new FieldError("regraDeAplicabilidade", new DomainError(
                ModalidadeErrorCodes.RegraDeAplicabilidadeObrigatoria,
                "A modalidade precisa da regra que diz quem concorre a ela; a ampla concorrência a declara sem condição."))
            : null;

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
