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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

public sealed class ProcessoSeletivoPublicarCoerenciaDaConsequenciaTests
{
    [Fact(DisplayName = "CA-05 (1/5 — heteroidentificação/indígena): ELIMINA é incoerente com RECLASSIFICA_AC da modalidade PPI")]
    public void Publicar_HeteroidentificacaoElimina_IncoerenteComAcaoDaModalidadePpi()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RECLASSIFICA_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "ELIMINA"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (2/5 — modalidade de cota): ELIMINA é incoerente com RECLASSIFICA_AC de uma cota reservada (comprovante de renda)")]
    public void Publicar_ComprovanteRendaElimina_IncoerenteComAcaoDaModalidadeDeCota()
    {
        ModalidadeSelecionada cota = NovaModalidadeComAcao(
            "LB_Q", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RECLASSIFICA_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(cota);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RENDA", "Comprovante de renda familiar", "RENDA", "ELIMINA"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (3/5 — categoria incompatível): exigência CONDICIONAL só alcança a modalidade pelo gatilho, não pela categoria do documento")]
    public void Publicar_LaudoMedicoElimina_IncoerenteComAcaoDaModalidadeAlcancadaPorGatilho()
    {
        ModalidadeSelecionada suplementar = NovaModalidadeComAcao(
            "PSIQ_INDIGENA", NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, "ABRE_PRAZO_PARA_REENVIO");
        ProcessoSeletivo processo = NovoProcessoComModalidade(suplementar);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalPorModalidadeComConsequencia(faseId, "LAUDO_MEDICO", "Laudo médico", "SAUDE", "PSIQ_INDIGENA", "ELIMINA"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue(
            "a categoria do documento (SAUDE) não isenta a coerência — o que importa é a modalidade que o gatilho alcança");
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "CA-05 (4/5 — REMOVE_VANTAGEM sem vantagem viva): recusado quando o processo não tem bônus regional configurado")]
    public void Publicar_RemoveVantagemSemVantagemViva_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RESIDENCIA_CONVENIO", "Comprovante de residência no município do convênio", "BONUS_REGIONAL", "REMOVE_VANTAGEM"), 0).Value!]).IsSuccess.Should().BeTrue();
        // O processo declara que não aplica o bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015): sem configuração do bônus
        // não há "vantagem viva" para a consequência REMOVE_VANTAGEM remover.

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "CA-05: REMOVE_VANTAGEM com bônus regional configurado é aceito (contraprova)")]
    public void Publicar_RemoveVantagemComVantagemViva_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirBonusRegional(
            aplica: true, bonus: ConfiguracaoBonusRegional.Criar(ReferenciaRegra.De(RegraDeBonus.Multiplicativo.Codigo(), "v1", HashFixo), fator: 1.2m, teto: null, fatoElegibilidadeCodigo: FormularioDeTeste.FatoBooleanoDaInscricao, new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!).IsSuccess.Should().BeTrue();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "COMPROVANTE_RESIDENCIA_CONVENIO", "Comprovante de residência no município do convênio", "BONUS_REGIONAL", "REMOVE_VANTAGEM"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static ProcessoSeletivo ProcessoQueEliminaPorFaixaEtariaNaModalidadeQueReclassifica()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalPorFaixaEtariaComConsequencia(faseId, "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", "ELIMINA"), 0).Value!]).IsSuccess.Should().BeTrue();
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
    [Fact(DisplayName = "CA-05 (5/5 — gatilho não-modal): ELIMINA condicionado só a FAIXA_ETARIA (nenhuma condição de MODALIDADE) ainda é incoerente com RECLASSIFICAR_AC — achado de revisão da PR #903")]
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
    // null passasse a ser lido como RECLASSIFICAR_AC, toda exigência ELIMINA do catálogo
    // real deixaria de publicar. Este teste é o que segura essa leitura.
    [Fact(DisplayName = "CA-05: modalidade sem ação de indeferimento declarada não é confrontada — a consequência da exigência decide sozinha")]
    public void Publicar_ModalidadeSemAcaoDeclarada_NaoDisparaCa05()
    {
        ModalidadeSelecionada semAcao = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, acaoQuandoIndeferido: null);
        ProcessoSeletivo processo = NovoProcessoComModalidade(semAcao);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "ELIMINA"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(
            "ELIMINA seria incoerente com uma ação declarada de reclassificação, mas esta "
            + "modalidade não declara nenhuma — não há o que confrontar");
    }

    // DocumentoExigido.ConsequenciaIndeferimento aceita "RECLASSIFICA_AC" (rol fechado
    // desde a PR #895), mas ModalidadeSelecionada.AcaoQuandoIndeferido — snapshot-copy do
    // cadastro real de Modalidade — só aceita "RECLASSIFICAR_AC"/"RECLASSIFICAR_REGRA_EDITAL"
    // (ck_modalidades_acao_quando_indeferido, módulo Configuração). Comparar os tokens
    // crus sempre reprovaria o único caso de reclassificação coerente que existe.
    [Fact(DisplayName = "CA-05: RECLASSIFICA_AC (vocabulário de DocumentoExigido) é coerente com RECLASSIFICAR_AC (vocabulário real de ModalidadeSelecionada.AcaoQuandoIndeferido) — achado de revisão da PR #903")]
    public void Publicar_ReclassificaAcComAcaoRealReclassificarAc_Aceita()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeralComConsequencia(faseId, "RESULTADO_HETEROIDENTIFICACAO", "Resultado da banca de heteroidentificação", "HETEROIDENTIFICACAO", "RECLASSIFICA_AC"), 0).Value!]).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static DocumentoExigido ExigenciaGeralComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria, string consequenciaIndeferimento) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipoDocumentoCodigo, tipoDocumentoNome, tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FinalidadeFormulario.Inscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, consequenciaIndeferimento, [BaseLegalResolvidaQualquer()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;

    private static DocumentoExigido ExigenciaCondicionalPorFaixaEtariaComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria,
        string consequenciaIndeferimento) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipoDocumentoCodigo, tipoDocumentoNome, tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FinalidadeFormulario.Inscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, consequenciaIndeferimento, [BaseLegalResolvidaQualquer()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;
}
