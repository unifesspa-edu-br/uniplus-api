namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Com inscrição própria e classificação baseada em ENEM, o candidato escolhe na inscrição a edição
/// do ENEM cujas notas a classificação usa: a publicação exige o campo da edição, pedido a todo
/// candidato, e as edições admitidas. Sem inscrição própria, nenhuma das duas exigências vale.
/// </summary>
public sealed class EdicaoDoEnemNaInscricaoTests
{
    private const string ItemDoCampo = "formulario_inscricao_sem_edicao_enem";
    private const string ItemDasEdicoes = "classificacao_edicao_enem_nao_declarada";

    private static ConfiguracaoClassificacao ClassificacaoImportadaDoEnem() =>
        ConfiguracaoClassificacao.Criar(
            ProcessoConformeFactory.Regra(RegraCalculoCodigo.ClassificacaoImportada, "a"), null, null,
            ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"), 1, [],
            baseadoEmEnem: true, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;

    private static ProcessoSeletivo ProcessoDoEnem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirClassificacao(ClassificacaoImportadaDoEnem(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
                EdicaoDoEnem.Fato, [OpcaoDeclaradaFato.Criar(EdicaoDoEnem.Fato, "2026", "ENEM 2026", 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FatoColetado CampoDaEdicao(
        Obrigatoriedade? obrigatoriedade = null, IReadOnlyList<CondicaoPrecondicaoFato>? exibicao = null, string secao = FormularioDeTeste.Secao) =>
        FatoColetado.Criar(
            EdicaoDoEnem.Fato, FormularioDeTeste.PrimeiraOrdemDeInscricao, "Qual edição do ENEM você vai usar?", TipoRenderizacao.SelecaoUnica,
            obrigatoriedade ?? Obrigatoriedade.Sempre, exibicao, "PESSOAL", OrigemValoresColeta.OpcoesDoProcesso, etapaCodigo: secao).Value!;

    private static bool ItemOk(ProcessoSeletivo processo, string codigo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo)
            .Single(i => i.Codigo == codigo).Ok;

    private static PredicadoDnf SeCasado() =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("ESTADO_CIVIL", Operador.Igual, JsonSerializer.SerializeToElement("CASADO")).Value!)]).Value!;

    [Theory(DisplayName = "Com ENEM e inscrição própria, a publicação e o checklist exigem o campo da edição no formulário de inscrição")]
    [InlineData(true)]
    [InlineData(false)]
    public void Publicacao_SemOCampoDaEdicao_Recusa(bool comOCampo)
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        processo.DefinirItens(comOCampo ? [CampoDaEdicao()] : []).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo);

        (pendencia?.Code).Should().Be(comOCampo ? null : FormularioProcessoErrorCodes.InscricaoSemEdicaoDoEnem);
        ItemOk(processo, ItemDoCampo).Should().Be(comOCampo);
    }

    [Fact(DisplayName = "O campo da edição com obrigatoriedade condicional não pede a edição a todo candidato e é recusado")]
    public void Publicacao_CampoComObrigatoriedadeCondicional_Recusa()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        processo.DefinirItens([CampoDaEdicao(obrigatoriedade: Obrigatoriedade.Quando(SeCasado()))]).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FormularioProcessoErrorCodes.InscricaoSemEdicaoDoEnem);
    }

    [Fact(DisplayName = "O campo da edição com exibição condicional não pede a edição a todo candidato e é recusado")]
    public void Publicacao_CampoComExibicaoCondicional_Recusa()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        processo.DefinirItens(
            [CampoDaEdicao(exibicao: [CondicaoPrecondicaoFato.Criar(0, "ESTADO_CIVIL", Operador.Igual, JsonSerializer.SerializeToElement("CASADO")).Value!])])
            .IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FormularioProcessoErrorCodes.InscricaoSemEdicaoDoEnem);
    }

    [Fact(DisplayName = "O campo da edição numa seção que se esconde não pede a edição a todo candidato e é recusado")]
    public void Publicacao_CampoEmSecaoCondicional_Recusa()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        FormularioProcesso inscricao = processo.FormularioDe(FinalidadeFormulario.Inscricao)!;
        processo.DefinirFormulario(
                FinalidadeFormulario.Inscricao, inscricao.FaseId, inscricao.Titulo,
                [
                    FormularioDeTeste.Etapas()[0],
                    EtapaFormulario.Criar(FormularioDeTeste.Secao, 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, SeCasado()).Value!,
                    EtapaFormulario.Criar("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
                ],
                PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([CampoDaEdicao()], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FormularioProcessoErrorCodes.InscricaoSemEdicaoDoEnem);
    }

    [Fact(DisplayName = "Com o resultado importado, a classificação baseada em ENEM não exige o campo da edição nem as edições admitidas")]
    public void Conformidade_ImportacaoExterna_NaoExigeEdicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Importado", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirClassificacao(ClassificacaoImportadaDoEnem(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemConformidade> itens =
            processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo);

        itens.Should().NotContain(i => i.Codigo == ItemDasEdicoes);
        itens.Single(i => i.Codigo == ItemDoCampo).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Com inscrição própria e sem formulário de inscrição, a falta é só a do formulário")]
    public void Conformidade_SemFormularioDeInscricao_SoOFormularioFicaVermelho()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ItemOk(processo, "formulario_inscricao_ausente").Should().BeFalse();
        ItemOk(processo, ItemDoCampo).Should().BeTrue();
    }
}
