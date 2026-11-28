namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado.ProcessoDaImportacaoDeTeste;

/// <summary>A gravação e a ausência da declaração da importação do resultado no processo.</summary>
public sealed class DeclaracaoDaImportacaoNoProcessoTests
{
    [Fact(DisplayName = "O processo de resultado importado sem declaração é recusado como por declarar, e só por isso")]
    public void ImportadoSemDeclaracao_PorDeclarar()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        processo.DefinirImportacaoDoResultado([], null).IsSuccess.Should().BeTrue();

        CodigosDaImportacao(processo).Should().Equal(ImportacaoDoResultadoErrorCodes.PorDeclarar);
    }

    [Fact(DisplayName = "Lista vazia e primeira fase nula voltam a declaração a por declarar")]
    public void ListaVaziaEFaseNula_VoltaAPorDeclarar()
    {
        ProcessoSeletivo processo = ImportadoConforme();

        processo.DefinirImportacaoDoResultado([], null).IsSuccess.Should().BeTrue();

        processo.ImportacaoDoResultado.Should().BeNull();
    }

    [Fact(DisplayName = "A declaração gravada guarda os relatórios e a primeira fase informados")]
    public void DeclaracaoValida_Grava()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        Guid fase = PrimeiraFase(processo);

        processo.DefinirImportacaoDoResultado([new("CHAMADA_REGULAR", "v1"), new("LISTA_DE_ESPERA", "v2")], fase).IsSuccess.Should().BeTrue();

        processo.ImportacaoDoResultado!.Relatorios.Select(static relatorio => relatorio.LeiauteCodigo).Should().Equal("CHAMADA_REGULAR", "LISTA_DE_ESPERA");
        processo.ImportacaoDoResultado.PrimeiraFaseDepoisDaImportacaoId.Should().Be(fase);
    }

    [Fact(DisplayName = "O relatório de forma inválida e a primeira fase fora do cronograma são recusados juntos na gravação, nessa ordem")]
    public void RelatorioInvalidoEFaseForaDoCronograma_SomamAsRecusas()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        DeclaracaoDaImportacaoDoResultado? antes = processo.ImportacaoDoResultado;

        Result gravado = processo.DefinirImportacaoDoResultado([new("codigo inválido", "v1")], Guid.CreateVersion7());

        gravado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("importacao.relatorios[0].leiauteCodigo", ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute),
            ("importacao.primeiraFaseDepoisDaImportacao", ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma));
        processo.ImportacaoDoResultado.Should().Be(antes);
    }

    [Fact(DisplayName = "A lista de relatórios ausente e a primeira fase fora do cronograma são recusadas juntas na gravação, nessa ordem")]
    public void RelatoriosAusentesEFaseForaDoCronograma_SomamAsRecusas()
    {
        ProcessoSeletivo processo = ImportadoConforme();

        Result gravado = processo.DefinirImportacaoDoResultado(null, Guid.CreateVersion7());

        gravado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("importacao.relatorios", CodigosDeValidacao.Obrigatorio),
            ("importacao.primeiraFaseDepoisDaImportacao", ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma));
    }

    [Fact(DisplayName = "A importação declarada em processo com inscrição pelo sistema é recusada na gravação e nada é gravado")]
    public void InscricaoPeloSistema_RecusaNaGravacao()
    {
        ProcessoSeletivo processo = InscricaoPeloSistema();

        Result gravado = processo.DefinirImportacaoDoResultado(UmRelatorio, null);

        gravado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema);
        processo.ImportacaoDoResultado.Should().BeNull();
    }

    [Fact(DisplayName = "A origem incompatível vem primeiro: inscrição pelo sistema com relatório inválido só recebe a recusa de origem")]
    public void InscricaoPeloSistemaComRelatorioInvalido_SoARecusaDeOrigem()
    {
        ProcessoSeletivo processo = InscricaoPeloSistema();

        Result gravado = processo.DefinirImportacaoDoResultado([new("MEU LEIAUTE", null), new("MEU LEIAUTE", "v1")], Guid.CreateVersion7());

        gravado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema);
        processo.ImportacaoDoResultado.Should().BeNull();
    }

    [Fact(DisplayName = "A primeira fase fora do cronograma é recusada na gravação")]
    public void FaseForaDoCronograma_RecusaNaGravacao()
    {
        ProcessoSeletivo processo = ImportadoConforme();

        Result gravado = processo.DefinirImportacaoDoResultado(UmRelatorio, Guid.CreateVersion7());

        gravado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma);
    }

    [Fact(DisplayName = "O relatório sem versão de leiaute e o repetido são recusados juntos, cada um no campo da posição")]
    public void RelatoriosMalFormados_AcumulamRecusas()
    {
        ProcessoSeletivo processo = ImportadoConforme();

        Result gravado = processo.DefinirImportacaoDoResultado([new("A", null), new("B", "v1"), new("B", "v2")], null);

        gravado.Errors.Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("importacao.relatorios[0].leiauteVersao", ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute),
            ("importacao.relatorios[2].leiauteCodigo", ImportacaoDoResultadoErrorCodes.RelatorioRepetido));
    }
}
