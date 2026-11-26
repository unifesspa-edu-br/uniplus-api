namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Xunit;

/// <summary>
/// Cada recusa das referências aos cadastros do processo, da divulgação pública e da taxa de inscrição chega
/// ao cliente com o status 422 e o código estável do módulo, que o <c>type</c> do problema resolve na página
/// do catálogo.
/// </summary>
public sealed class ErrosDaAdministracaoDoProcessoTests
{
    private static readonly IDomainErrorMapper Mapper = MapperDoModuloDeTeste.Criar();

    [Theory(DisplayName = "A recusa da administração do processo é mapeada para 422 com o código estável do módulo")]
    [InlineData("ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada", "uniplus.selecao.processo_seletivo.unidade_administradora_nao_encontrada")]
    [InlineData("ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo", "uniplus.selecao.processo_seletivo.tipo_processo_nao_encontrado_ou_inativo")]
    [InlineData("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo", "uniplus.selecao.processo_seletivo.tipo_etapa_nao_encontrado_ou_inativo")]
    [InlineData("ProcessoSeletivo.UnidadeAdministradoraSemCidade", "uniplus.selecao.processo_seletivo.unidade_administradora_sem_cidade")]
    [InlineData("ConfiguracaoDivulgacao.CamposPublicosVazio", "uniplus.selecao.configuracao_divulgacao.campos_publicos_vazio")]
    [InlineData("ConfiguracaoDivulgacao.CampoNaoPermitido", "uniplus.selecao.configuracao_divulgacao.campo_nao_permitido")]
    [InlineData("ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio", "uniplus.selecao.configuracao_divulgacao.numero_inscricao_obrigatorio")]
    [InlineData("ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes", "uniplus.selecao.configuracao_divulgacao.formas_de_identificacao_excludentes")]
    [InlineData("ConfiguracaoDivulgacao.JustificativaObrigatoria", "uniplus.selecao.configuracao_divulgacao.justificativa_obrigatoria")]
    [InlineData("ConfiguracaoDivulgacao.JustificativaMuitoLonga", "uniplus.selecao.configuracao_divulgacao.justificativa_muito_longa")]
    [InlineData("ConfiguracaoDivulgacao.JustificativaCaractereInvalido", "uniplus.selecao.configuracao_divulgacao.justificativa_caractere_invalido")]
    [InlineData("RegrasDeNomeAbreviado.IdentificadorDesconhecido", "uniplus.selecao.regras_de_nome_abreviado.identificador_desconhecido")]
    [InlineData("RegrasDeNomeAbreviado.NomeEmBranco", "uniplus.selecao.regras_de_nome_abreviado.nome_em_branco")]
    [InlineData("ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra", "uniplus.selecao.configuracao_taxa_inscricao.valor_obrigatorio_quando_cobra")]
    [InlineData("ConfiguracaoTaxaInscricao.ValorNaoPermitidoQuandoNaoCobra", "uniplus.selecao.configuracao_taxa_inscricao.valor_nao_permitido_quando_nao_cobra")]
    [InlineData("ConfiguracaoTaxaInscricao.FundamentoExigeCobranca", "uniplus.selecao.configuracao_taxa_inscricao.fundamento_exige_cobranca")]
    [InlineData("ConfiguracaoTaxaInscricao.FundamentoDesconhecido", "uniplus.selecao.configuracao_taxa_inscricao.fundamento_desconhecido")]
    [InlineData("ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra", "uniplus.selecao.configuracao_taxa_inscricao.fundamento_obrigatorio_quando_cobra")]
    public void Recusa_MapeadaPara422ComOCodigoEstavel(string codigoDoDominio, string codigoNoFio)
    {
        bool mapeada = Mapper.TryGetMapping(codigoDoDominio, out DomainErrorMapping? mapeamento);

        mapeada.Should().BeTrue();
        mapeamento!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        mapeamento.Code.Should().Be(codigoNoFio);
        mapeamento.Title.Should().NotBeNullOrWhiteSpace();
        Mapper.GetProblemTypeUri(codigoNoFio).Should().Be(MapperDoModuloDeTeste.BaseUriDeErros + codigoNoFio);
    }
}
