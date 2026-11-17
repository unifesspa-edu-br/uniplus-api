namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

/// <summary>Códigos das regras de derivação dos fatos do candidato.</summary>
internal static class ErrosDaDerivacaoDosFatos
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new("RegraDerivacao.ContribuiObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regra_derivacao.contribui_obrigatorio", "Uma regra de derivação precisa contribuir um código")),
        new("RegrasDerivacaoFato.ContribuiForaDoDominio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.contribui_fora_do_dominio", "Uma regra contribui um código fora do domínio do fato")),
        new("RegrasDerivacaoFato.DerivacaoAutorreferente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.derivacao_autorreferente", "Um fato derivado não pode depender de si mesmo")),
    ];
}
