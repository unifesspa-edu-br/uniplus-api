namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A coerência entre a fórmula da nota (cálculo local × classificação importada) e o que a
/// acompanha: a precisão do arredondamento, as eliminações e a escala da nota final.
/// </summary>
internal static class ConferenciasDoCalculoDaNota
{
    private const string ErroEscalaNotaFinalInvalida = "ConfiguracaoClassificacao.EscalaNotaFinalInvalida";
    private const string ErroEscalaNotaFinalIndevida = "ConfiguracaoClassificacao.EscalaNotaFinalIndevida";

    internal static bool EhImportada(ReferenciaRegra regraCalculo) =>
        regraCalculo.Codigo == RegraDeCalculoDaNota.ClassificacaoImportada.Codigo();

    // Sem a fórmula confirmada no catálogo não se sabe se o cálculo é local ou importado: só o valor da
    // escala, que independe disso, é conferido.
    internal static List<FieldError> Conferir(CamposDaClassificacao campos) =>
    [
        .. ConferirSegundoOTipoDoCalculo(campos),
        .. ConferirEscala(campos),
    ];

    private static List<FieldError> ConferirSegundoOTipoDoCalculo(CamposDaClassificacao campos) => campos.RegraCalculo switch
    {
        null => [],
        { } regraCalculo when EhImportada(regraCalculo) => ConferirImportada(campos),
        _ => ConferirLocal(campos),
    };

    // Classificação importada (federal) não exige precisão local. As duas metades são checadas
    // e atribuídas ao field que cada uma carrega: informar só casasArredondamento não pode
    // apontar o field de regra. A eliminação por corte de nota pressupõe um cálculo local que a
    // importação substitui — o resultado já vem pronto de fora.
    private static List<FieldError> ConferirImportada(CamposDaClassificacao campos) =>
    [
        .. RecusaSe(campos.ArredondamentoDeclarado, "regraArredondamentoCodigo",
            "ConfiguracaoClassificacao.ArredondamentoIndevido",
            "Arredondamento local não se aplica quando a classificação é importada."),
        .. RecusaSe(campos.CasasArredondamento is not null, "casasArredondamento",
            "ConfiguracaoClassificacao.CasasArredondamentoIndevido",
            "Casas de arredondamento não se aplicam quando a classificação é importada."),
        .. RecusaSe(campos.Eliminacoes is { Quantidade: > 0 }, "regrasEliminacao",
            "ConfiguracaoClassificacao.EliminacaoIndevida",
            "Regras de eliminação não se aplicam quando a classificação é importada."),
    ];

    // Cálculo local exige uma regra de precisão declarada (default: truncar). As duas metades
    // são checadas e atribuídas ao field que cada uma corrige: um regraArredondamentoCodigo
    // válido acompanhado de casasArredondamento inválido não pode apontar o campo errado. A regra
    // informada e não encontrada no catálogo conta como declarada: a recusa dela é outra.
    private static List<FieldError> ConferirLocal(CamposDaClassificacao campos) =>
    [
        .. RecusaSe(!campos.ArredondamentoDeclarado, "regraArredondamentoCodigo",
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "Cálculo local exige regra de arredondamento."),
        .. RecusaSe(campos.CasasArredondamento is not > 0, "casasArredondamento",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
            "Cálculo local exige casas decimais de arredondamento maior que zero."),
    ];

    private static IEnumerable<FieldError> RecusaSe(bool condicao, string campo, string codigo, string mensagem) =>
        condicao ? [new(campo, new DomainError(codigo, mensagem))] : [];

    // O valor que não é positivo não é falta, e na importada não há média a escalar. O que não cabe na NotaFinal
    // (numeric(9,4)) o banco recusaria ou arredondaria em silêncio, e o positivo 0,00001 viraria zero.
    // A falta da escala é tolerada (o gate de publicação a cobra).
    private static List<FieldError> ConferirEscala(CamposDaClassificacao campos) =>
        RecusaDoValorDaEscala(campos.EscalaNotaFinal) is { } invalida ? [invalida]
        : campos is { EscalaNotaFinal: not null, RegraCalculo: { } regraCalculo } && EhImportada(regraCalculo)
            ? [Recusa(ErroEscalaNotaFinalIndevida, "A escala da nota final não se aplica quando a classificação é importada.")]
            : [];

    private static FieldError? RecusaDoValorDaEscala(decimal? escalaNotaFinal) => escalaNotaFinal switch
    {
        null => null,
        <= 0 => Recusa(ErroEscalaNotaFinalInvalida, "A escala da nota final, quando informada, deve ser maior que zero."),
        { } escala when !CabeNaNotaFinal(escala) => Recusa(ErroEscalaNotaFinalInvalida, MensagemDeEscalaQueNaoCabe()),
        _ => null,
    };

    private static FieldError Recusa(string codigo, string mensagem) =>
        new(ConfiguracaoClassificacao.CampoEscalaNotaFinal, new DomainError(codigo, mensagem));

    private static string MensagemDeEscalaQueNaoCabe() =>
        $"A escala da nota final deve ter no máximo {ConfiguracaoClassificacao.EscalaNotaFinalPrecisao - ConfiguracaoClassificacao.EscalaNotaFinalEscala} dígitos inteiros e {ConfiguracaoClassificacao.EscalaNotaFinalEscala} casas decimais.";

    private static bool CabeNaNotaFinal(decimal escala) =>
        Math.Round(escala, ConfiguracaoClassificacao.EscalaNotaFinalEscala) == escala
        && escala < (decimal)Math.Pow(10, ConfiguracaoClassificacao.EscalaNotaFinalPrecisao - ConfiguracaoClassificacao.EscalaNotaFinalEscala);
}
