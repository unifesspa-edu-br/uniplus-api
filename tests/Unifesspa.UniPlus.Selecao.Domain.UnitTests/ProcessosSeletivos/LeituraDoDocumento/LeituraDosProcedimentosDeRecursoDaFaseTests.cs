namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

public sealed class LeituraDosProcedimentosDeRecursoDaFaseTests
{
    private static readonly ReferenciaDeRegraNoDocumento Ancorada = new(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1");

    private static FaseNoDocumento ComRegra(ReferenciaDeRegraNoDocumento regra, params ProcedimentoNoDocumento[] procedimentos) =>
        Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, AtoDoResultado)) with
        {
            RegraDeRecurso = new(regra, AtoDoResultado, new ParametrosRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null), procedimentos),
        };

    [Fact(DisplayName = "O procedimento com regra desconhecida e item de avaliação como alvo acumula as duas recusas")]
    public void Procedimento_RegraEAlvoInvalidos_AcumulaRecusas()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, new AlvoNoDocumento("item-digitado", null), new("REGRA-DIGITADA", "v1"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimentos: procedimento)));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().BeEmpty();
        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal(
            "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra/codigo", "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/alvo");
    }

    [Fact(DisplayName = "A versão inexistente da regra do procedimento aponta o código citado")]
    public void Procedimento_ComVersaoInexistente_ApontaOCodigo()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new(CodigoDoProcedimento, "versao-inexistente"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimento)));

        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal(
            "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra/codigo");
    }

    [Theory(DisplayName = "A leitura aceita área do ENEM e recusa regra ou alvo inexistente")]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Procedimento_ReferenciaEAlvo_DefinemAdocao(bool regraInexistente, bool itemInexistente, bool aceito)
    {
        AlvoNoDocumento alvo = itemInexistente ? new("item-digitado", null) : new(null, "LINGUAGENS");
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, alvo, new(regraInexistente ? "REGRA-DIGITADA" : CodigoDoProcedimento, "v1"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimento)));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().HaveCount(aceito ? 1 : 0);
        lidos.Origem.PartesRecusadas.Should().HaveCount(aceito ? 0 : 1);
    }
}
