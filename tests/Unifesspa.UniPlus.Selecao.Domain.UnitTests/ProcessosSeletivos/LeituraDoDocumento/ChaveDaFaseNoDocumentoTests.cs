namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

public sealed class ChaveDaFaseNoDocumentoTests
{
    [Fact]
    public void Ler_FaseDoDocumento_AdotaAChaveRecebida()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "fase_inscricao_2")));

        lidos.Cronograma.CronogramaFases.Single().Chave.Valor.Should().Be("fase_inscricao_2");
    }
}
