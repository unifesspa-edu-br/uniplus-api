namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// No processo com inscrição pelo sistema, o conjunto básico de dados do candidato está em formulário
/// obrigatório da fase que coleta inscrição (UNI-REQ-0151); só em formulário facultativo é recusado.
/// </summary>
public sealed class ConjuntoBasicoNaInscricaoTests
{
    private static readonly string[] ConjuntoBasico = [.. ConjuntoBasicoDaInscricao.Fatos];

    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _isencao = FaseDeTeste.Criar(2, "ISENCAO", coletaSolicitacaoIsencao: true);

    private static ProcessoDosFormularios PeloSistema => new(InscricaoPeloSistema: true, CobraTaxa: false, PrimeiraFaseDepoisDaImportacaoId: null);

    private IReadOnlyList<string> Codigos(ProcessoDosFormularios processo, params ColetaDoFormulario[] coletas) =>
        [.. ConjuntoBasicoNaInscricao.Conferir(processo, coletas, [_inscricao, _isencao]).Select(static recusa => recusa.Error.Code)];

    [Fact(DisplayName = "O conjunto básico no formulário obrigatório da inscrição é aceito")]
    public void ConjuntoBasicoNoObrigatorio_Aceita() =>
        Codigos(PeloSistema, ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), ConjuntoBasico)).Should().BeEmpty();

    [Fact(DisplayName = "O conjunto básico dividido entre dois formulários obrigatórios da inscrição é aceito")]
    public void ConjuntoBasicoDivididoEntreObrigatorios_Aceita() =>
        Codigos(
            PeloSistema,
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO_A", _inscricao), [.. ConjuntoBasico.Take(2)]),
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO_B", _inscricao), [.. ConjuntoBasico.Skip(2)])).Should().BeEmpty();

    [Fact(DisplayName = "O conjunto básico só em formulário facultativo é recusado")]
    public void ConjuntoBasicoSoNoFacultativo_Recusa() =>
        Codigos(
            PeloSistema,
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar"),
            ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("EXTRA", _inscricao), ConjuntoBasico)).Should().Equal(FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico);

    [Fact(DisplayName = "O conjunto básico em formulário de fora da fase de inscrição é recusado")]
    public void ConjuntoBasicoForaDaInscricao_Recusa() =>
        Codigos(
            PeloSistema,
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar"),
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("PEDIDO", _isencao), ConjuntoBasico)).Should().Equal(FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico);

    [Fact(DisplayName = "O conjunto básico em campos de grupo repetível não conta como dados do candidato")]
    public void ConjuntoBasicoEmGrupoRepetivel_Recusa() =>
        Codigos(
            PeloSistema,
            ColetaDeTeste.ColetaComGrupo(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "dependentes", ConjuntoBasico)).Should().Equal(FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico);

    [Fact(DisplayName = "O processo de resultado importado não é conferido")]
    public void ResultadoImportado_NaoEConferido() =>
        Codigos(PeloSistema with { InscricaoPeloSistema = false }, ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar")).Should().BeEmpty();

    [Fact(DisplayName = "Sem formulário obrigatório na inscrição, a recusa é só a da obrigatoriedade")]
    public void SemObrigatorioNaInscricao_NaoDuplicaARecusa() =>
        Codigos(PeloSistema, ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("EXTRA", _inscricao), "origem_escolar")).Should().BeEmpty();

    [Fact(DisplayName = "A recusa não lista os campos que faltam")]
    public void Recusa_NaoListaOsCampos()
    {
        IReadOnlyList<Unifesspa.UniPlus.Kernel.Results.FieldError> recusas = ConjuntoBasicoNaInscricao.Conferir(
            PeloSistema, [ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar")], [_inscricao, _isencao]);

        recusas.Should().ContainSingle().Which.Error.Message.Should().NotContain(ConjuntoBasico[0]);
    }
}
