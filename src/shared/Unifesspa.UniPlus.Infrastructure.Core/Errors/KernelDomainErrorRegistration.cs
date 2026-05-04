namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, KernelDomainErrorRegistration>() em AddDomainErrorMapper().")]
internal sealed class KernelDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new("Cpf.Vazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cpf.vazio", "CPF obrigatório")),
        new("Cpf.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cpf.invalido", "CPF inválido")),
        new("Email.Vazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.email.vazio", "E-mail obrigatório")),
        new("Email.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.email.invalido", "E-mail inválido")),
        new("NomeSocial.NomeCivilVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.nome_social.nome_civil_vazio", "Nome civil obrigatório")),
        new("NotaFinal.Negativa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.nota_final.negativa", "Nota final inválida")),
    ];
}
