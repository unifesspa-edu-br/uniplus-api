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
    ];
}
