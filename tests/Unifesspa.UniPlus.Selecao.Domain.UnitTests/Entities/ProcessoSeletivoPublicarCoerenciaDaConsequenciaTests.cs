namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

public sealed class ProcessoSeletivoPublicarCoerenciaDaConsequenciaTests
{
    [Fact(DisplayName = "CA-05 (1/5 — heteroidentificação/indígena): ELIMINA é incoerente com RETIRA_A_MODALIDADE da modalidade PPI")]
    public void Publicar_HeteroidentificacaoElimina_IncoerenteComAcaoDaModalidadePpi()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "ELIMINA_DO_PROCESSO"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (2/5 — modalidade de cota): ELIMINA é incoerente com RETIRA_A_MODALIDADE de uma cota reservada (comprovante de renda)")]
    public void Publicar_ComprovanteRendaElimina_IncoerenteComAcaoDaModalidadeDeCota()
    {
        ModalidadeSelecionada cota = NovaModalidadeComAcao(
            "LB_Q", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(cota);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RENDA", "Comprovante de renda familiar", "RENDA", "ELIMINA_DO_PROCESSO"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (3/5 — categoria incompatível): exigência CONDICIONAL só alcança a modalidade pelo gatilho, não pela categoria do documento")]
    public void Publicar_LaudoMedicoElimina_IncoerenteComAcaoDaModalidadeAlcancadaPorGatilho()
    {
        ModalidadeSelecionada suplementar = NovaModalidadeComAcao(
            "PSIQ_INDIGENA", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(suplementar);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalPorModalidadeComConsequencia(faseId, "LAUDO_MEDICO", "Laudo médico", "SAUDE", "PSIQ_INDIGENA", "ELIMINA_DO_PROCESSO"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue(
            "a categoria do documento (SAUDE) não isenta a coerência — o que importa é a modalidade que o gatilho alcança");
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (4/5 — RETIRA_O_ACRESCIMO sem acréscimo oferecido): recusado quando o processo não tem o FADA configurado")]
    public void Publicar_AcrescimoNaoOferecido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RESIDENCIA_CONVENIO", "Comprovante de residência no município do convênio", "FADA", "RETIRA_O_ACRESCIMO"), 0).Value!]).IsSuccess.Should().BeTrue();
        // O processo declara que não aplica o Fator de Atenuação das Desigualdades de Acesso (FADA, UNI-REQ-0015): sem configuração do FADA
        // não há "acréscimo oferecido" para a consequência RETIRA_O_ACRESCIMO remover.

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConsequenciasDoDescumprimento.AcrescimoNaoOferecido");
    }

    [Fact(DisplayName = "CA-05: RETIRA_O_ACRESCIMO com o FADA configurado é aceito (contraprova)")]
    public void Publicar_AcrescimoOferecido_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirFada(
            aplica: true, configuracao: ConfiguracaoDoFada.Criar(ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", HashFixo), fator: 1.2m, teto: null, fatoElegibilidadeCodigo: FormularioDeTeste.FatoBooleanoDaInscricao, new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!).IsSuccess.Should().BeTrue();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RESIDENCIA_CONVENIO", "Comprovante de residência no município do convênio", "FADA", "RETIRA_O_ACRESCIMO"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static ProcessoSeletivo ProcessoQueEliminaPorFaixaEtariaNaModalidadeQueReclassifica()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalPorFaixaEtariaComConsequencia(faseId, "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", "ELIMINA_DO_PROCESSO"), 0).Value!]).IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    // O gatilho desta exigência é inteiramente sobre FAIXA_ETARIA — nenhuma condição
    // sobre MODALIDADE. Antes da correção, ModalidadesAlcancadasPor avaliava o gatilho
    // com um dicionário de fatos só com MODALIDADE; a condição de FAIXA_ETARIA (fato
    // ausente) avaliava como falsa (PredicadoDnf.Avaliar), reprovando a cláusula
    // inteira e escondendo QUALQUER modalidade da conferência CA-05 — publicaria incoerente.
    [Fact(DisplayName = "CA-05 (5/5 — gatilho não-modal): ELIMINA condicionado só a FAIXA_ETARIA (nenhuma condição de MODALIDADE) ainda é incoerente com RETIRA_A_MODALIDADE")]
    public void Publicar_GatilhoPorFaixaEtariaSemCondicaoDeModalidadeElimina_IncoerenteComAcaoDaModalidade()
    {
        ProcessoSeletivo processo = ProcessoQueEliminaPorFaixaEtariaNaModalidadeQueReclassifica();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue(
            "um gatilho sem NENHUMA condição de MODALIDADE não isenta a exigência do CA-05 — ela alcança " +
            "candidatos de qualquer modalidade, inclusive a PPI incoerente");
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    // A ausência de AcaoQuandoIndeferido é declaração, não omissão: a modalidade não
    // reclassifica, e quem responde pelo destino do candidato indeferido é a consequência
    // da exigência documental. Nenhuma das treze modalidades semeadas declara ação — se
    // null passasse a ser lido como RETIRA_A_MODALIDADE, toda exigência ELIMINA do catálogo
    // real deixaria de publicar. Este teste é o que segura essa leitura.
    [Fact(DisplayName = "CA-05: modalidade sem ação de indeferimento declarada não é confrontada — a consequência da exigência decide sozinha")]
    public void Publicar_ModalidadeSemAcaoDeclarada_NaoDisparaCa05()
    {
        ModalidadeSelecionada semAcao = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, acaoQuandoIndeferido: null);
        ProcessoSeletivo processo = NovoProcessoComModalidade(semAcao);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "ELIMINA_DO_PROCESSO"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(
            "ELIMINA seria incoerente com uma ação declarada de reclassificação, mas esta "
            + "modalidade não declara nenhuma — não há o que confrontar");
    }

    [Fact(DisplayName = "A mesma ação de reclassificação na exigência e na modalidade é coerente")]
    public void Publicar_ReclassificaAcComAcaoRealReclassificaAc_Aceita()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalPorModalidadeComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "LB_PPI", "RETIRA_A_MODALIDADE"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact]
    public void Causas_AcrescimoEmExigenciaEGrupoSemOferta_NaoCompareComAcaoDaModalidade()
    {
        ModalidadeSelecionada modalidade = NovaModalidadeComAcao("LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(modalidade);
        Guid fase = processo.CronogramaFases.Single().Id;
        DocumentoExigido documento = ExigenciaGeralComConsequencia(fase, "DOC", "Documento", "PESSOAL", "RETIRA_O_ACRESCIMO");
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "RETIRA_O_ACRESCIMO"), 0, [BaseLegalDaComprovacao.Criar("Lei 12.711/2012", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!], [item]).Value!;
        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();
        FieldError[] causas = [.. CoerenciaDaConsequenciaDoDescumprimento.Causas(processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade, CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas))];
        causas.Select(causa => causa.Error.Code).Should().Equal("ConsequenciasDoDescumprimento.AcrescimoNaoOferecido", "ConsequenciasDoDescumprimento.AcrescimoNaoOferecido");
        causas.Select(causa => causa.Field).Should().Equal("documentosExigidos.exigencias[0].consequenciaDoDescumprimento", "documentosExigidos.regrasDeComprovacao[0].consequenciaDoDescumprimento");
    }

    private static DocumentoExigido ExigenciaGeralComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria, string consequenciaDoDescumprimento) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipoDocumentoCodigo, tipoDocumentoNome, tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FormularioDeTeste.CodigoDoPapel(FinalidadeFormulario.Inscricao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, consequenciaDoDescumprimento, [BaseLegalResolvidaQualquer()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;

    private static DocumentoExigido ExigenciaCondicionalPorFaixaEtariaComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria,
        string consequenciaDoDescumprimento) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipoDocumentoCodigo, tipoDocumentoNome, tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FormularioDeTeste.CodigoDoPapel(FinalidadeFormulario.Inscricao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, consequenciaDoDescumprimento, [BaseLegalResolvidaQualquer()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;
}
