namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

internal static class ErrosDaImportacaoDoResultado
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new(ImportacaoDoResultadoErrorCodes.PorDeclarar, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.importacao_resultado.por_declarar", "A importação do resultado não está declarada")),
        new(ImportacaoDoResultadoErrorCodes.SemRelatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.importacao_resultado.sem_relatorio", "A importação não declara relatório")),
        new(ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.importacao_resultado.em_inscricao_pelo_sistema", "Importação declarada em processo com inscrição pelo sistema")),
        new(ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.importacao_resultado.relatorio_sem_leiaute", "Relatório sem leiaute")),
        new(ImportacaoDoResultadoErrorCodes.RelatorioRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.importacao_resultado.relatorio_repetido", "Relatório repetido")),
        new(ImportacaoDoResultadoErrorCodes.PrimeiraFasePorDeclarar, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.importacao_resultado.primeira_fase_por_declarar", "Primeira fase depois da importação não declarada")),
        new(ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.importacao_resultado.primeira_fase_fora_do_cronograma", "Primeira fase depois da importação fora do cronograma")),
    ];
}
