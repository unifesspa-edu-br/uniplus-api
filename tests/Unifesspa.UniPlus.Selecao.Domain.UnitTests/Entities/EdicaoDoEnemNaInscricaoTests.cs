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
using Unifesspa.UniPlus.Selecao.Domain.Errors;
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
            ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"), [],
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

    private static ConfiguracaoClassificacao ClassificacaoImportadaSemEnem() =>
        ConfiguracaoClassificacao.Criar(
            ProcessoConformeFactory.Regra(RegraCalculoCodigo.ClassificacaoImportada, "a"), null, null,
            ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"), [],
            baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;

    [Fact(DisplayName = "A classificação que deixa de ser baseada em ENEM retira as edições do ENEM declaradas")]
    public void DefinirClassificacao_DeixaDeSerEnem_RetiraAsEdicoes()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();

        processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.OpcoesDoProcesso(EdicaoDoEnem.Fato).Should().BeEmpty();
    }

    [Fact(DisplayName = "Voltar à classificação baseada em ENEM exige declarar as edições de novo")]
    public void DefinirClassificacao_VoltaAoEnem_ExigeDeclararAsEdicoes()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ClassificacaoImportadaDoEnem(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ItemOk(processo, ItemDasEdicoes).Should().BeFalse();
    }

    [Fact(DisplayName = "Deixar de ser ENEM é recusado quando uma condição viva cita uma edição, e as edições ficam")]
    public void DefinirClassificacao_EdicaoCitadaPorCondicao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        FatoColetado citaAEdicao = FatoColetado.Criar(
            "APELIDO", FormularioDeTeste.PrimeiraOrdemDeInscricao + 1, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre,
            [CondicaoPrecondicaoFato.Criar(0, EdicaoDoEnem.Fato, Operador.Igual, JsonSerializer.SerializeToElement("2026")).Value!],
            "PESSOAL", formato: "LIVRE", etapaCodigo: FormularioDeTeste.Secao).Value!;
        Result itens = processo.DefinirItens([CampoDaEdicao(), citaAEdicao]);
        itens.IsSuccess.Should().BeTrue(itens.Error?.Message ?? string.Join("; ", itens.Errors.Select(static e => e.Error.Message)));

        Result resultado = processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Curinga);

        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "baseadoEmEnem" && e.Error.Code == OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva);
        processo.OpcoesDoProcesso(EdicaoDoEnem.Fato).Should().ContainSingle();
    }

    [Theory(DisplayName = "Sem classificação baseada em ENEM, a publicação e o checklist recusam o formulário que pede a edição")]
    [InlineData(true)]
    [InlineData(false)]
    public void Publicacao_CampoDaEdicaoSemEnem_Recusa(bool comOCampo)
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        processo.DefinirItens(comOCampo ? [CampoDaEdicao()] : []).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo);

        (pendencia?.Code).Should().Be(comOCampo ? FormularioProcessoErrorCodes.EdicaoDoEnemSemClassificacaoEnem : null);
        ItemOk(processo, "formulario_edicao_enem_sem_classificacao_enem").Should().Be(!comOCampo);
    }

    [Fact(DisplayName = "Sem ENEM, o campo da edição vem antes do termo sem forma de aceite no checklist, na ordem em que a publicação recusa")]
    public void Checklist_CampoDaEdicaoSemEnem_NaOrdemDaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoDoEnem();
        processo.DefinirItens([CampoDaEdicao()]).IsSuccess.Should().BeTrue();
        TermoExigidoFormulario termoSemForma = TermoExigidoFormulario.Criar(
            "DECLARACAO", 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "Declaração", "Texto", "Base legal", "A_DEFINIR", new string('a', 64)),
            null, Obrigatoriedade.Sempre).Value!;
        processo.DefinirTermos([termoSemForma], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportadaSemEnem(), PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        List<string> vermelhos = [.. processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo)
            .Where(static i => !i.Ok).Select(static i => i.Codigo)];

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code
            .Should().Be(FormularioProcessoErrorCodes.EdicaoDoEnemSemClassificacaoEnem);
        vermelhos.IndexOf("formulario_edicao_enem_sem_classificacao_enem")
            .Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(vermelhos.IndexOf("termo_exigido_sem_forma_de_aceite"));
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
