namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, AutorizacaoDomainErrorRegistration>().")]
internal sealed class AutorizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(PerfilErrorCodes.NomeInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.nome_invalido", "Nome do perfil inválido")),
        new(PerfilErrorCodes.DescricaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.descricao_invalida", "Descrição do perfil inválida")),
        new(PerfilErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.sem_permissao", "O perfil precisa reunir ao menos uma permissão")),
        new(PerfilErrorCodes.PermissaoInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_inexistente", "Permissão fora do catálogo")),
        new(PerfilErrorCodes.PermissaoReservada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_reservada", "Permissão reservada aos perfis de governança")),
        new(PerfilErrorCodes.GovernancaImutavel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.governanca_imutavel", "Perfil de governança não pode ser alterado")),
        new(PerfilErrorCodes.NomeDuplicado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.perfil.nome_duplicado", "Já existe perfil com este nome")),
        new(PerfilErrorCodes.IdDivergente, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.perfil.id_divergente", "O identificador da URL não corresponde ao do corpo da requisição")),
        new(PerfilErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.perfil.nao_encontrado", "Perfil de acesso não encontrado")),

        // Mesmo código do conflito que o GlobalExceptionMiddleware emite para endpoints sem
        // idempotência: a corrida é a mesma, só muda quem a traduz. Retentável, porque a
        // requisição idêntica repetida depois da corrida lê a versão nova.
        new(PerfilErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true)),

        new(ConcessaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.concessao.nao_encontrada", "Concessão de acesso não encontrada")),
        new(ConcessaoErrorCodes.JaRevogada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.ja_revogada", "Concessão de acesso já revogada")),
        new(ConcessaoErrorCodes.MinimoDeGestores, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.minimo_de_gestores", "A revogação deixaria menos de dois gestores de acessos")),
        new(ConcessaoErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.motivo_invalido", "Motivo da revogação inválido")),
        new(ConcessaoErrorCodes.PerfilInativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.perfil_inativo", "Perfil de acesso desativado não aceita concessão")),
        new(ConcessaoErrorCodes.Duplicada, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.concessao.duplicada", "Já existe concessão igual não revogada, vigente ou vencida; revogue-a antes de conceder de novo")),
        new(ConcessaoErrorCodes.ValidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.validade_invalida", "Fim da vigência inválido")),
        new(ConcessaoErrorCodes.EscopoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.escopo_invalido", "Escopo incoerente ou não admitido pelo perfil")),
        new(ConcessaoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.concessao.autor_nao_identificado", "Autor da operação não identificado")),
        new(ConcessaoErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true)),

        new(InstalacaoErrorCodes.JaHaGestor, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.ja_ha_gestor", "Já existe gestor de acessos ativo")),
        new(InstalacaoErrorCodes.ExecutorEntreGestores, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.executor_entre_gestores", "Quem executa a instalação não pode estar entre os gestores")),
        new(InstalacaoErrorCodes.GestoresInvalidos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.gestores_invalidos", "Gestores da instalação inválidos")),
        new(InstalacaoErrorCodes.DocumentoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.documento_invalido", "Documento de designação inválido")),
        new(InstalacaoErrorCodes.SituacaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.situacao_invalida", "A instalação não está na situação que este passo exige")),
        new(InstalacaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.instalacao.nao_encontrada", "Instalação não encontrada")),
        new(InstalacaoErrorCodes.EmailNaoInstitucional, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.email_nao_institucional", "O e-mail da sessão não é institucional verificado")),
        new(InstalacaoErrorCodes.CodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.codigo_invalido", "Código de confirmação inválido")),
        new(InstalacaoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.instalacao.autor_nao_identificado", "Autor da operação não identificado")),
        new(InstalacaoErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true)),
    ];
}
