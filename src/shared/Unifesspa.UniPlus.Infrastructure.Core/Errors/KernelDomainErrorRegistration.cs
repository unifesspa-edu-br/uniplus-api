namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;

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

        // Referência de cidade do Geo (ADR-0090) — CidadeReferenciaErrorCodes é compartilhado por
        // qualquer módulo que guarde cidade_codigo_ibge/cidade_nome/cidade_uf (Campus/LocalOferta
        // em Configuração, Instituicao/Unidade em Organização Institucional,
        // UnidadeAdministradoraSnapshot em Seleção). DomainErrorMappingRegistry agrega TODAS as
        // IDomainErrorRegistration num único dicionário global, chave por código — registrar o
        // mesmo código em mais de um módulo faz o último registrado (ordem de DI) sobrescrever
        // silenciosamente o vendor MIME dos demais, mesmo para requisições de outro módulo.
        // Registro único aqui, com namespace sem prefixo de módulo, é a fonte única de verdade.
        new(CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.codigo_ibge_obrigatorio", "Código IBGE da cidade é obrigatório")),
        new(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.codigo_ibge_formato_invalido", "Código IBGE da cidade em formato inválido")),
        new(CidadeReferenciaErrorCodes.UfObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.uf_obrigatoria", "UF da cidade é obrigatória")),
        new(CidadeReferenciaErrorCodes.UfIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.uf_incoerente", "UF informada incompatível com o prefixo do código IBGE")),
        new(CidadeReferenciaErrorCodes.NomeObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.nome_obrigatorio", "Nome da cidade é obrigatório")),
        new(CidadeReferenciaErrorCodes.NomeCaractereNulo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.nome_caractere_nulo", "Nome da cidade contém caractere nulo")),
        new(CidadeReferenciaErrorCodes.NomeTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.nome_tamanho", "Nome da cidade excede o tamanho máximo")),
    ];
}
