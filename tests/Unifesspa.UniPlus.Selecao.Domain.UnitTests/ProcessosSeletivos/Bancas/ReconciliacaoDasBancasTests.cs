namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ReconciliacaoDasBancasTests
{
    private static BancaDaEtapa Banca(TipoDeBanca tipo, Guid? substitui = null, Guid? origem = null, string nome = "Banca") =>
        BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.Nomeada(TiposDeBancaCopiadosDeTeste.De(tipo, origem), nome), idDaBancaQueSubstitui: substitui);

    private static BancaRequerida BancaDaFase(TipoDeBanca tipo, Guid? substitui = null, params string[] categorias) =>
        BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.De(tipo),
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
        BancaDaEtapa gravada = Gravadas(Banca(TipoDeBanca.AnaliseDocumental))[0];
        Guid outraOrigem = Guid.CreateVersion7();

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [Banca(TipoDeBanca.Entrevista, gravada.Id, outraOrigem, "Novo")]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravada);
        gravada.Tipo.Codigo.Should().Be(TipoDeBanca.Entrevista);
        gravada.Tipo.OrigemId.Should().Be(outraOrigem);
        gravada.Tipo.Nome.Should().Be("Novo");
        gravada.PosicaoNoTipo.Should().Be(1);
    }

    [Fact(DisplayName = "A banca declarada sem id é banca nova, ainda que haja banca gravada do mesmo tipo")]
    public void Reconciliar_SemId_CriaABancaNova()
    {
        BancaDaEtapa gravada = Gravadas(Banca(TipoDeBanca.AnaliseDocumental))[0];
        BancaDaEtapa declarada = Banca(TipoDeBanca.AnaliseDocumental);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [declarada]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(declarada);
        declarada.Id.Should().NotBe(gravada.Id);
    }

    [Fact(DisplayName = "O id que não é de banca do lugar não reaproveita banca nenhuma e a banca é criada de novo")]
    public void Reconciliar_IdDeOutroLugar_CriaABancaNova()
    {
        BancaDaEtapa gravada = Gravadas(Banca(TipoDeBanca.AnaliseDocumental))[0];
        Guid deOutroLugar = Guid.CreateVersion7();
        BancaDaEtapa declarada = Banca(TipoDeBanca.AnaliseDocumental, deOutroLugar);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [declarada]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(declarada);
        declarada.Id.Should().NotBe(deOutroLugar);
    }

    [Fact(DisplayName = "A banca gravada que ninguém declara sai do lugar")]
    public void Reconciliar_GravadaNaoDeclarada_Sai()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.Entrevista));

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(gravadas, [Banca(TipoDeBanca.Entrevista, gravadas[1].Id)]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravadas[1]);
    }

    [Fact(DisplayName = "Reordenar bancas do mesmo tipo mantém os ids e troca as posições")]
    public void Reconciliar_Reordenar_MantemOsIdsETrocaAsPosicoes()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.AnaliseDocumental));
        gravadas.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2, 3);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(
            gravadas,
            [Banca(TipoDeBanca.AnaliseDocumental, gravadas[2].Id), Banca(TipoDeBanca.AnaliseDocumental, gravadas[0].Id), Banca(TipoDeBanca.AnaliseDocumental, gravadas[1].Id)]);

        finais.Should().Equal(gravadas[2], gravadas[0], gravadas[1]);
        gravadas[2].PosicaoNoTipo.Should().Be(1);
        gravadas[0].PosicaoNoTipo.Should().Be(2);
        gravadas[1].PosicaoNoTipo.Should().Be(3);
    }

    [Fact(DisplayName = "Retirar a banca do meio renumera as seguintes sem trocar o id de nenhuma")]
    public void Reconciliar_RetirarDoMeio_RenumeraASeguinte()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.AnaliseDocumental));
        Guid[] ids = [.. gravadas.Select(static b => b.Id)];

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(
            gravadas, [Banca(TipoDeBanca.AnaliseDocumental, ids[0]), Banca(TipoDeBanca.AnaliseDocumental, ids[2])]);

        finais.Select(static b => b.Id).Should().Equal(ids[0], ids[2]);
        finais.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "Acrescentar uma banca nova no fim mantém as gravadas e dá a ela a posição seguinte")]
    public void Reconciliar_AcrescentarNoFim_DaAPosicaoSeguinte()
    {
        List<BancaDaEtapa> gravadas = Gravadas(Banca(TipoDeBanca.AnaliseDocumental));
        BancaDaEtapa nova = Banca(TipoDeBanca.AnaliseDocumental);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar(gravadas, [Banca(TipoDeBanca.AnaliseDocumental, gravadas[0].Id), nova]);

        finais[0].Should().BeSameAs(gravadas[0]);
        finais[1].Should().BeSameAs(nova);
        finais.Select(static b => b.PosicaoNoTipo).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "Duas bancas declaradas com o mesmo id consomem a banca gravada uma vez só e a segunda é criada de novo")]
    public void Reconciliar_MesmoIdDuasVezes_ConsomeAGravadaUmaVez()
    {
        BancaDaEtapa gravada = Gravadas(Banca(TipoDeBanca.AnaliseDocumental))[0];
        BancaDaEtapa segunda = Banca(TipoDeBanca.AnaliseDocumental, gravada.Id);

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [Banca(TipoDeBanca.AnaliseDocumental, gravada.Id), segunda]);

        finais[0].Should().BeSameAs(gravada);
        finais[1].Should().BeSameAs(segunda);
    }

    [Fact(DisplayName = "A posição conta dentro de cada tipo, na ordem em que as bancas chegam")]
    public void Reconciliar_VariosTipos_NumeraDentroDeCadaTipo()
    {
        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>(
            [], [Banca(TipoDeBanca.Entrevista), Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.Entrevista), Banca(TipoDeBanca.AnaliseDocumental), Banca(TipoDeBanca.Entrevista)]);

        finais.Select(static b => (b.Tipo.Codigo, b.PosicaoNoTipo)).Should().Equal(
            (TipoDeBanca.Entrevista, 1), (TipoDeBanca.AnaliseDocumental, 1), (TipoDeBanca.Entrevista, 2), (TipoDeBanca.AnaliseDocumental, 2), (TipoDeBanca.Entrevista, 3));
    }

    private static BancaRequerida DeclaradaComRendaEPpi(Guid idDaGravada, Guid origemDaRenda) =>
        BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental),
            [CategoriaJulgada.Criar(origemDaRenda, "RENDA"), CategoriaJulgada.Criar(Guid.CreateVersion7(), "PPI")],
            idDaBancaQueSubstitui: idDaGravada);

    [Fact(DisplayName = "Na fase, a banca mantida continua com a linha da categoria de mesmo código e troca só o que mudou")]
    public void Reconciliar_BancaDaFase_MantemALinhaDaCategoriaDeMesmoCodigo()
    {
        BancaRequerida gravada = BancaDaFase(TipoDeBanca.AnaliseDocumental, null, "RENDA", "PCD");
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
        BancaRequerida primeira = BancaDaFase(TipoDeBanca.AnaliseDocumental, null, "RENDA");
        BancaRequerida segunda = BancaDaFase(TipoDeBanca.AnaliseDocumental, null, "PCD");
        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [primeira, segunda]);

        List<BancaRequerida> finais = ReconciliacaoDasBancas.Reconciliar(
            [primeira, segunda],
            [BancaDaFase(TipoDeBanca.AnaliseDocumental, segunda.Id, "PCD"), BancaDaFase(TipoDeBanca.AnaliseDocumental, primeira.Id, "RENDA")]);

        finais.Should().Equal(segunda, primeira);
        segunda.PosicaoNoTipo.Should().Be(1);
        primeira.PosicaoNoTipo.Should().Be(2);
    }

    [Fact(DisplayName = "A banca da etapa reconstruída dos dados salvos substitui a gravada de mesmo id e a mantém")]
    public void Reconciliar_BancaDaEtapaReconstruida_SubstituiAGravadaDeMesmoId()
    {
        BancaDaEtapa gravada = Gravadas(Banca(TipoDeBanca.AnaliseDocumental))[0];
        BancaDaEtapa reconstruida = BancaDaEtapa.ReconstruirDosDadosSalvos(gravada.Id, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, gravada.Tipo.OrigemId));

        List<BancaDaEtapa> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [reconstruida]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravada);
    }

    [Fact(DisplayName = "A banca da fase reconstruída dos dados salvos substitui a gravada de mesmo id e a mantém")]
    public void Reconciliar_BancaDaFaseReconstruida_SubstituiAGravadaDeMesmoId()
    {
        BancaRequerida gravada = BancaDaFase(TipoDeBanca.AnaliseDocumental, null, "RENDA");
        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [gravada]);
        BancaRequerida reconstruida = BancaRequerida.ReconstruirDosDadosSalvos(gravada.Id, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, gravada.Tipo.OrigemId), []);

        List<BancaRequerida> finais = ReconciliacaoDasBancas.Reconciliar([gravada], [reconstruida]);

        finais.Should().ContainSingle().Which.Should().BeSameAs(gravada);
    }
}
