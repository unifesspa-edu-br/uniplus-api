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

    internal static List<FieldError> Conferir(
        ReferenciaRegra regraCalculo,
        ReferenciaRegra? regraArredondamento,
        int? casasArredondamento,
        int quantidadeDeEliminacoes,
        decimal? escalaNotaFinal)
    {
        bool ehImportada = EhImportada(regraCalculo);
        List<FieldError> erros = ehImportada
            ? ConferirImportada(regraArredondamento, casasArredondamento, quantidadeDeEliminacoes)
            : ConferirLocal(regraArredondamento, casasArredondamento);
        erros.AddRange(ConferirEscala(escalaNotaFinal, ehImportada));
        return erros;
    }

    // Classificação importada (federal) não exige precisão local. As duas metades são checadas
    // e atribuídas ao field que cada uma carrega: informar só casasArredondamento não pode
    // apontar o field de regra. A eliminação por corte de nota pressupõe um cálculo local que a
    // importação substitui — o resultado já vem pronto de fora.
    private static List<FieldError> ConferirImportada(
        ReferenciaRegra? regraArredondamento,
        int? casasArredondamento,
        int quantidadeDeEliminacoes) =>
    [
        .. RecusaSe(regraArredondamento is not null, "regraArredondamentoCodigo",
            "ConfiguracaoClassificacao.ArredondamentoIndevido",
            "Arredondamento local não se aplica quando a classificação é importada."),
        .. RecusaSe(casasArredondamento is not null, "casasArredondamento",
            "ConfiguracaoClassificacao.CasasArredondamentoIndevido",
            "Casas de arredondamento não se aplicam quando a classificação é importada."),
        .. RecusaSe(quantidadeDeEliminacoes > 0, "regrasEliminacao",
            "ConfiguracaoClassificacao.EliminacaoIndevida",
            "Regras de eliminação não se aplicam quando a classificação é importada."),
    ];

    // Cálculo local exige uma regra de precisão declarada (default: truncar). As duas metades
    // são checadas e atribuídas ao field que cada uma corrige: um regraArredondamentoCodigo
    // válido acompanhado de casasArredondamento inválido não pode apontar o campo errado.
    private static List<FieldError> ConferirLocal(ReferenciaRegra? regraArredondamento, int? casasArredondamento) =>
    [
        .. RecusaSe(regraArredondamento is null, "regraArredondamentoCodigo",
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "Cálculo local exige regra de arredondamento."),
        .. RecusaSe(casasArredondamento is not > 0, "casasArredondamento",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
            "Cálculo local exige casas decimais de arredondamento maior que zero."),
    ];

    private static IEnumerable<FieldError> RecusaSe(bool condicao, string campo, string codigo, string mensagem) =>
        condicao ? [new(campo, new DomainError(codigo, mensagem))] : [];

    // A falta da escala é tolerada (o gate de publicação a cobra), mas o valor que não é
    // positivo não é falta, e na importada não há média a escalar. O que não cabe na NotaFinal
    // (numeric(9,4)) o banco recusaria ou arredondaria em silêncio, e o positivo 0,00001 viraria zero.
    private static List<FieldError> ConferirEscala(decimal? escalaNotaFinal, bool ehImportada) =>
        escalaNotaFinal switch
        {
            null => [],
            <= 0 => [Recusa(ErroEscalaNotaFinalInvalida, "A escala da nota final, quando informada, deve ser maior que zero.")],
            { } escala when !CabeNaNotaFinal(escala) => [Recusa(ErroEscalaNotaFinalInvalida, MensagemDeEscalaQueNaoCabe())],
            _ when ehImportada => [Recusa(ErroEscalaNotaFinalIndevida, "A escala da nota final não se aplica quando a classificação é importada.")],
            _ => [],
        };

    private static FieldError Recusa(string codigo, string mensagem) =>
        new(ConfiguracaoClassificacao.CampoEscalaNotaFinal, new DomainError(codigo, mensagem));

    private static string MensagemDeEscalaQueNaoCabe() =>
        $"A escala da nota final deve ter no máximo {ConfiguracaoClassificacao.EscalaNotaFinalPrecisao - ConfiguracaoClassificacao.EscalaNotaFinalEscala} dígitos inteiros e {ConfiguracaoClassificacao.EscalaNotaFinalEscala} casas decimais.";

    private static bool CabeNaNotaFinal(decimal escala) =>
        Math.Round(escala, ConfiguracaoClassificacao.EscalaNotaFinalEscala) == escala
        && escala < (decimal)Math.Pow(10, ConfiguracaoClassificacao.EscalaNotaFinalPrecisao - ConfiguracaoClassificacao.EscalaNotaFinalEscala);
}
