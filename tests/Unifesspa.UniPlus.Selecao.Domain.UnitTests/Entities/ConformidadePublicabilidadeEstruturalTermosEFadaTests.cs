namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

public sealed class ConformidadePublicabilidadeEstruturalTermosEFadaTests
{
    private static TermoExigidoFormulario Termo(
        string codigo, int ordem, FormaAceite formaAceite = FormaAceite.RegistroSemEnderecoIp, PredicadoDnf? exibicao = null) =>
        TermoExigidoFormulario.Criar(
            codigo, ordem,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Declaração", "Texto", "Base legal", formaAceite, new string('a', 64)),
            exibicao, Obrigatoriedade.Sempre).Value!;

    private static PredicadoDnf CondicaoDeOpcaoDeTeste(string opcao) => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("FATO_OPCAO_DE_TESTE", Operador.Igual, JsonSerializer.SerializeToElement(opcao)).Value!)]).Value!;

    [Fact(DisplayName = "Termo cuja versão não definiu a forma de aceite: item vermelho e publicação recusada; com registro digital, liberado")]
    public void TermoSemFormaDeAceite_RecusaAteHaverRegistroDigital()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensNaFaseDaImportacao([]).IsSuccess.Should().BeTrue();
        processo.DefinirTermosNaFaseDaImportacao([Termo("DECLARACAO", 0, FormaAceite.ADefinir)]).IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be("ProcessoSeletivo.TermoExigidoSemFormaDeAceite");
        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("termo_exigido_sem_forma_de_aceite");

        processo.DefinirTermosNaFaseDaImportacao([Termo("DECLARACAO", 0)]).IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Termos com código ou ordem repetidos são recusados, cada recusa no índice do termo")]
    public void DefinirTermos_CodigoEOrdemRepetidos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        processo.DefinirTermos([Termo("DECLARACAO", 0), Termo("DECLARACAO", 0)])
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [
                ("termos[1].codigo", TermoFormularioErrorCodes.CodigoDuplicado),
                ("termos[1].ordem", TermoFormularioErrorCodes.OrdemDuplicada),
            ]);
        processo.TermosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Condição de termo que cita fato que o processo deixou de coletar recusa a publicação")]
    public void TermoCitaFatoNaoColetado_RecusaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensNaFaseDaImportacao([FatoDeOpcaoDeTeste()]).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")]).IsSuccess.Should().BeTrue();
        processo.DefinirTermosNaFaseDaImportacao([Termo("DECLARACAO", 0, exibicao: CondicaoDeOpcaoDeTeste("2025"))])
            .IsSuccess.Should().BeTrue();
        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo).Should().BeNull();

        processo.DefinirItensNaFaseDaImportacao([]).IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Redefinir opções recusa deixar de fora uma opção citada pela condição de um termo")]
    public void DefinirOpcoesDeclaradas_OpcaoCitadaPorTermo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensNaFaseDaImportacao([FatoDeOpcaoDeTeste()]).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2024"), Opcao("2025", 1)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosNaFaseDaImportacao([Termo("DECLARACAO", 0, exibicao: CondicaoDeOpcaoDeTeste("2024"))])
            .IsSuccess.Should().BeTrue();

        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")])
            .Error!.Code.Should().Be("OpcaoDeclaradaFato.ReferenciadaPorExigenciaViva");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Municípios do FADA como opções
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static FatoColetado FatoMunicipioDoFada() => FatoColetadoDeTeste.Coletando("MUNICIPIO_EM_AREA_FADA", 0)
        .ComRotulo("Município")
        .DoTipo(TipoDeCampo.EscolhaUnica)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComOrigem(OrigemValoresColeta.MunicipiosDoFada)
        .Construir();

    private static ConfiguracaoDoFada Fada(params (string CodigoIbge, string Nome, string Uf)[] municipios) =>
        ConfiguracaoDoFada.Criar(ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", new string('a', 64)), 1.20m, null, null, new BaseLegalCongelada(Guid.NewGuid(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [.. municipios])).Value!;

    [Fact(DisplayName = "Fato coletado com os municípios do FADA num processo sem FADA: item vermelho; com FADA, liberado")]
    public void FatoDosMunicipiosDoFadaSemFada_RecusaAteHaverFada()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirFada(aplica: false, configuracao: null).IsSuccess.Should().BeTrue();
        processo.DefinirItensNaFaseDaImportacao([FatoMunicipioDoFada()]).IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");

        processo.DefinirFada(aplica: true, configuracao: Fada(("1504208", "Marabá", "PA"))).IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo).Should().BeNull();
    }

    private static DocumentoExigido ExigenciaPorMunicipioDeParauapebas(Guid faseId) => new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COMPROVANTE_PARAUAPEBAS", "Comprovante de escolaridade em Parauapebas", "ACADEMICO", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, "MUNICIPIO_EM_AREA_FADA", Operador.Igual, JsonSerializer.SerializeToElement("1505536")).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [BaseLegalResolvida()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;

    [Fact(DisplayName = "Redefinir o FADA recusa tirar da área um município citado por exigência viva")]
    public void DefinirFada_MunicipioCitadoPorExigencia_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirFada(aplica: true, configuracao: Fada(("1504208", "Marabá", "PA"), ("1505536", "Parauapebas", "PA")))
            .IsSuccess.Should().BeTrue();
        processo.DefinirItensNaFaseDaImportacao([FatoMunicipioDoFada()]).IsSuccess.Should().BeTrue();
        ExigirDocumentos(processo, ExigenciaPorMunicipioDeParauapebas(FaseDoResultado(processo)));

        processo.DefinirFada(aplica: true, configuracao: Fada(("1504208", "Marabá", "PA")))
            .Error!.Code.Should().Be("ProcessoSeletivo.MunicipioDoFadaReferenciadoPorCondicaoViva");
        processo.DefinirFada(aplica: false, configuracao: null)
            .Error!.Code.Should().Be("ProcessoSeletivo.MunicipioDoFadaReferenciadoPorCondicaoViva");
    }

    [Fact(DisplayName = "Município citado fora da área do FADA é pendência de publicação mesmo quando o campo saiu da coleta ao redefinir o FADA")]
    public void MunicipioCitadoForaDaArea_ContornandoPelaColeta_PendenciaDePublicacao()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirFada(aplica: true, configuracao: Fada(("1504208", "Marabá", "PA"), ("1505536", "Parauapebas", "PA")))
            .IsSuccess.Should().BeTrue();
        processo.DefinirItensNaFaseDaImportacao([FatoMunicipioDoFada()]).IsSuccess.Should().BeTrue();
        ExigirDocumentos(processo, ExigenciaPorMunicipioDeParauapebas(FaseDoResultado(processo)));

        processo.DefinirItensNaFaseDaImportacao([]).IsSuccess.Should().BeTrue();
        processo.DefinirFada(aplica: true, configuracao: Fada(("1504208", "Marabá", "PA"))).IsSuccess.Should().BeTrue();
        processo.DefinirItensNaFaseDaImportacao([FatoMunicipioDoFada()]).IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be("ProcessoSeletivo.MunicipioDoFadaReferenciadoPorCondicaoViva");
        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_municipio_citado_fora_da_area_do_fada");
    }
}
