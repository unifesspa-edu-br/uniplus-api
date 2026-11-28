namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// A publicação do processo de resultado importado exige a declaração da importação (UNI-REQ-0144): ao menos um
/// relatório de leiaute bem formado e a primeira fase depois da importação, que é fase do cronograma. O
/// processo com inscrição pelo sistema não declara importação, e a gravação já recusa quem tenta.
/// </summary>
public sealed class ConferenciaDaImportacaoDoResultadoTests
{
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private static ProcessoDosFormularios Importado => new(InscricaoPeloSistema: false, CobraTaxa: false, PrimeiraFaseDepoisDaImportacaoId: null);

    private static ProcessoDosFormularios PeloSistema => new(InscricaoPeloSistema: true, CobraTaxa: false, PrimeiraFaseDepoisDaImportacaoId: null);

    private IReadOnlyList<FieldError> Conferir(ProcessoDosFormularios processo, DeclaracaoDaImportacaoDoResultado? declaracao) =>
        ConferenciaDaImportacaoDoResultado.Conferir(processo, declaracao, [_habilitacao]);

    [Fact(DisplayName = "A declaração com relatórios e a primeira fase do cronograma é aceita")]
    public void DeclaracaoCompleta_Aceita() =>
        Conferir(Importado, LeiauteDeTeste.Declarar(_habilitacao.Id, "CHAMADA_REGULAR", "LISTA_DE_ESPERA")).Should().BeEmpty();

    [Fact(DisplayName = "O processo com inscrição pelo sistema sem declaração é aceito")]
    public void InscricaoPeloSistema_SemDeclaracao_Aceita() => Conferir(PeloSistema, null).Should().BeEmpty();

    [Fact(DisplayName = "O processo de resultado importado sem declaração é recusado como por declarar")]
    public void ImportadoSemDeclaracao_PorDeclarar() =>
        Conferir(Importado, null).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "importacao",
            Error = new { Code = ImportacaoDoResultadoErrorCodes.PorDeclarar },
        });

    [Fact(DisplayName = "A declaração sem relatório é recusada")]
    public void SemRelatorio_Recusa() =>
        Conferir(Importado, LeiauteDeTeste.Declarar(_habilitacao.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "importacao.relatorios",
            Error = new { Code = ImportacaoDoResultadoErrorCodes.SemRelatorio },
        });

    [Fact(DisplayName = "A declaração sem a primeira fase depois da importação é recusada como por declarar")]
    public void SemPrimeiraFase_PorDeclarar() =>
        Conferir(Importado, LeiauteDeTeste.Declarar(null, "CHAMADA_REGULAR")).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "importacao.primeiraFaseDepoisDaImportacao",
            Error = new { Code = ImportacaoDoResultadoErrorCodes.PrimeiraFasePorDeclarar },
        });

    [Fact(DisplayName = "A primeira fase que o cronograma não tem é recusada")]
    public void PrimeiraFaseForaDoCronograma_Recusa() =>
        Conferir(Importado, LeiauteDeTeste.Declarar(Guid.CreateVersion7(), "CHAMADA_REGULAR")).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "importacao.primeiraFaseDepoisDaImportacao",
            Error = new { Code = ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma },
        });

    [Fact(DisplayName = "As recusas dos relatórios e da primeira fase se acumulam")]
    public void RecusasIndependentes_SeAcumulam() =>
        Conferir(Importado, LeiauteDeTeste.Declarar(null)).Select(static recusa => recusa.Error.Code).Should().Equal(
            ImportacaoDoResultadoErrorCodes.SemRelatorio, ImportacaoDoResultadoErrorCodes.PrimeiraFasePorDeclarar);

    [Fact(DisplayName = "Na gravação, a declaração em processo com inscrição pelo sistema é recusada")]
    public void Gravacao_InscricaoPeloSistema_Recusa() =>
        ConferenciaDaImportacaoDoResultado.RecusasDaGravacao(PeloSistema, _habilitacao.Id, [_habilitacao])
            .Should().ContainSingle().Which.Error.Code.Should().Be(ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema);

    [Fact(DisplayName = "Na gravação, a primeira fase fora do cronograma é recusada")]
    public void Gravacao_FaseForaDoCronograma_Recusa() =>
        ConferenciaDaImportacaoDoResultado.RecusasDaGravacao(Importado, Guid.CreateVersion7(), [_habilitacao])
            .Should().ContainSingle().Which.Error.Code.Should().Be(ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma);

    [Fact(DisplayName = "Na gravação, a declaração sem relatório e sem primeira fase por declarar não é recusada: só a publicação a recusa")]
    public void Gravacao_SemRelatorioESemFase_Aceita() =>
        ConferenciaDaImportacaoDoResultado.RecusasDaGravacao(Importado, null, [_habilitacao]).Should().BeEmpty();
}
