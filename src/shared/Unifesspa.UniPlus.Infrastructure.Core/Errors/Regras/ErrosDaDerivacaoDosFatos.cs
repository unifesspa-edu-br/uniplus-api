namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

/// <summary>Códigos das regras de derivação dos fatos do candidato.</summary>
internal static class ErrosDaDerivacaoDosFatos
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new("RegraDerivacao.ContribuiObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regra_derivacao.contribui_obrigatorio", "Uma regra de derivação precisa contribuir um código")),
        new("RegrasDerivacaoFato.SemRegras", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.sem_regras", "A derivação de um fato precisa de ao menos uma regra")),
        new("RegrasDerivacaoFato.ContribuiForaDoDominio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.contribui_fora_do_dominio", "Uma regra contribui um código fora do domínio do fato")),
        new("RegrasDerivacaoFato.DependenciasIncoerentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.dependencias_incoerentes", "As dependências declaradas não são exatamente os fatos citados")),
        new("RegrasDerivacaoFato.DerivacaoAutorreferente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.derivacao_autorreferente", "Um fato derivado não pode depender de si mesmo")),
    ];
}
