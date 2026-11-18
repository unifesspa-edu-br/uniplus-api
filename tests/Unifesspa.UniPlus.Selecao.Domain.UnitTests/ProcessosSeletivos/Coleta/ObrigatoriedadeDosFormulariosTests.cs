namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// A obrigatoriedade dos formulários por fase (UNI-REQ-0144): o processo com inscrição pelo sistema tem
/// formulário obrigatório na fase que coleta inscrição; o de resultado importado não tem formulário nela; a
/// janela de isenção tem formulário facultativo quando há taxa e nenhum quando não há; e o obrigatório só
/// existe na janela que também coleta inscrição.
/// </summary>
public sealed class ObrigatoriedadeDosFormulariosTests
{
    private static readonly ProcessoDosFormularios ComTaxa = new(InscricaoPeloSistema: true, CobraTaxa: true, PrimeiraFaseDepoisDaImportacaoId: null);
    private static readonly ProcessoDosFormularios SemTaxa = ComTaxa with { CobraTaxa = false };

    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);
    private readonly FaseCronograma _inscricaoEIsencao = FaseDeTeste.Criar(3, "INSCRICAO_E_ISENCAO", coletaInscricao: true, coletaSolicitacaoIsencao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private IReadOnlyList<string> Codigos(ProcessoDosFormularios processo, params FormularioDeclarado[] formularios) =>
        Codigos(processo, [.. formularios.Select(static formulario => ColetaDeTeste.Coleta(formulario))]);

    private IReadOnlyList<string> Codigos(ProcessoDosFormularios processo, params ColetaDoFormulario[] coletas) =>
        [.. ObrigatoriedadeDosFormularios.Conferir(processo, coletas, [_inscricao, _isencao, _inscricaoEIsencao, _habilitacao]).Select(static recusa => recusa.Error.Code)];

    [Fact(DisplayName = "A inscrição pelo sistema com formulário obrigatório na fase de inscrição é aceita")]
    public void InscricaoComObrigatorio_Aceita() =>
        Codigos(SemTaxa, ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao)).Should().BeEmpty();

    [Fact(DisplayName = "A inscrição pelo sistema sem formulário obrigatório na fase de inscrição é recusada")]
    public void InscricaoSemObrigatorio_Recusa() =>
        Codigos(SemTaxa, ColetaDeTeste.Facultativo("EXTRA", _inscricao)).Should().Equal(FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio);

    [Fact(DisplayName = "O resultado importado sem formulário na fase de inscrição é aceito")]
    public void ImportadoSemFormularioNaInscricao_Aceita() =>
        Codigos(SemTaxa with { InscricaoPeloSistema = false }, ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao)).Should().BeEmpty();

    [Fact(DisplayName = "O resultado importado com formulário na fase de inscrição é recusado, obrigatório ou facultativo")]
    public void ImportadoComFormularioNaInscricao_Recusa() =>
        Codigos(
            SemTaxa with { InscricaoPeloSistema = false },
            ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao),
            ColetaDeTeste.Facultativo("EXTRA", _inscricao)).Should().Equal(
                FormularioProcessoErrorCodes.InscricaoEmResultadoImportado, FormularioProcessoErrorCodes.InscricaoEmResultadoImportado);

    [Fact(DisplayName = "O obrigatório na janela de isenção que não coleta inscrição é recusado")]
    public void ObrigatorioNaJanelaSemInscricao_Recusa() =>
        Codigos(ComTaxa, ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ColetaDeTeste.Obrigatorio("PEDIDO", _isencao), ColetaDeTeste.Facultativo("PEDIDO_B", _isencao))
            .Should().Equal(FormularioProcessoErrorCodes.FormularioDaIsencaoObrigatorio);

    [Fact(DisplayName = "Na fase que coleta inscrição e pedido de isenção juntas, o obrigatório é o da inscrição e o facultativo, o do pedido")]
    public void FaseDeInscricaoEIsencaoJuntas_Aceita() =>
        Codigos(ComTaxa, ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricaoEIsencao), ColetaDeTeste.Facultativo("PEDIDO", _inscricaoEIsencao)).Should().BeEmpty();

    [Fact(DisplayName = "O processo que cobra taxa sem formulário facultativo na janela de isenção é recusado")]
    public void TaxaSemFormularioDeIsencao_Recusa() =>
        Codigos(ComTaxa, ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao)).Should().Equal(FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao);

    [Fact(DisplayName = "O processo que cobra taxa com formulário facultativo na janela de isenção é aceito")]
    public void TaxaComFormularioDeIsencao_Aceita() =>
        Codigos(ComTaxa, ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ColetaDeTeste.Facultativo("PEDIDO", _isencao)).Should().BeEmpty();

    [Fact(DisplayName = "O formulário facultativo na janela de isenção de processo sem taxa é recusado")]
    public void IsencaoSemTaxa_Recusa() =>
        Codigos(SemTaxa, ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ColetaDeTeste.Facultativo("PEDIDO", _isencao))
            .Should().Equal(FormularioProcessoErrorCodes.IsencaoSemTaxa);

    [Fact(DisplayName = "A primeira fase depois da importação exige formulário obrigatório, sem fato fixo")]
    public void PosImportacao_ExigeFormularioObrigatorio()
    {
        ProcessoDosFormularios importado = SemTaxa with { InscricaoPeloSistema = false, PrimeiraFaseDepoisDaImportacaoId = _habilitacao.Id };

        Codigos(importado, ColetaDeTeste.Facultativo("HABILITACAO", _habilitacao)).Should().Equal(FormularioProcessoErrorCodes.PosImportacaoSemFormularioObrigatorio);
        Codigos(importado, ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase de habilitação sem formulário é aceita quando não é a primeira depois da importação")]
    public void HabilitacaoSemFormulario_Aceita() =>
        Codigos(SemTaxa, ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao)).Should().BeEmpty();

    [Fact(DisplayName = "As recusas independentes saem juntas e apontam o formulário pelo código")]
    public void Recusas_SeAcumulam()
    {
        FieldError[] recusas = [.. ObrigatoriedadeDosFormularios.Conferir(
            ComTaxa, [ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("PEDIDO", _isencao))], [_inscricao, _isencao, _habilitacao])];

        recusas.Select(static recusa => recusa.Error.Code).Should().BeEquivalentTo(
            FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio,
            FormularioProcessoErrorCodes.FormularioDaIsencaoObrigatorio,
            FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao);
        recusas.Single(static recusa => recusa.Error.Code == FormularioProcessoErrorCodes.FormularioDaIsencaoObrigatorio).Field.Should().Be("formularios[PEDIDO]");
    }
}
