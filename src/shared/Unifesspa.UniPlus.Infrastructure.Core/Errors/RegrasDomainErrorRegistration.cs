namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

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
        new("EstruturaFormulario.EtapaCodigoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_codigo_duplicado", "O código de etapa se repete no formulário")),
        new("EstruturaFormulario.EtapaOrdemDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_ordem_duplicada", "Duas etapas do formulário têm a mesma ordem")),
        new("EstruturaFormulario.TipoDeEtapaObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.tipo_de_etapa_obrigatorio", "A etapa é uma seção ou um bloco de sistema")),
        new("EstruturaFormulario.SecaoComBloco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.secao_com_bloco", "Uma seção não declara bloco de sistema")),
        new("EstruturaFormulario.BlocoNaoAdmitido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_nao_admitido", "O bloco não é admitido pela finalidade do formulário")),
        new("EstruturaFormulario.BlocoRepetido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_repetido", "O bloco aparece mais de uma vez no formulário")),
        new("EstruturaFormulario.BlocoExigidoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_exigido_ausente", "Falta um bloco que a finalidade exige")),
        new("EstruturaFormulario.RevisaoEAceiteForaDoFim", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.revisao_e_aceite_fora_do_fim", "A revisão e aceite é sempre a última etapa")),
        new("EstruturaFormulario.ItemForaDeSecao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.item_fora_de_secao", "O item precisa estar numa seção do formulário")),
        new("EstruturaFormulario.ItemForaDaOrdemDasSecoes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.item_fora_da_ordem_das_secoes", "A ordem dos itens não acompanha a ordem das seções")),
        new(EstruturaFormularioErrorCodes.FinalidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.finalidade_invalida", "A finalidade do formulário é inscrição, isenção de taxa ou habilitação")),
        new(EstruturaFormularioErrorCodes.TituloTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.titulo_tamanho", "O título do formulário excede o tamanho máximo")),
        new(EstruturaFormularioErrorCodes.EtapaCodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_codigo_invalido", "O código da etapa é obrigatório e tem tamanho limitado")),
        new(EstruturaFormularioErrorCodes.EtapaOrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_ordem_invalida", "A ordem da etapa não pode ser negativa")),
        new(EstruturaFormularioErrorCodes.EtapaTituloInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_titulo_invalido", "O título da etapa é obrigatório e tem tamanho limitado")),
        new(EstruturaFormularioErrorCodes.EtapaTextoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_texto_tamanho", "A descrição ou o aviso da etapa excede o tamanho máximo")),
        new(EstruturaFormularioErrorCodes.ExibicaoForaDeSecao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.exibicao_fora_de_secao", "Só a seção do formulário tem exibição condicional")),
        new(GrafoFormularioErrorCodes.FatoDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.fato_duplicado", "O fato aparece mais de uma vez no formulário")),
        new(GrafoFormularioErrorCodes.OrdemDuplicada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.ordem_duplicada", "A ordem de coleta do formulário precisa ser total")),
        new(GrafoFormularioErrorCodes.GrafoComCiclo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.grafo_com_ciclo", "As regras dos campos formam um ciclo")),
        new(GrafoFormularioErrorCodes.CitaFatoNaoConhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_fato_nao_conhecido", "Uma regra do formulário cita fato que ele não conhece")),
        new(GrafoFormularioErrorCodes.CitaFatoPosterior, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_fato_posterior", "Uma regra do formulário cita fato conhecido só depois dela")),
        new(GrafoFormularioErrorCodes.CitaAtributoDoCandidato, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_atributo_do_candidato", "A regra do formulário cita fato calculado de atributos do candidato")),
        new(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_fato_de_membro_fora_do_grupo", "A regra cita campo de grupo repetível fora do grupo")),
        new(GrupoFormularioErrorCodes.CodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.codigo_invalido", "O código do grupo é obrigatório e tem tamanho limitado")),
        new(GrupoFormularioErrorCodes.OrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.ordem_invalida", "A ordem do grupo não pode ser negativa")),
        new(GrupoFormularioErrorCodes.RotuloInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.rotulo_invalido", "O rótulo do grupo é obrigatório e tem tamanho limitado")),
        new(GrupoFormularioErrorCodes.ContagemIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.contagem_incoerente", "O mínimo e o máximo de ocorrências do grupo são incoerentes")),
        new(GrupoFormularioErrorCodes.SubitensForaDoLimite, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.subitens_fora_do_limite", "A quantidade de campos do grupo está fora do limite")),
        new(GrupoFormularioErrorCodes.RegraAutorreferente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.regra_autorreferente", "A regra do grupo cita o próprio grupo")),
        new(GrupoFormularioErrorCodes.CampoComSecaoPropria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.campo_com_secao_propria", "O campo do grupo segue a seção do grupo")),
        new(ItemFormularioErrorCodes.FatoCodigoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_codigo_obrigatorio", "O código do fato do item é obrigatório")),
        new(ItemFormularioErrorCodes.FatoCodigoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_codigo_tamanho", "O código do fato do item excede o tamanho máximo")),
        new(ItemFormularioErrorCodes.OrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.ordem_invalida", "A ordem do item não pode ser negativa")),
        new(ItemFormularioErrorCodes.RotuloObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.rotulo_obrigatorio", "O rótulo do item é obrigatório")),
        new(ItemFormularioErrorCodes.RotuloTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.rotulo_tamanho", "O rótulo do item excede o tamanho máximo")),
        new(ItemFormularioErrorCodes.TipoRenderizacaoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.tipo_renderizacao_obrigatorio", "O tipo de campo do item é obrigatório")),
        new(ItemFormularioErrorCodes.AjudaTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.ajuda_tamanho", "Ajuda do campo acima do tamanho máximo")),
        new(ItemFormularioErrorCodes.FormatoIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.formato_incoerente", "Formato do campo incoerente com o tipo de campo")),
        new(ItemFormularioErrorCodes.RegraAutorreferente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.regra_autorreferente", "Uma regra do item cita o próprio fato")),
        new(ItemFormularioErrorCodes.RestricaoIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.restricao_incoerente", "A restrição de valor não se aplica ao tipo do campo")),
        new(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.opcional_que_alimenta_regra", "Campo que alimenta derivação, agregado ou negação precisa ser obrigatório sempre que exibido")),
        new(ItemFormularioErrorCodes.TipoRenderizacaoIncoerenteComDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.tipo_renderizacao_incoerente_com_dominio", "O tipo de campo não é coerente com o domínio do fato")),
        new(ItemFormularioErrorCodes.FatoDesconhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_desconhecido", "O fato do item não pertence ao catálogo de fatos do candidato")),
        new(ItemFormularioErrorCodes.FatoNaoColetavel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_nao_coletavel", "O fato do item não é coletável num campo")),
        new(ItemFormularioErrorCodes.ItensEmExcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.itens_em_excesso", "O formulário excede o teto de itens")),
        new(ItemFormularioErrorCodes.OpcoesDeOutroDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.opcoes_de_outro_dominio", "As opções vêm de um campo com outra fonte ou outro domínio de valores")),
        new(VinculoCatalogoErrorCodes.FatoDesativado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.vinculo_catalogo.fato_desativado", "O fato está desativado no catálogo e não aceita vínculo novo")),
        new(VinculoCatalogoErrorCodes.ValorDesativado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.vinculo_catalogo.valor_desativado", "O valor está desativado no catálogo e não aceita vínculo novo")),
        new(TermoFormularioErrorCodes.CodigoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.codigo_obrigatorio", "O código do termo exigido é obrigatório")),
        new(TermoFormularioErrorCodes.CodigoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.codigo_tamanho", "O código do termo exigido excede o tamanho máximo")),
        new(TermoFormularioErrorCodes.OrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.ordem_invalida", "A ordem do termo não pode ser negativa")),
        new(TermoFormularioErrorCodes.CodigoDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.codigo_duplicado", "O código aparece em mais de um termo do formulário")),
        new(TermoFormularioErrorCodes.OrdemDuplicada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.ordem_duplicada", "Dois termos do formulário têm a mesma ordem")),
        new(RestricaoValorErrorCodes.TipoDesconhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.tipo_desconhecido", "O tipo da restrição de valor não é reconhecido")),
        new(RestricaoValorErrorCodes.LimitesIncoerentes, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.limites_incoerentes", "Os limites da restrição de valor são incoerentes")),
        new(RestricaoValorErrorCodes.OpcoesVazias, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.opcoes_vazias", "As opções permitidas precisam de valores")),
        new(RestricaoValorErrorCodes.FatosVazios, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.fatos_vazios", "As opções formadas pelas respostas precisam citar fatos")),
        new(RestricaoValorErrorCodes.FormaJsonInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.forma_json_invalida", "A restrição de valor não tem a forma esperada")),
        new(RestricaoValorErrorCodes.TipoRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.tipo_repetido", "O item declara no máximo uma restrição de cada tipo")),
        new("PredicadoDnf.FormaJsonInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.forma_json_invalida", "O predicado ou a obrigatoriedade não tem a forma esperada")),
        new("ClausulaDnf.ClausulaVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.clausula_dnf.clausula_vazia", "Uma cláusula do predicado deve ter ao menos uma condição")),
        new(PredicadoDnfErrorCodes.CondicaoNula, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.condicao_nula", "O predicado contém uma condição nula")),
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
