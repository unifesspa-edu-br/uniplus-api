namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// Mapeamento dos códigos de erro emitidos pelas regras sobre fatos do candidato
/// (<c>Unifesspa.UniPlus.Regras</c>): predicado, condição, cláusula, descritor do fato e
/// derivação. Registrados uma única vez, com código de wire sem prefixo de módulo, porque
/// Configuração e Seleção emitem os mesmos códigos e o registry é global — o último
/// registro de um código venceria sem aviso (ADR-0135).
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, RegrasDomainErrorRegistration>() em AddDomainErrorMapper().")]
internal sealed class RegrasDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new("ClausulaDnf.ClausulaVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.clausula_dnf.clausula_vazia", "Uma cláusula do predicado deve ter ao menos uma condição")),
        new("CondicaoDnf.FatoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.condicao_dnf.fato_obrigatorio", "O fato da condição é obrigatório")),
        new("CondicaoDnf.OperadorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.condicao_dnf.operador_invalido", "O operador da condição não é reconhecido")),
        new("CondicaoDnf.FormaIncoerenteComOperador", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.condicao_dnf.forma_incoerente_com_operador", "A forma do valor não é coerente com o operador da condição")),
        new("PredicadoDnf.FatoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.fato_desconhecido", "O fato da condição não pertence ao vocabulário fechado")),
        new(PredicadoDnfErrorCodes.OperadorIncompativelComDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.operador_incompativel_com_dominio", "O operador da condição não é compatível com o domínio do fato")),
        new(PredicadoDnfErrorCodes.ValorIncompativelComTipo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.valor_incompativel_com_tipo", "O valor da condição não é compatível com o tipo do fato")),
        new(PredicadoDnfErrorCodes.ValorForaDoDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.valor_fora_do_dominio", "O valor da condição não pertence ao domínio declarado do fato")),
        new("PredicadoDnf.FatoNaoColetadoPeloProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.fato_nao_coletado_pelo_processo", "O fato da condição não é coletado por este processo")),
        new("PredicadoDnf.DominioDinamicoNaoFornecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.dominio_dinamico_nao_fornecido", "O domínio dinâmico do fato não foi fornecido pelo chamador")),
        new("DescritorFatoCandidato.DominioIncoerente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.descritor_fato_candidato.dominio_incoerente", "A combinação de domínio e valores declarados do fato é incoerente")),
        new("RegraDerivacao.ContribuiObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regra_derivacao.contribui_obrigatorio", "Uma regra de derivação precisa contribuir um código")),
        new("RegrasDerivacaoFato.SemRegras", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.sem_regras", "A derivação de um fato precisa de ao menos uma regra")),
        new("RegrasDerivacaoFato.ContribuiForaDoDominio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.contribui_fora_do_dominio", "Uma regra contribui um código fora do domínio do fato")),
        new("RegrasDerivacaoFato.DependenciasIncoerentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.dependencias_incoerentes", "As dependências declaradas não são exatamente os fatos citados")),
        new("RegrasDerivacaoFato.DerivacaoAutorreferente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.regras_derivacao_fato.derivacao_autorreferente", "Um fato derivado não pode depender de si mesmo")),
    ];
}
