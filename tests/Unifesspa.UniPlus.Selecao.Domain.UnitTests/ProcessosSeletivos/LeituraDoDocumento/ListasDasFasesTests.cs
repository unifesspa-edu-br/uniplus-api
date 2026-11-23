namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>
/// A fase guarda só os itens que a leitura aceitou, em ordem canônica: a causa que aponta um item pela posição dele na lista da
/// declaração aponta, no documento, a posição em que o item foi enviado, mesmo com item recusado antes dele.
/// </summary>
public sealed partial class ListasDasFasesTests
{
    private const string TipoDeBanca = "ANALISE_RECURSOS";

    [GeneratedRegex(@"^fases\[\d+\]\.bancasRequeridas\[\d+\]$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CampoDaBanca();

    private static FaseNoDocumento ComBancas(FaseNoDocumento fase, params BancaNoDocumento[] bancas) => fase with { Bancas = bancas };

    private static string[] PonteirosDasBancasSemRegra(IdentificacaoECronogramaLidos lidos) =>
        [.. PonteiroNoDocumento.Converter(
                CausasQueImpedemCongelar.De(Configuracao(lidos), CadastrosDeTeste.Padrao).Where(static causa => causa.Field is not null && CampoDaBanca().IsMatch(causa.Field)),
                lidos.Origem)
            .Select(static causa => causa.Field!)];

    [Fact(DisplayName = "A banca do meio recusada desloca a posição canônica da seguinte, e a causa dela aponta a posição enviada")]
    public void BancaDoMeioRecusada_CausaPosteriorApontaAPosicaoEnviada()
    {
        FaseNoDocumento fase = ComBancas(
            Fase(0, "recurso", "RECURSO"),
            Banca(0, "banca-a", TipoDeBanca, []),
            Banca(1, "banca-b", "TIPO-DIGITADO", []),
            Banca(2, "banca-c", TipoDeBanca, []));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(fase));

        PonteirosDasBancasSemRegra(lidos).Should().BeEquivalentTo(
            "/cronograma/fases/0/bancasRequeridas/0", "/cronograma/fases/0/bancasRequeridas/2");
    }

    [Fact(DisplayName = "A fase anterior recusada desloca a posição canônica da fase, e a causa da banca aponta a fase enviada")]
    public void FaseAnteriorRecusada_CausaDaBancaApontaAFaseEnviada()
    {
        FaseNoDocumento recusada = Fase(0, "inexistente", "FASE-DIGITADA");
        FaseNoDocumento fase = ComBancas(Fase(1, "recurso", "RECURSO"), Banca(0, "banca-a", "TIPO-DIGITADO", []), Banca(1, "banca-b", TipoDeBanca, []));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(recusada, fase));

        PonteirosDasBancasSemRegra(lidos).Should().Equal("/cronograma/fases/1/bancasRequeridas/1");
    }

    [Fact(DisplayName = "O produto do meio recusado desloca a posição canônica do seguinte, que aponta a posição enviada")]
    public void ProdutoDoMeioRecusado_PosicaoCanonicaApontaAPosicaoEnviada()
    {
        FaseNoDocumento fase = Fase(0, "resultado", "RESULTADO")
            .Publicando(Preliminar(0, AtoDoResultado), Preliminar(1, "ATO-DIGITADO"), Definitivo(2, AtoDoDefinitivo));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(fase));

        // Na ordem canônica o definitivo vem antes do preliminar: a posição 0 da declaração é a 2 do documento, e a 1 é a 0.
        PonteiroNoDocumento.Converter("fases[0].produtos[0]", lidos.Origem).Should().Be("/cronograma/fases/0/publicacoesPrevistas/2");
        PonteiroNoDocumento.Converter("fases[0].produtos[1]", lidos.Origem).Should().Be("/cronograma/fases/0/publicacoesPrevistas/0");
    }

    [Fact(DisplayName = "A lista sem item aceito não registra origem, e a posição canônica é a enviada")]
    public void ListaSemItemAceito_FicaNaOrdemDoDocumento()
    {
        FaseNoDocumento fase = Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, "ATO-DIGITADO"));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(fase));

        PonteiroNoDocumento.Converter("fases[0].produtos[0]", lidos.Origem).Should().Be("/cronograma/fases/0/publicacoesPrevistas/0");
    }
}
