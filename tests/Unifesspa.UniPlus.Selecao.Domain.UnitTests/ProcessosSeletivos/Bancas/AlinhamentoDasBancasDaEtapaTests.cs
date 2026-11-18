namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

public sealed class AlinhamentoDasBancasDaEtapaTests
{
    private const string CodigoComposto = "BANCA_HETEROIDENTIFICAÇÃO";
    private static readonly string CodigoDecomposto = CodigoComposto.Normalize(System.Text.NormalizationForm.FormD);

    private static BancaDaEtapa Banca(string codigo, Guid? tipo = null) =>
        BancaDaEtapa.Criar(tipo ?? Guid.CreateVersion7(), codigo, "Banca");

    [Fact(DisplayName = "O código repetido é reconhecido mesmo escrito em formas de acento diferentes")]
    public void CodigoRepetido_MesmoCodigoEmNfcDiferentes_Reconhece()
    {
        CodigoComposto.IsNormalized(System.Text.NormalizationForm.FormC).Should().BeTrue();
        CodigoDecomposto.IsNormalized(System.Text.NormalizationForm.FormC).Should().BeFalse();

        AlinhamentoDasBancasDaEtapa.CodigoRepetido([Banca(CodigoComposto), Banca(CodigoDecomposto)]).Should().BeTrue();
    }

    [Fact(DisplayName = "Códigos diferentes não são repetição")]
    public void CodigoRepetido_CodigosDiferentes_NaoReconhece()
    {
        AlinhamentoDasBancasDaEtapa.CodigoRepetido([Banca("BANCA_A"), Banca("BANCA_B")]).Should().BeFalse();
    }

    [Fact(DisplayName = "Sem bancas não há código repetido")]
    public void CodigoRepetido_SemBancas_NaoReconhece()
    {
        AlinhamentoDasBancasDaEtapa.CodigoRepetido([]).Should().BeFalse();
    }

    [Fact(DisplayName = "Reconciliar reaproveita a banca viva de código equivalente em NFC e repõe o tipo de origem")]
    public void Reconciliar_CodigoEquivalenteEmNfc_ReaproveitaALinhaERepoeAOrigem()
    {
        Guid tipoDeclarado = Guid.CreateVersion7();
        BancaDaEtapa viva = Banca(CodigoDecomposto);

        List<BancaDaEtapa> finais = AlinhamentoDasBancasDaEtapa.Reconciliar([viva], [Banca(CodigoComposto, tipoDeclarado)]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(viva);
        viva.TipoBancaOrigemId.Should().Be(tipoDeclarado);
    }

    [Fact(DisplayName = "Reconciliar adota a banca declarada que a etapa viva não tem")]
    public void Reconciliar_BancaSemLinhaViva_EntraComoDeclarada()
    {
        BancaDaEtapa declarada = Banca("BANCA_NOVA");

        List<BancaDaEtapa> finais = AlinhamentoDasBancasDaEtapa.Reconciliar([Banca("BANCA_VIVA")], [declarada]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(declarada);
    }

    [Fact(DisplayName = "Reconciliar segue a ordem das declaradas e descarta a banca viva que ninguém declarou")]
    public void Reconciliar_VariasBancas_SegueAOrdemDasDeclaradas()
    {
        BancaDaEtapa vivaA = Banca("BANCA_A");
        BancaDaEtapa vivaB = Banca("BANCA_B");
        BancaDaEtapa declaradaC = Banca("BANCA_C");

        List<BancaDaEtapa> finais = AlinhamentoDasBancasDaEtapa.Reconciliar(
            [vivaA, vivaB], [declaradaC, Banca("BANCA_B"), Banca("BANCA_A")]);

        finais.Should().HaveCount(3);
        finais[0].Should().BeSameAs(declaradaC);
        finais[1].Should().BeSameAs(vivaB);
        finais[2].Should().BeSameAs(vivaA);
    }

    [Fact(DisplayName = "Reconciliar consome cada banca viva uma vez")]
    public void Reconciliar_DuasDeclaradasDeMesmoCodigo_ConsomeAVivaUmaVez()
    {
        BancaDaEtapa viva = Banca("BANCA_A");
        BancaDaEtapa segunda = Banca("BANCA_A");

        List<BancaDaEtapa> finais = AlinhamentoDasBancasDaEtapa.Reconciliar([viva], [Banca("BANCA_A"), segunda]);

        finais[0].Should().BeSameAs(viva);
        finais[1].Should().BeSameAs(segunda);
    }
}
