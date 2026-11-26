namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

internal sealed class KernelDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. LeituraDaRequisicao(),
        .. ValidacaoDaRequisicao(),
        .. TiposDeValor(),
        .. EnderecoPostal(),
        .. LocalizacaoDoEndereco(),
        .. ReferenciaDeCidade(),
        .. ParametrosDeConsulta(),
        .. CopiaDaOfertaDeCurso(),
    ];

    // Falhas de desserialização e binding acontecem antes da execução do comando.
    private static KeyValuePair<string, DomainErrorMapping>[] LeituraDaRequisicao() =>
    [
        new(InvalidRequestErrorCodes.MissingRequiredField, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.campo_obrigatorio_ausente", "A requisição não declara campo obrigatório do contrato")),
        new(InvalidRequestErrorCodes.Malformed, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.malformada", "A requisição não pôde ser lida")),
        new(InvalidRequestErrorCodes.MissingBody, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.corpo_ausente", "A requisição não traz corpo")),
        new(InvalidRequestErrorCodes.InvalidValue, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.requisicao.valor_invalido", "A requisição traz valor que não corresponde ao tipo declarado")),
    ];

    // O validador usa o código público tanto no ProblemDetails quanto nos itens de errors[].
    private static KeyValuePair<string, DomainErrorMapping>[] ValidacaoDaRequisicao() =>
    [
        new(ValidationErrorCodes.Raiz, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, ValidationErrorCodes.Raiz, "Erro de validação")),
        new(ValidationErrorCodes.Obrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, ValidationErrorCodes.Obrigatorio, "Campo obrigatório não informado")),
        new(ValidationErrorCodes.Tamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, ValidationErrorCodes.Tamanho, "Campo fora do tamanho permitido")),
        new(ValidationErrorCodes.Formato, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, ValidationErrorCodes.Formato, "Campo em formato inválido")),
        new(ValidationErrorCodes.Faixa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, ValidationErrorCodes.Faixa, "Valor fora do intervalo permitido")),
        new(ValidationErrorCodes.Regra, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, ValidationErrorCodes.Regra, "Campo não atende a regra de validação")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] TiposDeValor() =>
    [
        new("Cpf.Vazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cpf.vazio", "CPF obrigatório")),
        new("Cpf.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cpf.invalido", "CPF inválido")),
        new("Email.Vazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.email.vazio", "E-mail obrigatório")),
        new("Email.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.email.invalido", "E-mail inválido")),
        new("NomeSocial.NomeCivilVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.nome_social.nome_civil_vazio", "Nome civil obrigatório")),
        new("NotaFinal.Negativa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.nota_final.negativa", "Nota final inválida")),
        new("Percentual.ForaDeFaixa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.percentual.fora_de_faixa", "Percentual fora do intervalo permitido")),
    ];

    // O endereço estruturado é compartilhado por Campus, LocalOferta e Instituição.
    private static KeyValuePair<string, DomainErrorMapping>[] EnderecoPostal() =>
    [
        new(EnderecoReferenciaErrorCodes.CepObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cep_obrigatorio", "CEP é obrigatório")),
        new(EnderecoReferenciaErrorCodes.CepFormatoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cep_formato_invalido", "CEP em formato inválido")),
        new(EnderecoReferenciaErrorCodes.LogradouroTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.logradouro_tamanho", "Logradouro excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.NumeroTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.numero_tamanho", "Número do endereço excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.ComplementoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.complemento_tamanho", "Complemento do endereço excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.BairroTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.bairro_tamanho", "Bairro excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.DistritoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.distrito_tamanho", "Distrito excede o tamanho permitido")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] LocalizacaoDoEndereco() =>
    [
        new(EnderecoReferenciaErrorCodes.NivelResolucaoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.nivel_resolucao_obrigatorio", "Nível de resolução do endereço é obrigatório")),
        new(EnderecoReferenciaErrorCodes.NivelResolucaoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.nivel_resolucao_invalido", "Nível de resolução do endereço fora do vocabulário")),
        new(EnderecoReferenciaErrorCodes.OrigemObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.origem_obrigatoria", "Origem do endereço é obrigatória")),
        new(EnderecoReferenciaErrorCodes.OrigemTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.origem_tamanho", "Origem do endereço excede o tamanho permitido")),
        new(EnderecoReferenciaErrorCodes.LatitudeForaDeFaixa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.latitude_fora_de_faixa", "Latitude fora do intervalo permitido")),
        new(EnderecoReferenciaErrorCodes.LongitudeForaDeFaixa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.longitude_fora_de_faixa", "Longitude fora do intervalo permitido")),
        new(EnderecoReferenciaErrorCodes.CidadeIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cidade_incoerente", "Cidade do endereço diverge da referência de cidade da entidade")),
        new(EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.endereco_referencia.cidade_obrigatoria_com_endereco", "Endereço informado exige a referência de cidade da entidade")),
    ];

    // Um único registro para a referência de cidade mantém o mesmo contrato em todos os módulos.
    private static KeyValuePair<string, DomainErrorMapping>[] ReferenciaDeCidade() =>
    [
        new(CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.codigo_ibge_obrigatorio", "Código IBGE da cidade é obrigatório")),
        new(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.codigo_ibge_formato_invalido", "Código IBGE da cidade em formato inválido")),
        new(CidadeReferenciaErrorCodes.UfObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.uf_obrigatoria", "UF da cidade é obrigatória")),
        new(CidadeReferenciaErrorCodes.UfIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.uf_incoerente", "UF informada incompatível com o prefixo do código IBGE")),
        new(CidadeReferenciaErrorCodes.NomeObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.nome_obrigatorio", "Nome da cidade é obrigatório")),
        new(CidadeReferenciaErrorCodes.NomeCaractereInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.nome_caractere_invalido", "Nome da cidade contém o caractere nulo ou caractere que não é texto")),
        new(CidadeReferenciaErrorCodes.NomeTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cidade_referencia.nome_tamanho", "Nome da cidade excede o tamanho máximo")),
    ];

    // Os parâmetros comuns de ordenação e busca têm a mesma recusa em todos os módulos.
    private static KeyValuePair<string, DomainErrorMapping>[] ParametrosDeConsulta() =>
    [
        new(ConsultaErrorCodes.CampoDeOrdenacaoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.consulta.campo_de_ordenacao_invalido", "Campo de ordenação não aceito por esta listagem")),
        new(ConsultaErrorCodes.OrdenacaoMalFormada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.consulta.ordenacao_mal_formada", "Expressão de ordenação inválida")),
        new(ConsultaErrorCodes.BuscaMuitoLonga, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.consulta.busca_muito_longa", "Texto pesquisado excede o comprimento aceito")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] CopiaDaOfertaDeCurso() =>
    [
        new(CopiaDaOfertaDeCursoErrorCodes.Invalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.copia_da_oferta_de_curso.invalida", "A oferta de curso não tem curso, grau, unidade ofertante e turno válidos")),
    ];
}
