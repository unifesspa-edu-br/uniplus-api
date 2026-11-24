namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O lugar que a chave gravada no ato declara: só se reconhece a forma que a própria identidade escreve, e a chave que
/// não a tem é desconhecida, nunca uma falha.
/// </summary>
public sealed class LugarGravadoNaChaveTests
{
    private static readonly Guid Identidade = Guid.Parse("0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e90");

    [Fact(DisplayName = "A chave do item de uma fase devolve a fase canônica de origem dela")]
    public void ChaveDaFase_DevolveAFaseDeOrigem()
    {
        FaseCronograma fase = FaseDeTeste.Criar(1, "FASE");
        string chave = IdentidadeDoItemDoEdital.DaFase(fase, ProdutoDaFase.Criar("RESULTADO_FINAL", PapelDaPublicacaoPrevista.Definitivo)).Chave;

        (Guid? daFase, Guid? daEtapa) = IdentidadeDoItemDoEdital.LugarGravadoNaChave(chave)!.Value;

        daFase.Should().Be(fase.FaseCanonicaOrigemId);
        daEtapa.Should().BeNull();
    }

    [Fact(DisplayName = "A chave do item de uma etapa devolve a identidade da etapa")]
    public void ChaveDaEtapa_DevolveAEtapa()
    {
        EtapaProcesso etapa = ApoioDosDocumentosExigidosDoProcessoSeletivo.Etapa("Prova", "FASE", 1);
        string chave = IdentidadeDoItemDoEdital.DaEtapa(etapa, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)).Chave;

        (Guid? daFase, Guid? daEtapa) = IdentidadeDoItemDoEdital.LugarGravadoNaChave(chave)!.Value;

        daFase.Should().BeNull();
        daEtapa.Should().Be(etapa.Id);
    }

    [Theory(DisplayName = "A chave fora da forma que a identidade escreve não declara lugar")]
    [InlineData("grupo:0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e90|ATO|-")]
    [InlineData("FASE:0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e90|ATO|-")]
    [InlineData("0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e90|ATO|-")]
    [InlineData("fase:0197A3F2-5B8C-7D21-9C4E-3A6F8B1D2E90|ATO|-")]
    [InlineData("etapa:0197A3F2-5B8C-7D21-9C4E-3A6F8B1D2E90|ATO|-")]
    [InlineData("fase:0197a3f25b8c7d219c4e3a6f8b1d2e90|ATO|-")]
    [InlineData("fase:{0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e90}|ATO|-")]
    [InlineData("fase:0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e90")]
    [InlineData("fase:0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e90-ATO|-")]
    [InlineData("fase:0197a3f2-5b8c-7d21-9c4e-3a6f8b1d2e9|ATO|-")]
    [InlineData("fase:")]
    [InlineData("etapa:abc|")]
    [InlineData("")]
    public void ChaveForaDaForma_NaoDeclaraLugar(string chave) =>
        IdentidadeDoItemDoEdital.LugarGravadoNaChave(chave).Should().BeNull();

    [Fact(DisplayName = "A identidade vazia da fase é devolvida como vazia, e a da etapa também")]
    public void IdentidadeVazia_EDevolvidaComoVazia()
    {
        (Guid? daFase, Guid? daEtapaDaFase) = IdentidadeDoItemDoEdital.LugarGravadoNaChave($"fase:{Guid.Empty:D}|ATO|-")!.Value;
        (Guid? daFaseDaEtapa, Guid? daEtapa) = IdentidadeDoItemDoEdital.LugarGravadoNaChave($"etapa:{Guid.Empty:D}|ATO|-")!.Value;

        daFase.Should().Be(Guid.Empty);
        daEtapaDaFase.Should().BeNull();
        daFaseDaEtapa.Should().BeNull();
        daEtapa.Should().Be(Guid.Empty);
    }

    [Fact(DisplayName = "A chave com a identidade na forma que a identidade escreve, mesmo sem o resto, é reconhecida pelo lugar")]
    public void ChaveComIdentidadeESeparador_ReconheceOLugar()
    {
        (Guid? daFase, _) = IdentidadeDoItemDoEdital.LugarGravadoNaChave($"fase:{Identidade:D}|qualquer coisa")!.Value;

        daFase.Should().Be(Identidade);
    }

    [Fact(DisplayName = "Sem a chave a leitura não se faz")]
    public void ChaveNula_Lanca()
    {
        Func<(Guid?, Guid?)?> ler = () => IdentidadeDoItemDoEdital.LugarGravadoNaChave(null!);

        ler.Should().Throw<ArgumentNullException>();
    }
}
