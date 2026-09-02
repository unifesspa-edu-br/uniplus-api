namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

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
        new("Percentual.ForaDeFaixa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.percentual.fora_de_faixa", "Percentual fora do intervalo permitido")),

        // Referência de endereço estruturado ao Geo (ADR-0096), compartilhada por
        // Campus, LocalOferta e Instituicao.
        new(EnderecoReferenciaErrorCodes.CepObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cep_obrigatorio", "CEP é obrigatório")),
        new(EnderecoReferenciaErrorCodes.CepFormatoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cep_formato_invalido", "CEP em formato inválido")),
        new(EnderecoReferenciaErrorCodes.LogradouroTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.logradouro_tamanho", "Logradouro excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.NumeroTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.numero_tamanho", "Número do endereço excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.ComplementoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.complemento_tamanho", "Complemento do endereço excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.BairroTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.bairro_tamanho", "Bairro excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.DistritoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.distrito_tamanho", "Distrito excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.NivelResolucaoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.nivel_resolucao_obrigatorio", "Nível de resolução do endereço é obrigatório")),
        new(EnderecoReferenciaErrorCodes.NivelResolucaoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.nivel_resolucao_invalido", "Nível de resolução do endereço fora do vocabulário")),
        new(EnderecoReferenciaErrorCodes.OrigemObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.origem_obrigatoria", "Origem do endereço é obrigatória")),
        new(EnderecoReferenciaErrorCodes.OrigemTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.origem_tamanho", "Origem do endereço excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.LatitudeForaDeFaixa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.latitude_fora_de_faixa", "Latitude fora do intervalo permitido")),
        new(EnderecoReferenciaErrorCodes.LongitudeForaDeFaixa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.longitude_fora_de_faixa", "Longitude fora do intervalo permitido")),
        new(EnderecoReferenciaErrorCodes.CidadeIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cidade_incoerente", "Cidade do endereço diverge da referência de cidade da entidade")),
        new(EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cidade_obrigatoria_com_endereco", "Endereço informado exige a referência de cidade da entidade")),

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
