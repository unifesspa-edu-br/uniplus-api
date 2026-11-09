namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: instalação dos gestores.</summary>
internal static class ErrosDaInstalacao
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Gestores(), .. Confirmacao()];

    private static KeyValuePair<string, DomainErrorMapping>[] Gestores() =>
    [
        new(InstalacaoErrorCodes.JaHaGestor, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.ja_ha_gestor", "Já existe gestor de acessos ativo", RecusaAposGravacao: true)),
        new(InstalacaoErrorCodes.JaHaGestorAoIniciar, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.ja_ha_gestor", "Já existe gestor de acessos ativo")),
        new(InstalacaoErrorCodes.ExecutorEntreGestores, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.executor_entre_gestores", "Quem executa a instalação não pode estar entre os gestores")),
        new(InstalacaoErrorCodes.GestoresInvalidos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.gestores_invalidos", "Gestores da instalação inválidos")),
        new(InstalacaoErrorCodes.DocumentoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.documento_invalido", "Documento de designação inválido")),
        new(InstalacaoErrorCodes.SituacaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.situacao_invalida", "A instalação não está na situação que este passo exige")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Confirmacao() =>
    [
        new(InstalacaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.instalacao.nao_encontrada", "Instalação não encontrada")),
        new(InstalacaoErrorCodes.EmailNaoInstitucional, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.email_nao_institucional", "O e-mail da sessão não é institucional verificado")),
        new(InstalacaoErrorCodes.CodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.codigo_invalido", "Código de confirmação inválido", RecusaAposGravacao: true)),
        new(InstalacaoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.instalacao.autor_nao_identificado", "Autor da operação não identificado")),
        MapeamentosComuns.Concorrencia(InstalacaoErrorCodes.ConflitoDeConcorrencia),
    ];
}
