namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas que a fase impõe à configuração: a etapa que não declara a fase em que acontece, a troca da unidade
/// responsável por uma fase que já começou e a remoção dela do cronograma.
/// </summary>
internal static class ErrosDaFaseDoProcesso
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new("ProcessoSeletivo.EtapaSemFaseDeclarada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_sem_fase_declarada", "A etapa não declara a fase em que acontece")),
        new("FaseCronograma.UnidadeDeFaseIniciadaNaoMuda", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.unidade_de_fase_iniciada_nao_muda", "A unidade responsável por uma fase iniciada não muda")),
        new("FaseCronograma.FaseIniciadaNaoSaiDoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.fase_iniciada_nao_sai_do_cronograma", "Uma fase iniciada não sai do cronograma")),
    ];
}
