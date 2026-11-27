namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// A fase organizada em chamadas tem ao menos um formulário obrigatório: uma recusa por fase, na ordem do
/// cronograma, depois das recusas da inscrição e da isenção e antes da primeira fase depois da importação.
/// </summary>
public sealed class ObrigatoriedadeDasChamadasTests
{
    private static readonly ProcessoDosFormularios SemTaxa = new(InscricaoPeloSistema: true, CobraTaxa: false, PrimeiraFaseDepoisDaImportacaoId: null);

    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseOrganizadaEmChamadasDeTeste.Criar(2, "HABILITACAO", "habilitacao");
    private readonly FaseCronograma _outra = FaseOrganizadaEmChamadasDeTeste.Criar(3, "AVALIACAO", "avaliacao");
    private readonly FaseCronograma _semMarca = FaseDeTeste.Criar(4, "RESULTADO_FINAL");

    private static IReadOnlyList<FieldError> Conferir(ProcessoDosFormularios processo, FaseCronograma[] cronograma, params FormularioDeclarado[] formularios) =>
        ObrigatoriedadeDosFormularios.Conferir(
            processo, [.. formularios.Select(static formulario => ColetaDeTeste.Coleta(formulario))], cronograma);

    private static IReadOnlyList<FieldError> DasChamadas(IReadOnlyList<FieldError> recusas) =>
        [.. recusas.Where(static recusa => recusa.Error.Code == FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio)];

    [Fact(DisplayName = "A fase organizada em chamadas sem formulário obrigatório é recusada, apontada pela posição da fase no documento")]
    public void FaseSemObrigatorio_Recusa()
    {
        IReadOnlyList<FieldError> recusas = Conferir(SemTaxa, [_inscricao, _habilitacao], ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao));

        recusas.Should().ContainSingle();
        recusas[0].Error.Code.Should().Be(FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio);
        recusas[0].Field.Should().Be("fases[1]");
        recusas[0].Error.Message.Should().Contain("HABILITACAO");
    }

    [Fact(DisplayName = "A fase organizada em chamadas com formulário obrigatório é aceita")]
    public void FaseComObrigatorio_Aceita() =>
        Conferir(SemTaxa, [_inscricao, _habilitacao], ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao))
            .Should().BeEmpty();

    [Fact(DisplayName = "O formulário facultativo não basta para a fase organizada em chamadas")]
    public void FaseSoComFacultativo_Recusa() =>
        DasChamadas(Conferir(SemTaxa, [_inscricao, _habilitacao], ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ColetaDeTeste.Facultativo("HABILITACAO", _habilitacao)))
            .Should().ContainSingle();

    [Fact(DisplayName = "O formulário obrigatório de outra fase não basta para a fase organizada em chamadas")]
    public void ObrigatorioDeOutraFase_Recusa() =>
        DasChamadas(Conferir(SemTaxa, [_inscricao, _habilitacao], ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao)))
            .Should().ContainSingle();

    [Fact(DisplayName = "Várias fases organizadas em chamadas sem obrigatório saem juntas, na ordem do cronograma")]
    public void VariasFasesSemObrigatorio_AcumulamNaOrdemDoCronograma() =>
        Conferir(SemTaxa, [_semMarca, _outra, _habilitacao, _inscricao], ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao))
            .Select(static recusa => recusa.Field).Should().Equal("fases[2]", "fases[1]");

    [Fact(DisplayName = "Só a fase sem formulário obrigatório é recusada quando outra organizada em chamadas o tem")]
    public void UmaDasFasesComObrigatorio_RecusaSoAOutra() =>
        Conferir(SemTaxa, [_inscricao, _habilitacao, _outra], ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ColetaDeTeste.Obrigatorio("AVALIACAO", _outra))
            .Select(static recusa => recusa.Field).Should().Equal("fases[1]");

    [Fact(DisplayName = "A fase sem a marca de organizada em chamadas não é afetada, com ou sem formulário")]
    public void FaseSemMarca_NaoEAfetada() =>
        Conferir(SemTaxa, [_inscricao, _semMarca], ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ColetaDeTeste.Facultativo("RESULTADO", _semMarca))
            .Should().BeEmpty();

    [Fact(DisplayName = "Com faltas simultâneas, a recusa das chamadas vem depois das de inscrição e de isenção")]
    public void FaltasSimultaneas_RespeitamAOrdem()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(5, "ISENCAO", coletaSolicitacaoIsencao: true);
        ProcessoDosFormularios processo = SemTaxa with { CobraTaxa = true };

        Conferir(processo, [_inscricao, isencao, _habilitacao]).Select(static recusa => recusa.Error.Code).Should().Equal(
            FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio,
            FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao,
            FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio);
    }

    [Fact(DisplayName = "Com faltas simultâneas, a recusa das chamadas vem antes da primeira fase depois da importação")]
    public void FaltaDasChamadasEDaImportacao_RespeitamAOrdem()
    {
        ProcessoDosFormularios importado = SemTaxa with { InscricaoPeloSistema = false, PrimeiraFaseDepoisDaImportacaoId = _habilitacao.Id };

        Conferir(importado, [_habilitacao]).Select(static recusa => recusa.Error.Code).Should().Equal(
            FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio,
            FormularioProcessoErrorCodes.PosImportacaoSemFormularioObrigatorio);
    }

    [Fact(DisplayName = "Com as quatro faltas simultâneas, saem inscrição, isenção, chamadas e primeira fase depois da importação, nessa ordem")]
    public void QuatroFaltasSimultaneas_RespeitamAOrdem()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(5, "ISENCAO", coletaSolicitacaoIsencao: true);
        ProcessoDosFormularios processo = SemTaxa with { CobraTaxa = true, PrimeiraFaseDepoisDaImportacaoId = _semMarca.Id };

        Conferir(processo, [_inscricao, isencao, _habilitacao, _semMarca]).Select(static recusa => recusa.Error.Code).Should().Equal(
            FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio,
            FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao,
            FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio,
            FormularioProcessoErrorCodes.PosImportacaoSemFormularioObrigatorio);
    }
}
