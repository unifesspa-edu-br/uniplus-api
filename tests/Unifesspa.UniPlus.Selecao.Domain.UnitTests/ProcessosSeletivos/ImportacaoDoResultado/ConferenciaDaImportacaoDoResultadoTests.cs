namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// A publicação do processo de resultado importado exige a declaração da importação (UNI-REQ-0144): ao menos um
/// relatório de leiaute que o catálogo tem e a primeira fase depois da importação, que é fase do cronograma. O
/// processo com inscrição pelo sistema não declara importação.
/// </summary>
public sealed class ConferenciaDaImportacaoDoResultadoTests
{
    private static readonly LeiauteDoRelatorio[] Catalogo = [LeiauteDeTeste.Criar("CHAMADA_REGULAR"), LeiauteDeTeste.Criar("LISTA_DE_ESPERA")];

    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private static ProcessoDosFormularios Importado => new(InscricaoPeloSistema: false, CobraTaxa: false, PrimeiraFaseDepoisDaImportacaoId: null);

    private static ProcessoDosFormularios PeloSistema => new(InscricaoPeloSistema: true, CobraTaxa: false, PrimeiraFaseDepoisDaImportacaoId: null);

    private IReadOnlyList<FieldError> Conferir(ProcessoDosFormularios processo, DeclaracaoDaImportacaoDoResultado? declaracao) =>
        ConferenciaDaImportacaoDoResultado.Conferir(processo, declaracao, Catalogo, [_habilitacao]);

    [Fact(DisplayName = "A declaração com relatórios do catálogo e a primeira fase do cronograma é aceita")]
    public void DeclaracaoCompleta_Aceita() =>
        Conferir(Importado, LeiauteDeTeste.Declarar(_habilitacao.Id, "CHAMADA_REGULAR", "LISTA_DE_ESPERA")).Should().BeEmpty();

    [Fact(DisplayName = "O processo com inscrição pelo sistema sem declaração é aceito")]
    public void InscricaoPeloSistema_SemDeclaracao_Aceita() => Conferir(PeloSistema, null).Should().BeEmpty();

    [Theory(DisplayName = "O processo com inscrição pelo sistema que declara importação é recusado, mesmo com a lista vazia, e só por isso")]
    [InlineData(false)]
    [InlineData(true)]
    public void InscricaoPeloSistema_ComDeclaracao_Recusa(bool comRelatorio)
    {
        DeclaracaoDaImportacaoDoResultado declaracao = comRelatorio ? LeiauteDeTeste.Declarar(null, "NAO_EXISTE") : LeiauteDeTeste.Declarar(null);

        Conferir(PeloSistema, declaracao).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "importacao",
            Error = new { Code = ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema },
        });
    }

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

    [Fact(DisplayName = "O leiaute que o catálogo não tem é recusado no relatório pelo índice, e a versão também conta")]
    public void LeiauteDesconhecido_RecusaPeloIndice()
    {
        DeclaracaoDaImportacaoDoResultado declaracao = DeclaracaoDaImportacaoDoResultado.Declarar(
            [new("CHAMADA_REGULAR", "v1"), new("NAO_EXISTE", "v1"), new("LISTA_DE_ESPERA", "v9")], _habilitacao.Id).Value!;

        Conferir(Importado, declaracao).Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("importacao.relatorios[1]", ImportacaoDoResultadoErrorCodes.LeiauteDesconhecido),
            ("importacao.relatorios[2]", ImportacaoDoResultadoErrorCodes.LeiauteDesconhecido));
    }

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
        Conferir(Importado, LeiauteDeTeste.Declarar(null, "NAO_EXISTE")).Select(static recusa => recusa.Error.Code).Should().Equal(
            ImportacaoDoResultadoErrorCodes.LeiauteDesconhecido, ImportacaoDoResultadoErrorCodes.PrimeiraFasePorDeclarar);
}
