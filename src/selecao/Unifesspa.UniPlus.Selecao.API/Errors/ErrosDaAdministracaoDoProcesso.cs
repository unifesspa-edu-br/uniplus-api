namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da unidade administradora, da divulgação e da taxa de inscrição.
/// </summary>
internal static class ErrosDaAdministracaoDoProcesso
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Cursor.* codes vivem em Infrastructure.Core/Pagination/PaginationDomainErrorRegistration —
        // capability cross-module, registrada uma única vez via AddCursorPagination().

        // Unidade administradora do Processo Seletivo.
        new("ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.unidade_administradora_nao_encontrada", "Unidade administradora não encontrada ou não está mais viva")),
        new("ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_processo_nao_encontrado_ou_inativo", "Tipo de processo seletivo não encontrado ou não está ativo")),
        new("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_etapa_nao_encontrado_ou_inativo", "Tipo de etapa não encontrado ou não está ativo")),
        // A localidade que governa a contagem de prazo em dias úteis
        // vem da Unidade administradora — sem cidade cadastrada, criar processo é recusado.
        new("ProcessoSeletivo.UnidadeAdministradoraSemCidade", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.unidade_administradora_sem_cidade", "Unidade administradora não tem cidade cadastrada")),
        new("UnidadeAdministradoraSnapshot.SiglaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.unidade_administradora_snapshot.sigla_obrigatoria", "Sigla da unidade administradora é obrigatória")),
        new("UnidadeAdministradoraSnapshot.SlugObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.unidade_administradora_snapshot.slug_obrigatorio", "Slug da unidade administradora é obrigatório")),
        new("UnidadeAdministradoraSnapshot.NomeObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.unidade_administradora_snapshot.nome_obrigatorio", "Nome da unidade administradora é obrigatório")),
        new("UnidadeAdministradoraSnapshot.TipoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.unidade_administradora_snapshot.tipo_obrigatorio", "Tipo da unidade administradora é obrigatório")),
        // Referência de cidade do Geo (ADR-0090) — usada tanto por
        // UnidadeAdministradoraSnapshot.Criar (Domain) quanto pelo decoder do envelope
        // (LeituraDoEdital.LerIdentidadesUnidade): registrada uma única vez em
        // KernelDomainErrorRegistration, não aqui — DomainErrorMappingRegistry agrega todo
        // IDomainErrorRegistration num único dicionário global por código, e um segundo registro
        // do mesmo CidadeReferenciaErrorCodes sobrescreveria silenciosamente o mapeamento de
        // Configuração/Organização Institucional (ou vice-versa, a depender da ordem de DI).
        // Divulgação pública do Processo Seletivo (UNI-REQ-0050) — ausência já é o
        // default minimizado (só o número de inscrição), toggle por presença.
        new("ConfiguracaoDivulgacao.CamposPublicosVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.campos_publicos_vazio", "A lista de campos públicos não pode ser vazia")),
        new("ConfiguracaoDivulgacao.CampoNaoPermitido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.campo_nao_permitido", "Campo fora do vocabulário de divulgação pública")),
        new("ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.numero_inscricao_obrigatorio", "O número de inscrição é o piso da divulgação pública e não pode ser removido")),
        new("ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.formas_de_identificacao_excludentes", "nome_abreviado e nome não podem ser publicados ao mesmo tempo")),
        new("ConfiguracaoDivulgacao.JustificativaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.justificativa_obrigatoria", "Publicar o nome integral exige justificativa")),
        new("ConfiguracaoDivulgacao.JustificativaMuitoLonga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.justificativa_muito_longa", "A justificativa excede o limite de caracteres")),
        new("ConfiguracaoDivulgacao.JustificativaComCaractereNulo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_divulgacao.justificativa_com_caractere_nulo", "A justificativa contém o caractere nulo (U+0000), que a coluna de texto não admite")),
        new("RegrasDeNomeAbreviado.IdentificadorDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regras_de_nome_abreviado.identificador_desconhecido", "O identificador de regra de abreviação não é conhecido")),
        new("RegrasDeNomeAbreviado.NomeEmBranco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regras_de_nome_abreviado.nome_em_branco", "O nome a abreviar não pode ser vazio ou só espaços")),
        // Taxa de inscrição e isenção do Processo Seletivo. Diferente de
        // ConfiguracaoDivulgacao, ausência aqui NÃO é estado publicável — ver ProcessoSeletivo.ItensEstruturaisDeConformidade.
        new("ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.valor_obrigatorio_quando_cobra", "Processo que cobra taxa exige valor positivo")),
        new("ConfiguracaoTaxaInscricao.ValorNaoPermitidoQuandoNaoCobra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.valor_nao_permitido_quando_nao_cobra", "Processo que declara não cobrar taxa não pode informar valor")),
        new("ConfiguracaoTaxaInscricao.FundamentoExigeCobranca", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.fundamento_exige_cobranca", "Processo que declara não cobrar taxa não pode configurar fundamento de isenção")),
        new("ConfiguracaoTaxaInscricao.FundamentoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.fundamento_desconhecido", "O fundamento de isenção informado não é conhecido")),
        new("ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_taxa_inscricao.fundamento_obrigatorio_quando_cobra", "Processo que cobra taxa de inscrição exige ao menos um fundamento de isenção")),
    ];
}
