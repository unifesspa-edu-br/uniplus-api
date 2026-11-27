namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A fase organizada em chamadas sem formulário obrigatório sai da mesma regra na gravação, na publicação,
/// na retificação e no painel, no item próprio, sem acender o da isenção.
/// </summary>
public sealed class ChamadasSemFormularioObrigatorioNaConferenciaTests
{
    private const string ItemDasChamadas = "formulario_chamadas_sem_obrigatorio";
    private const string ItemDaIsencao = "formulario_isencao_sem_taxa";

    private static ProcessoSeletivo ProcessoComFasesEmChamadas(int quantidade)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        FaseCronograma[] novas =
        [
            .. Enumerable.Range(0, quantidade).Select(static indice =>
                FaseOrganizadaEmChamadasDeTeste.Criar(2 + indice, indice == 0 ? "HABILITACAO" : "AVALIACAO", $"chamadas{indice}")),
        ];
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, .. novas], []).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static string[] CodigosDaGravacao(ProcessoSeletivo processo) =>
        [.. CondicoesParaRegistrarConfiguracao.Causas(processo.Declaracao(), CadastrosDeTeste.Padrao).Select(static causa => causa.Error.Code)];

    private static string[] CodigosAoCongelar(ProcessoSeletivo processo) =>
        [.. ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors.Select(static causa => causa.Error.Code)];

    private static string[] CamposDasChamadas(IEnumerable<FieldError> causas) =>
        [.. causas.Where(static causa => causa.Error.Code == FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio).Select(static causa => causa.Field ?? string.Empty)];

    private static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(item => item.Codigo == codigo);

    [Fact(DisplayName = "A fase organizada em chamadas sem obrigatório recusa na gravação, ao congelar e no painel, uma recusa por fase")]
    public void SemObrigatorio_RecusaEmTodosOsMomentos()
    {
        ProcessoSeletivo processo = ProcessoComFasesEmChamadas(2);

        string[] esperado = [FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio, FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio];
        CodigosDaGravacao(processo).Where(static codigo => codigo == FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio).Should().Equal(esperado);
        CodigosAoCongelar(processo).Where(static codigo => codigo == FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio).Should().Equal(esperado);
        Item(processo, ItemDasChamadas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Cada fase sem obrigatório é apontada pela posição dela no documento, na gravação e ao congelar")]
    public void SemObrigatorio_ApontaAPosicaoDeCadaFase()
    {
        ProcessoSeletivo processo = ProcessoComFasesEmChamadas(2);

        CamposDasChamadas(CondicoesParaRegistrarConfiguracao.Causas(processo.Declaracao(), CadastrosDeTeste.Padrao)).Should().Equal("fases[1]", "fases[2]");
        CamposDasChamadas(ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors)
            .Should().Equal("fases[1]", "fases[2]");
        PendenciasDosFormularios.PendenciasDasChamadas(processo.Declaracao()).Select(static recusa => recusa.Field).Should().Equal("fases[1]", "fases[2]");
    }

    [Fact(DisplayName = "Com faltas simultâneas, a gravação e o congelamento trazem inscrição, isenção e chamadas, nessa ordem")]
    public void FaltasSimultaneas_SaemNaOrdemDaRegra()
    {
        ProcessoSeletivo processo = ProcessoSemInscricaoComTaxaSemIsencaoEFaseEmChamadas();
        string[] ordemDaRegra =
        [
            FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio,
            FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao,
            FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio,
        ];

        CodigosDaGravacao(processo).Where(ordemDaRegra.Contains).Should().Equal(ordemDaRegra);
        CodigosAoCongelar(processo).Where(ordemDaRegra.Contains).Should().Equal(ordemDaRegra);
    }

    private static ProcessoSeletivo ProcessoSemInscricaoComTaxaSemIsencaoEFaseEmChamadas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(coletaSolicitacaoIsencao: true));
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Inscricao).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(cobra: true, valor: 100m, fundamentosCodigos: ["CARENCIA_SOCIOECONOMICA"]).Value!)
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases(
            [.. processo.CronogramaFases, FaseOrganizadaEmChamadasDeTeste.Criar(2, "HABILITACAO", "chamadas0")], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "A recusa das chamadas não acende o item da isenção, e a pendência da isenção não a traz")]
    public void SemObrigatorio_NaoAcendeOItemDaIsencao()
    {
        ProcessoSeletivo processo = ProcessoComFasesEmChamadas(1);

        Item(processo, ItemDaIsencao).Ok.Should().BeTrue();
        PendenciasDosFormularios.PendenciasDaIsencao(processo.Declaracao()).Should().BeEmpty();
        PendenciasDosFormularios.PendenciasDasChamadas(processo.Declaracao()).Should().ContainSingle();
    }

    [Fact(DisplayName = "Com o formulário obrigatório na fase organizada em chamadas, nada é recusado e o item fica verde")]
    public void ComObrigatorio_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComFasesEmChamadas(1);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);

        CodigosDaGravacao(processo).Should().NotContain(FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio);
        CodigosAoCongelar(processo).Should().NotContain(FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio);
        Item(processo, ItemDasChamadas).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "A gravação, o congelamento e o painel concordam sobre quantas fases faltam")]
    public void Paridade_EntreOsMomentos()
    {
        ProcessoSeletivo processo = ProcessoComFasesEmChamadas(2);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);

        int pendencias = PendenciasDosFormularios.PendenciasDasChamadas(processo.Declaracao()).Count;

        pendencias.Should().Be(1);
        CodigosDaGravacao(processo).Count(static codigo => codigo == FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio).Should().Be(pendencias);
        CodigosAoCongelar(processo).Count(static codigo => codigo == FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio).Should().Be(pendencias);
        Item(processo, ItemDasChamadas).Ok.Should().Be(pendencias == 0);
    }

    [Fact(DisplayName = "O processo sem fase organizada em chamadas não tem a pendência")]
    public void SemFaseOrganizadaEmChamadas_NaoTemPendencia()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        PendenciasDosFormularios.PendenciasDasChamadas(processo.Declaracao()).Should().BeEmpty();
        Item(processo, ItemDasChamadas).Ok.Should().BeTrue();
    }
}
