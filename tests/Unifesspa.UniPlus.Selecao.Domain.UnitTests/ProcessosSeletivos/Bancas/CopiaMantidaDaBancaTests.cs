namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A banca que a declaração substitui sem trocar o tipo mantém a cópia que já tinha: o tipo que mudou ou saiu do cadastro depois
/// não afeta a banca gravada, e só a banca nova ou a que troca de tipo copia o cadastro.
/// </summary>
public sealed class CopiaMantidaDaBancaTests
{
    private static readonly Guid Origem = Guid.CreateVersion7();

    private static BancaDaEtapa BancaDaEtapaGravada() =>
        BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.Nomeada(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista, Origem), "Nome de quando nasceu"));

    private static BancaRequerida BancaDaFaseGravada() =>
        BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.Nomeada(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista, Origem), "Nome de quando nasceu"), []);

    [Fact(DisplayName = "A banca da etapa que a declaração substitui, com o mesmo tipo, mantém a cópia que já tinha")]
    public void BancaDaEtapa_MesmoTipo_DevolveACopiaGravada()
    {
        BancaDaEtapa gravada = BancaDaEtapaGravada();

        CopiaMantidaDaBanca.De([gravada], gravada.Id, Origem).Should().BeSameAs(gravada.Tipo);
    }

    [Fact(DisplayName = "A banca da fase que a declaração substitui, com o mesmo tipo, mantém a cópia que já tinha")]
    public void BancaDaFase_MesmoTipo_DevolveACopiaGravada()
    {
        BancaRequerida gravada = BancaDaFaseGravada();

        CopiaMantidaDaBanca.De([gravada], gravada.Id, Origem).Should().BeSameAs(gravada.Tipo);
    }

    [Fact(DisplayName = "A banca que troca de tipo não mantém a cópia: o tipo novo é copiado do cadastro")]
    public void BancaQueTrocaDeTipo_NaoMantem()
    {
        BancaDaEtapa gravada = BancaDaEtapaGravada();

        CopiaMantidaDaBanca.De([gravada], gravada.Id, Guid.CreateVersion7()).Should().BeNull();
    }

    [Fact(DisplayName = "A banca nova, sem id de banca gravada, não mantém cópia")]
    public void BancaNova_SemId_NaoMantem()
    {
        CopiaMantidaDaBanca.De([BancaDaEtapaGravada()], null, Origem).Should().BeNull();
        CopiaMantidaDaBanca.De([BancaDaFaseGravada()], null, Origem).Should().BeNull();
    }

    [Fact(DisplayName = "O id que não é de banca gravada do lugar não mantém cópia")]
    public void IdQueNaoEDeBancaGravada_NaoMantem()
    {
        CopiaMantidaDaBanca.De([BancaDaEtapaGravada()], Guid.CreateVersion7(), Origem).Should().BeNull();
        CopiaMantidaDaBanca.De([BancaDaFaseGravada()], Guid.CreateVersion7(), Origem).Should().BeNull();
    }
}
