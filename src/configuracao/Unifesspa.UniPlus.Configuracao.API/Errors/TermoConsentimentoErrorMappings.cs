namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Termo de consentimento (termos imunes após a publicação, UNI-REQ-0086).</summary>
internal static class TermoConsentimentoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(TermoConsentimentoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.nome_obrigatorio",
                "Nome do termo de consentimento é obrigatório")),

        new(TermoConsentimentoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.nome_tamanho",
                "Tamanho do nome do termo de consentimento inválido")),

        new(TermoConsentimentoErrorCodes.FormaAceiteInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.forma_aceite_invalida",
                "Forma de aceite do termo fora do domínio fechado")),

        new(TermoConsentimentoErrorCodes.TextoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.texto_tamanho",
                "Tamanho do texto do rascunho inválido")),

        new(TermoConsentimentoErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.base_legal_tamanho",
                "Tamanho da base legal do rascunho inválido")),

        new(TermoConsentimentoErrorCodes.RevisaoSemTexto,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.revisao_sem_texto",
                "Rascunho sem texto não pode ser marcado como revisado nem promovido")),

        new(TermoConsentimentoErrorCodes.RevisaoSemBaseLegal,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.revisao_sem_base_legal",
                "Rascunho sem base legal não pode ser marcado como revisado nem promovido")),

        new(TermoConsentimentoErrorCodes.PromocaoSemRevisao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.termo_consentimento.promocao_sem_revisao",
                "Só um rascunho marcado como revisado pode ser promovido a versão")),

        new(TermoConsentimentoErrorCodes.RemocaoBloqueadaComVersaoPromovida,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.termo_consentimento.remocao_bloqueada_com_versao_promovida",
                "Termo com ao menos uma versão promovida não pode ser removido")),

        new(TermoConsentimentoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.termo_consentimento.nao_encontrado",
                "Termo de consentimento não encontrado")),

        new(TermoConsentimentoErrorCodes.ConflitoDeConcorrencia,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.termo_consentimento.conflito_de_concorrencia",
                // NÃO retentável, apesar de ser corrida: este código é compartilhado pelas
                // operações do termo, e uma delas é a marcação de revisado. Editar o rascunho
                // LIMPA a revisão anterior, porque a aprovação vale para o texto exato que foi
                // lido. Liberar a reserva deixaria um retry automático da mesma requisição — que
                // não tem corpo — aprovar o texto NOVO, sem ninguém o ter revisado.
                "O rascunho foi modificado concorrentemente")),
    ];
}
