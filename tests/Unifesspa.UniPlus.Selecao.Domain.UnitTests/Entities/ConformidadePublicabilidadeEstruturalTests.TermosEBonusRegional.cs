namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class ConformidadePublicabilidadeEstruturalTests
{
    private static TermoExigidoFormulario Termo(
        string codigo, int ordem, string formaAceite = "REGISTRO_DIGITAL_SEM_LOG_IP", PredicadoDnf? exibicao = null) =>
        TermoExigidoFormulario.Criar(
            codigo, ordem,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "Declaração", "Texto", "Base legal", formaAceite, new string('a', 64)),
            exibicao, Obrigatoriedade.Sempre).Value!;

    private static PredicadoDnf CondicaoDeOpcaoDeTeste(string opcao) => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("FATO_OPCAO_DE_TESTE", Operador.Igual, JsonSerializer.SerializeToElement(opcao)).Value!)]).Value!;

    [Fact(DisplayName = "Termo cuja versão não definiu a forma de aceite: item vermelho e publicação recusada; com registro digital, liberado")]
    public void TermoSemFormaDeAceite_RecusaAteHaverRegistroDigital()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensComFaseDeInscricao([]).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([Termo("DECLARACAO", 0, "A_DEFINIR")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be("ProcessoSeletivo.TermoExigidoSemFormaDeAceite");
        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("termo_exigido_sem_forma_de_aceite");

        processo.DefinirTermos([Termo("DECLARACAO", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull();
    }

    [Fact(DisplayName = "Termos com código ou ordem repetidos são recusados, cada recusa no índice do termo")]
    public void DefinirTermos_CodigoEOrdemRepetidos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        processo.DefinirTermos([Termo("DECLARACAO", 0), Termo("DECLARACAO", 0)], PrecondicaoIfMatch.Ausente)
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [
                ("termos[1].codigo", TermoFormularioErrorCodes.CodigoDuplicado),
                ("termos[1].ordem", TermoFormularioErrorCodes.OrdemDuplicada),
            ]);
        processo.TermosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Códigos de termo que só diferem na forma Unicode são o mesmo código")]
    public void DefinirTermos_CodigoEmFormaUnicodeDiferente_ERepetido()
    {
        ProcessoSeletivo processo = ProcessoConforme();

        processo.DefinirTermos([Termo("DECLARAÇÃO", 0), Termo("DECLARAC\u0327A\u0303O", 1)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(TermoFormularioErrorCodes.CodigoDuplicado);
    }

    [Fact(DisplayName = "Condição de termo que cita fato que o processo deixou de coletar recusa a publicação")]
    public void TermoCitaFatoNaoColetado_RecusaPublicacao()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensComFaseDeInscricao([FatoDeOpcaoDeTeste()]).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([Termo("DECLARACAO", 0, exibicao: CondicaoDeOpcaoDeTeste("2025"))], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull();

        processo.DefinirItensComFaseDeInscricao([]).IsSuccess.Should().BeTrue();

        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Redefinir opções recusa deixar de fora uma opção citada pela condição de um termo")]
    public void DefinirOpcoesDeclaradas_OpcaoCitadaPorTermo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensComFaseDeInscricao([FatoDeOpcaoDeTeste()]).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2024"), Opcao("2025", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermos([Termo("DECLARACAO", 0, exibicao: CondicaoDeOpcaoDeTeste("2024"))], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("OpcaoDeclaradaFato.ReferenciadaPorExigenciaViva");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Municípios do bônus regional como opções (issue #1691)
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static FatoColetado FatoMunicipioDoBonus() => FatoColetado.Criar(
        "MUNICIPIO_EM_AREA_BONUS", 0, "Município", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
        origemValores: OrigemValoresColeta.MunicipiosDoBonus, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static ConfiguracaoBonusRegional Bonus(params (string CodigoIbge, string Nome, string Uf)[] municipios) =>
        ConfiguracaoBonusRegional.Criar(
            ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)).Value!,
            1.20m, null, null, Guid.NewGuid(), "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional",
            municipios).Value!;

    [Fact(DisplayName = "Fato coletado com os municípios do bônus num processo sem bônus: item vermelho; com bônus, liberado")]
    public void FatoDosMunicipiosDoBonusSemBonus_RecusaAteHaverBonus()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirBonusRegional(aplica: false, bonus: null, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItensComFaseDeInscricao([FatoMunicipioDoBonus()]).IsSuccess.Should().BeTrue();

        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");

        processo.DefinirBonusRegional(aplica: true, bonus: Bonus(("1504208", "Marabá", "PA")), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull();
    }

    [Fact(DisplayName = "Redefinir o bônus recusa tirar da área um município citado por exigência viva")]
    public void DefinirBonusRegional_MunicipioCitadoPorExigencia_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirBonusRegional(aplica: true, bonus: Bonus(("1504208", "Marabá", "PA"), ("1505536", "Parauapebas", "PA")), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItensComFaseDeInscricao([FatoMunicipioDoBonus()]).IsSuccess.Should().BeTrue();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "COMPROVANTE_PARAUAPEBAS", "Comprovante de escolaridade em Parauapebas", "ACADEMICO", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [CondicaoGatilho.Criar(0, "MUNICIPIO_EM_AREA_BONUS", Operador.Igual, JsonSerializer.SerializeToElement("1505536")).Value!],
            basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirBonusRegional(aplica: true, bonus: Bonus(("1504208", "Marabá", "PA")), PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.MunicipioDoBonusReferenciadoPorCondicaoViva");
        processo.DefinirBonusRegional(aplica: false, bonus: null, PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.MunicipioDoBonusReferenciadoPorCondicaoViva");
    }

    [Fact(DisplayName = "Município citado fora da área do bônus é pendência de publicação mesmo quando o campo saiu da coleta ao redefinir o bônus")]
    public void MunicipioCitadoForaDaArea_ContornandoPelaColeta_PendenciaDePublicacao()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirBonusRegional(aplica: true, bonus: Bonus(("1504208", "Marabá", "PA"), ("1505536", "Parauapebas", "PA")), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItensComFaseDeInscricao([FatoMunicipioDoBonus()]).IsSuccess.Should().BeTrue();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "COMPROVANTE_PARAUAPEBAS", "Comprovante de escolaridade em Parauapebas", "ACADEMICO", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [CondicaoGatilho.Criar(0, "MUNICIPIO_EM_AREA_BONUS", Operador.Igual, JsonSerializer.SerializeToElement("1505536")).Value!],
            basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirItensComFaseDeInscricao([]).IsSuccess.Should().BeTrue();
        processo.DefinirBonusRegional(aplica: true, bonus: Bonus(("1504208", "Marabá", "PA")), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItensComFaseDeInscricao([FatoMunicipioDoBonus()]).IsSuccess.Should().BeTrue();

        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be("ProcessoSeletivo.MunicipioDoBonusReferenciadoPorCondicaoViva");
        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_municipio_citado_fora_da_area_do_bonus");
    }
}
