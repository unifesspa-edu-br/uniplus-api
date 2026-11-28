namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas das referências aos cadastros do processo (unidade administradora, tipo de processo e tipo de
/// etapa), da divulgação pública e da taxa de inscrição.
/// </summary>
internal static class ErrosDaAdministracaoDoProcesso
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. ReferenciasAosCadastros(),
        .. DivulgacaoPublica(),
        .. TaxaDeInscricao(),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] ReferenciasAosCadastros() =>
    [
        new("ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.unidade_administradora_nao_encontrada", "Unidade administradora não encontrada no cadastro ou removida")),
        new("ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_processo_nao_encontrado_ou_inativo", "Tipo de processo seletivo não encontrado ou não está ativo")),
        new("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_etapa_nao_encontrado_ou_inativo", "Tipo de etapa não encontrado ou não está ativo")),
        new("ProcessoSeletivo.UnidadeAdministradoraSemCidade", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.unidade_administradora_sem_cidade", "Unidade administradora não tem cidade cadastrada")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] DivulgacaoPublica() =>
    [
        new("ConfiguracaoDivulgacao.CamposPublicosVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.campos_publicos_vazio", "A lista de campos públicos não pode ser vazia")),
        new("ConfiguracaoDivulgacao.CampoNaoPermitido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.campo_nao_permitido", "Campo fora do vocabulário de divulgação pública")),
        new("ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.numero_inscricao_obrigatorio", "O número de inscrição é o piso da divulgação pública e não pode ser removido")),
        new("ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.formas_de_identificacao_excludentes", "NOME_ABREVIADO e NOME não podem ser publicados ao mesmo tempo")),
        new("ConfiguracaoDivulgacao.JustificativaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.justificativa_obrigatoria", "Publicar o nome integral exige justificativa")),
        new("ConfiguracaoDivulgacao.JustificativaMuitoLonga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.justificativa_muito_longa", "A justificativa excede o limite de caracteres")),
        new("ConfiguracaoDivulgacao.JustificativaCaractereInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.justificativa_caractere_invalido", "A justificativa contém o caractere nulo, de controle ou de formatação")),
        new("RegrasDeNomeAbreviado.IdentificadorDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regras_de_nome_abreviado.identificador_desconhecido", "O identificador de regra de abreviação não é conhecido")),
        new("RegrasDeNomeAbreviado.NomeEmBranco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regras_de_nome_abreviado.nome_em_branco", "O nome a abreviar não pode ser vazio ou só espaços")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] TaxaDeInscricao() =>
    [
        new("ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.valor_obrigatorio_quando_cobra", "Processo que cobra taxa exige valor positivo")),
        new("ConfiguracaoTaxaInscricao.ValorNaoPermitidoQuandoNaoCobra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.valor_nao_permitido_quando_nao_cobra", "Processo que declara não cobrar taxa não pode informar valor")),
        new("ConfiguracaoTaxaInscricao.FundamentoExigeCobranca", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.fundamento_exige_cobranca", "Processo que declara não cobrar taxa não pode configurar fundamento de isenção")),
        new("ConfiguracaoTaxaInscricao.FundamentoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.fundamento_desconhecido", "O fundamento de isenção informado não é conhecido")),
        new("ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.fundamento_obrigatorio_quando_cobra", "Processo que cobra taxa de inscrição exige ao menos um fundamento de isenção")),
    ];
}
