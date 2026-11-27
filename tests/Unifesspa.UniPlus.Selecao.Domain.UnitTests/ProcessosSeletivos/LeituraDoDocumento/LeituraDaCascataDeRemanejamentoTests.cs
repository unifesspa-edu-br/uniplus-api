namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.VagasDoDocumentoDeTeste;

public sealed class LeituraDaCascataDeRemanejamentoTests
{
    private const string SemCadastro = "COTA_SEM_CADASTRO";

    private static CascataLida Ler(CascataNoDocumento? cascata) => LeituraDaCascataDeRemanejamento.Ler(cascata, Cadastros());

    [Fact(DisplayName = "O bloco ausente não tem cascata nem recusa")]
    public void BlocoAusente_NaoTemCascataNemRecusa()
    {
        CascataLida lida = Ler(null);

        lida.Cascata.Should().BeNull();
        lida.Recusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A cascata conforme com a regra vira a configuração, e a ordem de cada destino é a posição dele entre os da mesma origem")]
    public void CascataConforme_ViraAConfiguracaoComAOrdemPelaPosicao()
    {
        CascataLida lida = Ler(Cascata(DestinosConformes()));

        lida.Recusadas.Should().BeEmpty();
        lida.Cascata!.Destinos.Select(static destino => (destino.ModalidadeOrigemCodigo, destino.Ordem, destino.ModalidadeDestinoCodigo))
            .Should().Equal((CotaA, 1, CotaB), (CotaA, 2, Ampla), (CotaB, 1, Ampla));
    }

    [Fact(DisplayName = "A cascata mantém a ordem enviada dos destinos, e a origem guarda a posição enviada de cada um")]
    public void CascataMantemAOrdemEnviada()
    {
        DestinoDaCascataNoDocumento[] foraDeOrdem = [Destino(5, CotaB, Ampla), Destino(2, CotaA, CotaB), Destino(3, CotaA, Ampla)];

        CascataLida lida = Ler(Cascata(foraDeOrdem));

        lida.Cascata!.Destinos.Select(static destino => (destino.ModalidadeOrigemCodigo, destino.Ordem)).Should().Equal((CotaA, 1), (CotaA, 2), (CotaB, 1));
        lida.Origem().PosicaoDeOrigem(LeituraDaCascataDeRemanejamento.Lista, 0).Should().Be(2);
        lida.Origem().PosicaoDeOrigem(LeituraDaCascataDeRemanejamento.Lista, 2).Should().Be(5);
    }

    [Theory(DisplayName = "A regra que o catálogo não tem, ou que é de outro tipo, recusa a cascata no campo da regra")]
    [InlineData(false, "ConfiguracaoCascataRemanejamento.RegraNaoEncontrada")]
    [InlineData(true, "ConfiguracaoCascataRemanejamento.RegraTipoInvalido")]
    public void Regra_ForaDoCatalogoOuDeOutroTipo_Recusa(bool deOutroTipo, string codigo)
    {
        CascataNoDocumento cascata = Cascata(DestinosConformes()) with { Regra = deOutroTipo ? Institucional : new(TextoDigitado, "v1") };

        CascataLida lida = Ler(cascata);

        lida.Cascata.Should().BeNull();
        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento/regra/codigo", codigo));
        lida.Recusadas.Single().Causa.Error.Message.Should().NotContain(TextoDigitado);
    }

    [Fact(DisplayName = "A cascata que diverge da declaração da regra é recusada no bloco")]
    public void MatrizDivergente_RecusaNoBloco()
    {
        CascataLida lida = Ler(Cascata(DestinosConformes()) with { DestinoFinalCodigo = CotaB });

        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento", "ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra"));
    }

    [Fact(DisplayName = "O passo com código inválido aponta a posição enviada dele, e a regra não é conferida")]
    public void PassoInvalido_ApontaAPosicaoEnviada()
    {
        CascataLida lida = Ler(Cascata(Destino(0, CotaA, CotaB), Destino(4, CotaA, "inválido"), Destino(6, CotaB, Ampla)));

        lida.Cascata.Should().BeNull();
        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento/destinos/4/modalidadeDestinoCodigo", "ConfiguracaoCascataRemanejamento.CodigoInvalido"));
        lida.Recusadas.Single().PosicaoDeOrigem.Should().Be(4);
    }

    [Fact(DisplayName = "As recusas de forma voltam juntas: o destino final inválido, o passo inválido e o passo que remaneja para a própria origem")]
    public void RecusasDeForma_Acumulam()
    {
        CascataNoDocumento cascata = Cascata(Destino(0, CotaA, CotaA), Destino(1, CotaB, "inválido")) with { DestinoFinalCodigo = null };

        Causas(Ler(cascata).Recusadas).Should().Equal(
            ("/ordemDeRemanejamento/destinoFinalCodigo", "ConfiguracaoCascataRemanejamento.FallbackObrigatorio"),
            ("/ordemDeRemanejamento/destinos/0/modalidadeDestinoCodigo", "ConfiguracaoCascataRemanejamento.OrigemIgualAoDestino"),
            ("/ordemDeRemanejamento/destinos/1/modalidadeDestinoCodigo", "ConfiguracaoCascataRemanejamento.CodigoInvalido"));
    }

    [Fact(DisplayName = "A cascata sem destinos é recusada na lista de destinos")]
    public void SemDestinos_RecusaNaLista()
    {
        Causas(Ler(Cascata()).Recusadas).Should().Equal(("/ordemDeRemanejamento/destinos", "ConfiguracaoCascataRemanejamento.SemDestinos"));
    }

    [Fact(DisplayName = "O destino repetido na mesma origem é recusado na lista de destinos")]
    public void DestinoRepetido_RecusaNaLista()
    {
        CascataLida lida = Ler(Cascata(Destino(0, CotaA, CotaB), Destino(1, CotaA, CotaB)));

        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento/destinos", "ConfiguracaoCascataRemanejamento.DestinoDuplicado"));
    }

    [Fact(DisplayName = "O destino que o cadastro de modalidades não tem é recusado na posição enviada dele, sem repetir o código")]
    public void DestinoSemCadastro_RecusaNaPosicaoEnviada()
    {
        CascataLida lida = Ler(Cascata(Destino(0, CotaA, CotaB), Destino(4, CotaA, SemCadastro), Destino(6, CotaB, Ampla)));

        lida.Cascata.Should().BeNull();
        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento/destinos/4/modalidadeDestinoCodigo", "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada"));
        lida.Recusadas.Single().Causa.Error.Message.Should().NotContain(SemCadastro);
    }

    [Fact(DisplayName = "A origem que o cadastro de modalidades não tem é recusada na posição enviada dela")]
    public void OrigemSemCadastro_RecusaNaPosicaoEnviada()
    {
        CascataLida lida = Ler(Cascata(Destino(0, CotaA, CotaB), Destino(3, SemCadastro, Ampla)));

        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento/destinos/3/modalidadeOrigemCodigo", "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada"));
    }

    [Fact(DisplayName = "O destino final que o cadastro de modalidades não tem é recusado no campo do destino final")]
    public void DestinoFinalSemCadastro_RecusaNoCampoDoDestinoFinal()
    {
        CascataLida lida = Ler(Cascata(DestinosConformes()) with { DestinoFinalCodigo = SemCadastro });

        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento/destinoFinalCodigo", "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada"));
        lida.Recusadas.Single().Causa.Error.Message.Should().NotContain(SemCadastro);
    }

    [Fact(DisplayName = "O código inválido não ganha uma segunda causa de modalidade sem cadastro")]
    public void CodigoInvalido_NaoGanhaCausaDeCadastro()
    {
        CascataLida lida = Ler(Cascata(Destino(0, CotaA, "inválido")));

        Causas(lida.Recusadas).Should().Equal(("/ordemDeRemanejamento/destinos/0/modalidadeDestinoCodigo", "ConfiguracaoCascataRemanejamento.CodigoInvalido"));
    }

    [Fact(DisplayName = "O passo que remaneja para a própria origem não impede a recusa da regra que o catálogo não tem")]
    public void PassoParaAPropriaOrigem_AcumulaARecusaDaRegra()
    {
        CascataNoDocumento cascata = Cascata(Destino(0, CotaA, CotaA)) with { Regra = new(TextoDigitado, "v1") };

        Causas(Ler(cascata).Recusadas).Should().Equal(
            ("/ordemDeRemanejamento/destinos/0/modalidadeDestinoCodigo", "ConfiguracaoCascataRemanejamento.OrigemIgualAoDestino"),
            ("/ordemDeRemanejamento/regra/codigo", "ConfiguracaoCascataRemanejamento.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "A forma recusada, a regra de outro tipo e a modalidade sem cadastro voltam juntas, e a matriz não é conferida")]
    public void FormaRegraEModalidade_AcumulamSemConferirAMatriz()
    {
        CascataNoDocumento cascata = Cascata(Destino(0, CotaA, SemCadastro), Destino(1, CotaA, SemCadastro)) with { Regra = Institucional };

        Causas(Ler(cascata).Recusadas).Should().Equal(
            ("/ordemDeRemanejamento/destinos", "ConfiguracaoCascataRemanejamento.DestinoDuplicado"),
            ("/ordemDeRemanejamento/regra/codigo", "ConfiguracaoCascataRemanejamento.RegraTipoInvalido"),
            ("/ordemDeRemanejamento/destinos/0/modalidadeDestinoCodigo", "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada"),
            ("/ordemDeRemanejamento/destinos/1/modalidadeDestinoCodigo", "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada"));
    }
}
