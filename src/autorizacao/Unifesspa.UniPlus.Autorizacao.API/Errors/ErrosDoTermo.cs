namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: termo de responsabilidade.</summary>
internal static class ErrosDoTermo
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Consulta(), .. Publicacao()];

    private static KeyValuePair<string, DomainErrorMapping>[] Consulta() =>
    [
        new(TermoErrorCodes.SemVersaoVigente, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.termo.sem_versao_vigente", "Não há termo de responsabilidade publicado")),
        new(TermoErrorCodes.VersaoNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.termo.versao_nao_encontrada", "Versão do termo não encontrada")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Publicacao() =>
    [
        new(TermoErrorCodes.TextoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.termo.texto_invalido", "Texto do termo inválido")),
        new(TermoErrorCodes.AtoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.termo.ato_invalido", "Ato de aprovação inválido")),
        new(TermoErrorCodes.TextoIgualAoVigente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.termo.texto_igual_ao_vigente", "O texto é igual ao da versão vigente")),
        new(TermoErrorCodes.VigenteAlterado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.termo.vigente_alterado", "Outra versão foi publicada antes")),
        new(TermoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.termo.autor_nao_identificado", "Autor da operação não identificado")),
        MapeamentosComuns.Concorrencia(TermoErrorCodes.ConflitoDeConcorrencia),
    ];
}
