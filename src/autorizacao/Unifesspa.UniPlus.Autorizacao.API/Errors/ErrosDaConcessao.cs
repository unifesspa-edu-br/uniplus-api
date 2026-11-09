namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: concessão de acesso.</summary>
internal static class ErrosDaConcessao
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Consulta(), .. Concessao()];

    private static KeyValuePair<string, DomainErrorMapping>[] Consulta() =>
    [
        new(ConcessaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.concessao.nao_encontrada", "Concessão de acesso não encontrada")),
        new(ConcessaoErrorCodes.JaRevogada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.ja_revogada", "Concessão de acesso já revogada")),
        new(ConcessaoErrorCodes.MinimoDeGestores, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.minimo_de_gestores", "A revogação deixaria menos de dois gestores de acessos")),
        new(ConcessaoErrorCodes.ConfirmacaoDivergente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.confirmacao_divergente", "O e-mail informado não é o do beneficiário da concessão")),
        new(ConcessaoErrorCodes.FiltroDePessoaInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.concessao.filtro_de_pessoa_invalido", "Filtro de pessoa das concessões inválido")),
        new(ConcessaoErrorCodes.FiltroDeSituacaoInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.concessao.filtro_de_situacao_invalido", "Situação do filtro de concessões inválida")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Concessao() =>
    [
        new(ConcessaoErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.motivo_invalido", "Motivo da revogação inválido")),
        new(ConcessaoErrorCodes.PerfilInativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.perfil_inativo", "Perfil de acesso desativado não aceita concessão")),
        new(ConcessaoErrorCodes.PerfilSemVersaoEmVigor, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.perfil_sem_versao_em_vigor", "O perfil de acesso não tem versão em vigor")),
        new(ConcessaoErrorCodes.Duplicada, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.concessao.duplicada", "Já existe concessão igual não revogada, vigente ou vencida; revogue-a antes de conceder de novo")),
        new(ConcessaoErrorCodes.ValidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.validade_invalida", "Fim da vigência inválido")),
        new(ConcessaoErrorCodes.EscopoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.escopo_invalido", "Escopo incoerente ou não admitido pelo perfil")),
        new(ConcessaoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.concessao.autor_nao_identificado", "Autor da operação não identificado")),
        MapeamentosComuns.Concorrencia(ConcessaoErrorCodes.ConflitoDeConcorrencia),
    ];
}
