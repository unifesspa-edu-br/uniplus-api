namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da regra de homologação das inscrições que a fase declara: o resultado que a regra aponta e a forma das
/// condições. Os códigos são literais porque o registro de erros só enxerga literal.
/// </summary>
internal static class ErrosDaHomologacaoDasInscricoes
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new("HomologacaoDasInscricoes.ResultadoNaoDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.homologacao_das_inscricoes.resultado_nao_declarado", "A regra de homologação das inscrições não aponta um resultado da fase")),
        new("HomologacaoDasInscricoes.CondicaoNaoDisponivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.homologacao_das_inscricoes.condicao_nao_disponivel", "A condição de homologação ainda não é apurada pelo sistema")),
        new("HomologacaoDasInscricoes.EdicaoDoExameInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.homologacao_das_inscricoes.edicao_do_exame_invalida", "A edição do exame da condição é inválida (de 1998 a 2100)")),
        new("HomologacaoDasInscricoes.AlternativaSemCondicao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.homologacao_das_inscricoes.alternativa_sem_condicao", "A alternativa da regra de homologação não tem condição")),
    ];
}
