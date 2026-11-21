namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

public sealed class ReconciliacaoDasBancasTests
{
    private const string CodigoComposto = "BANCA_HETEROIDENTIFICAÇÃO";
    private static readonly string CodigoDecomposto = CodigoComposto.Normalize(System.Text.NormalizationForm.FormD);

    private static BancaDaEtapa Banca(string codigo, Guid? substitui = null, Guid? tipo = null, string nome = "Banca") =>
        BancaDaEtapa.Criar(tipo ?? Guid.CreateVersion7(), codigo, nome, idDaBancaQueSubstitui: substitui);

    private static BancaRequerida BancaDaFase(string codigo, Guid? substitui = null, params string[] categorias) =>
        BancaRequerida.Criar(
            Guid.CreateVersion7(),
            codigo,
            "Banca",
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))],
            idDaBancaQueSubstitui: substitui);

    private static List<BancaDaEtapa> Gravadas(params BancaDaEtapa[] bancas)
    {
        ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>([], bancas);

        return [.. bancas];
    }

    [Fact(DisplayName = "A banca declarada com o id da banca gravada a mantém e leva a ela a origem, o nome e o tipo declarados")]
    public void Reconciliar_ComOIdDaGravada_MantemALinhaEAssumeADeclaracao()
    {
        BancaDaEtapa gravada = Gravadas(Banca("BANCA_A"))[0];
        Guid outraOrigem = Guid.CreateVersion7();

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [Banca("BANCA_B", gravada.Id, outraOrigem, "Novo")]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravada);
        gravada.Codigo.Should().Be("BANCA_B");
        gravada.TipoBancaOrigemId.Should().Be(outraOrigem);
        gravada.Nome.Should().Be("Novo");
        gravada.PosicaoNoTipo.Should().Be(1);
    }

    [Fact(DisplayName = "A banca declarada sem id é banca nova, ainda que haja banca gravada do mesmo tipo")]
    public void Reconciliar_SemId_CriaABancaNova()
    {
        BancaDaEtapa gravada = Gravadas(Banca("BANCA_A"))[0];
        BancaDaEtapa declarada = Banca("BANCA_A");

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [declarada]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(declarada);
        declarada.Id.Should().NotBe(gravada.Id);
    }

    [Fact(DisplayName = "O id que não é de banca do lugar não reaproveita banca nenhuma e a banca é criada de novo")]
    public void Reconciliar_IdDeOutroLugar_CriaABancaNova()
    {
        BancaDaEtapa gravada = Gravadas(Banca("BANCA_A"))[0];
        Guid deOutroLugar = Guid.CreateVersion7();
        BancaDaEtapa declarada = Banca("BANCA_A", deOutroLugar);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [declarada]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(declarada);
        declarada.Id.Should().NotBe(deOutroLugar);
    }

    [Fact(DisplayName = "A banca gravada que ninguém declara sai do lugar")]
    public void Reconciliar_GravadaNaoDeclarada_Sai()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca("BANCA_A"), Banca("BANCA_B"));

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(gravadas, [Banca("BANCA_B", gravadas[1].Id)]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravadas[1]);
    }

    [Fact(DisplayName = "Reordenar bancas do mesmo tipo mantém os ids e troca as posições")]
    public void Reconciliar_Reordenar_MantemOsIdsETrocaAsPosicoes()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca("BANCA_A"), Banca("BANCA_A"), Banca("BANCA_A"));
        gravadas.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2, 3);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(
            gravadas,
            [Banca("BANCA_A", gravadas[2].Id), Banca("BANCA_A", gravadas[0].Id), Banca("BANCA_A", gravadas[1].Id)]);

        finais.Should().Equal(gravadas[2], gravadas[0], gravadas[1]);
        gravadas[2].PosicaoNoTipo.Should().Be(1);
        gravadas[0].PosicaoNoTipo.Should().Be(2);
        gravadas[1].PosicaoNoTipo.Should().Be(3);
    }

    [Fact(DisplayName = "Retirar a banca do meio renumera as seguintes sem trocar o id de nenhuma")]
    public void Reconciliar_RetirarDoMeio_RenumeraASeguinte()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca("BANCA_A"), Banca("BANCA_A"), Banca("BANCA_A"));
        Guid[] ids = [.. gravadas.Select(static b => b.Id)];

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(
            gravadas, [Banca("BANCA_A", ids[0]), Banca("BANCA_A", ids[2])]);

        finais.Select(static b => b.Id).Should().Equal(ids[0], ids[2]);
        finais.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "Acrescentar uma banca nova no fim mantém as gravadas e dá a ela a posição seguinte")]
    public void Reconciliar_AcrescentarNoFim_DaAPosicaoSeguinte()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca("BANCA_A"));
        BancaDaEtapa nova = Banca("BANCA_A");

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(gravadas, [Banca("BANCA_A", gravadas[0].Id), nova]);

        finais[0].Should().BeSameAs(gravadas[0]);
        finais[1].Should().BeSameAs(nova);
        finais.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "Duas bancas declaradas com o mesmo id consomem a banca gravada uma vez só e a segunda é criada de novo")]
    public void Reconciliar_MesmoIdDuasVezes_ConsomeAGravadaUmaVez()
    {
        BancaDaEtapa gravada = Gravadas(Banca("BANCA_A"))[0];
        BancaDaEtapa segunda = Banca("BANCA_A", gravada.Id);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [Banca("BANCA_A", gravada.Id), segunda]);

        finais[0].Should().BeSameAs(gravada);
        finais[1].Should().BeSameAs(segunda);
    }

    [Fact(DisplayName = "A posição conta dentro de cada tipo, na ordem em que as bancas chegam")]
    public void Reconciliar_VariosTipos_NumeraDentroDeCadaTipo()
    {
        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>(
            [], [Banca("BANCA_B"), Banca("BANCA_A"), Banca("BANCA_B"), Banca("BANCA_A"), Banca("BANCA_B")]);

        finais.Select(static b => (b.Codigo, b.PosicaoNoTipo)).Should().Equal(
            ("BANCA_B", 1), ("BANCA_A", 1), ("BANCA_B", 2), ("BANCA_A", 2), ("BANCA_B", 3));
    }

    [Fact(DisplayName = "O tipo escrito em formas de acento diferentes é o mesmo tipo para a posição")]
    public void Reconciliar_CodigoEmNfcDiferentes_ContaComoUmTipo()
    {
        CodigoComposto.IsNormalized(System.Text.NormalizationForm.FormC).Should().BeTrue();
        CodigoDecomposto.IsNormalized(System.Text.NormalizationForm.FormC).Should().BeFalse();

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>([], [Banca(CodigoComposto), Banca(CodigoDecomposto)]);

        finais.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2);
    }

    private static BancaRequerida DeclaradaComRendaEPpi(Guid idDaGravada, Guid origemDaRenda) =>
        BancaRequerida.Criar(
            Guid.CreateVersion7(),
            "BANCA_ANALISE",
            "Banca",
            [CategoriaJulgada.Criar(origemDaRenda, "RENDA"), CategoriaJulgada.Criar(Guid.CreateVersion7(), "PPI")],
            idDaBancaQueSubstitui: idDaGravada);

    [Fact(DisplayName = "Na fase, a banca mantida continua com a linha da categoria de mesmo código e troca só o que mudou")]
    public void Reconciliar_BancaDaFase_MantemALinhaDaCategoriaDeMesmoCodigo()
    {
        BancaRequerida gravada = BancaDaFase("BANCA_ANALISE", null, "RENDA", "PCD");
        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [gravada]);
        CategoriaJulgada renda = gravada.CandidatosAtendidos.Single(static c => c.Codigo == "RENDA");
        Guid novaOrigemDaRenda = Guid.CreateVersion7();
        BancaRequerida declarada = DeclaradaComRendaEPpi(gravada.Id, novaOrigemDaRenda);

        List<BancaRequerida> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [declarada]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravada);
        gravada.CandidatosAtendidos.Select(static c => c.Codigo).Should().Equal("RENDA", "PPI");
        gravada.CandidatosAtendidos.First().Should().BeSameAs(renda);
        renda.CategoriaDocumentoOrigemId.Should().Be(novaOrigemDaRenda);
        gravada.CandidatosAtendidos.Should().OnlyContain(c => c.BancaRequeridaId == gravada.Id);
    }

    [Fact(DisplayName = "Na fase, reordenar bancas do mesmo tipo mantém os ids e troca as posições")]
    public void Reconciliar_BancasDaFase_ReordenaTrocandoAsPosicoes()
    {
        BancaRequerida primeira = BancaDaFase("BANCA_ANALISE", null, "RENDA");
        BancaRequerida segunda = BancaDaFase("BANCA_ANALISE", null, "PCD");
        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [primeira, segunda]);

        List<BancaRequerida> finais = ReconciliacaoDasBancas.Reconciliar(
            [primeira, segunda],
            [BancaDaFase("BANCA_ANALISE", segunda.Id, "PCD"), BancaDaFase("BANCA_ANALISE", primeira.Id, "RENDA")]);

        finais.Should().Equal(segunda, primeira);
        segunda.PosicaoNoTipo.Should().Be(1);
        primeira.PosicaoNoTipo.Should().Be(2);
    }

    [Fact(DisplayName = "A banca da etapa reconstruída dos dados salvos substitui a gravada de mesmo id e a mantém")]
    public void Reconciliar_BancaDaEtapaReconstruida_SubstituiAGravadaDeMesmoId()
    {
        BancaDaEtapa gravada = Gravadas(Banca("BANCA_A"))[0];
        BancaDaEtapa reconstruida = BancaDaEtapa.ReconstruirDosDadosSalvos(gravada.Id, gravada.TipoBancaOrigemId, "BANCA_A", "Banca");

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [reconstruida]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravada);
    }

    [Fact(DisplayName = "A banca da fase reconstruída dos dados salvos substitui a gravada de mesmo id e a mantém")]
    public void Reconciliar_BancaDaFaseReconstruida_SubstituiAGravadaDeMesmoId()
    {
        BancaRequerida gravada = BancaDaFase("BANCA_ANALISE", null, "RENDA");
        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [gravada]);
        BancaRequerida reconstruida = BancaRequerida.ReconstruirDosDadosSalvos(gravada.Id, gravada.TipoBancaOrigemId, "BANCA_ANALISE", "Banca", []);

        List<BancaRequerida> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [reconstruida]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravada);
    }
}
