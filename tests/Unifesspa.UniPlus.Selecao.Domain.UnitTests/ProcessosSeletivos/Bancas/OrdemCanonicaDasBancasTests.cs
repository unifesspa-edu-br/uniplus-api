namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;

/// <summary>
/// As bancas saem pelo código do tipo e pela posição dentro dele. O id da banca e a origem do tipo não decidem a ordem, nem como
/// desempate: o par código e posição é único no lugar.
/// </summary>
public sealed class OrdemCanonicaDasBancasTests
{
    private static readonly Guid IdMenor = new("00000000-0000-4000-8000-000000000001");
    private static readonly Guid IdMaior = new("00000000-0000-4000-8000-000000000002");

    private static BancaDaEtapa NovaBancaDaEtapa(string codigo) =>
        BancaDaEtapa.Criar(Guid.CreateVersion7(), codigo, "Banca");

    private static BancaRequerida NovaBancaDaFase(string codigo, Guid origem) =>
        BancaRequerida.Criar(origem, codigo, "Banca", []);

    [Fact(DisplayName = "As bancas da etapa saem pelo código do tipo e, dentro dele, pela posição")]
    public void BancasDaEtapa_CodigoEPosicao()
    {
        List<BancaDaEtapa> bancas = ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>(
            [], [NovaBancaDaEtapa("BANCA_B"), NovaBancaDaEtapa("BANCA_A"), NovaBancaDaEtapa("BANCA_B"), NovaBancaDaEtapa("BANCA_A")]);

        OrdemCanonicaDaConfiguracao.BancasDaEtapa(bancas.AsEnumerable().Reverse())
            .Select(static b => (b.Codigo, b.PosicaoNoTipo))
            .Should().Equal(("BANCA_A", 1), ("BANCA_A", 2), ("BANCA_B", 1), ("BANCA_B", 2));
    }

    [Fact(DisplayName = "O id da banca da etapa não decide a ordem: com o id em ordem inversa à da posição, a ordem segue a posição")]
    public void BancasDaEtapa_IdEmOrdemInversaAPosicao_SeguePosicao()
    {
        BancaDaEtapa idMenor = BancaDaEtapa.ReconstruirDosDadosSalvos(IdMenor, Guid.CreateVersion7(), "BANCA_A", "Banca");
        BancaDaEtapa idMaior = BancaDaEtapa.ReconstruirDosDadosSalvos(IdMaior, Guid.CreateVersion7(), "BANCA_A", "Banca");

        ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>([], [idMaior, idMenor]);

        OrdemCanonicaDaConfiguracao.BancasDaEtapa([idMenor, idMaior]).Should().Equal(idMaior, idMenor);
    }

    [Fact(DisplayName = "As bancas da fase saem pelo código do tipo, e não pela origem do tipo no cadastro")]
    public void BancasDaFase_OrigensEmOrdemInversaADosCodigos_SegueOCodigo()
    {
        Guid origemMenor = Guid.CreateVersion7();
        Guid origemMaior = Guid.CreateVersion7();
        BancaRequerida zeta = NovaBancaDaFase("BANCA_ZETA", origemMenor);
        BancaRequerida alfa = NovaBancaDaFase("BANCA_ALFA", origemMaior);
        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [zeta, alfa]);

        OrdemCanonicaDaConfiguracao.BancasDaFase([zeta, alfa]).Should().Equal(alfa, zeta);
    }

    [Fact(DisplayName = "O id da banca da fase não decide a ordem: com o id em ordem inversa à da posição, a ordem segue a posição")]
    public void BancasDaFase_IdEmOrdemInversaAPosicao_SeguePosicao()
    {
        BancaRequerida idMenor = BancaRequerida.ReconstruirDosDadosSalvos(IdMenor, Guid.CreateVersion7(), "BANCA_ANALISE", "Banca", []);
        BancaRequerida idMaior = BancaRequerida.ReconstruirDosDadosSalvos(IdMaior, Guid.CreateVersion7(), "BANCA_ANALISE", "Banca", []);

        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [idMaior, idMenor]);

        OrdemCanonicaDaConfiguracao.BancasDaFase([idMenor, idMaior]).Should().Equal(idMaior, idMenor);
    }

    [Fact(DisplayName = "Trocar as posições de duas bancas inverte a ordem delas sem trocar o id de nenhuma")]
    public void BancasDaEtapa_TrocaDePosicoes_InverteAOrdem()
    {
        BancaDaEtapa primeira = NovaBancaDaEtapa("BANCA_A");
        BancaDaEtapa segunda = NovaBancaDaEtapa("BANCA_A");
        List<BancaDaEtapa> gravadas = ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>([], [primeira, segunda]);
        Guid[] ids = [primeira.Id, segunda.Id];

        ReconciliacaoDasBancas.Reconciliar(
            gravadas,
            [BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_A", "Banca", idDaBancaQueSubstitui: segunda.Id),
             BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_A", "Banca", idDaBancaQueSubstitui: primeira.Id)]);

        OrdemCanonicaDaConfiguracao.BancasDaEtapa(gravadas).Select(static b => b.Id).Should().Equal(ids[1], ids[0]);
    }
}
