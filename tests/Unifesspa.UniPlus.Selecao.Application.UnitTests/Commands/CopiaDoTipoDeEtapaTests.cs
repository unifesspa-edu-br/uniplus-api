namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CopiaDoTipoDeEtapaTests
{
    [Fact(DisplayName = "A cópia leva a identidade e o que o tipo admite, como o cadastro tem agora")]
    public void De_TipoDoCadastro_CopiaIdentidadeEAvaliacao()
    {
        TipoEtapaView tipo = TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "REDACAO").Chamado("Redação")
            .ComCarater(admitePontuacao: true, admiteEliminacao: false).ComCorrecaoIndependente().Construir();

        Result<TipoEtapaSnapshot> copia = CopiaDoTipoDeEtapa.De(tipo);

        copia.IsSuccess.Should().BeTrue();
        TipoEtapaSnapshot snapshot = copia.Value!;
        snapshot.OrigemId.Should().Be(tipo.Id);
        snapshot.Codigo.Should().Be("REDACAO");
        snapshot.Nome.Should().Be("Redação");
        snapshot.AdmitePontuacao.Should().BeTrue();
        snapshot.AdmiteEliminacao.Should().BeFalse();
        snapshot.NotaDeOrigemNoEnem.Should().BeFalse();
        snapshot.ExigeCorrecaoIndependente.Should().BeTrue();
    }

    [Fact(DisplayName = "A cópia do tipo com nota do ENEM guarda a origem da nota")]
    public void De_TipoComNotaDoEnem_GuardaAOrigemDaNota()
    {
        TipoEtapaView tipo = TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "NOTA_ENEM").ComNotaDoEnem().Construir();

        Result<TipoEtapaSnapshot> copia = CopiaDoTipoDeEtapa.De(tipo);

        copia.Value!.NotaDeOrigemNoEnem.Should().BeTrue();
    }

    [Fact(DisplayName = "O tipo que não admite pontuar nem eliminar é recusado pelo domínio")]
    public void De_TipoSemCaraterAdmitido_DevolveARecusaDoDominio()
    {
        TipoEtapaView tipo = TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "SEM_CARATER")
            .ComCarater(admitePontuacao: false, admiteEliminacao: false).Construir();

        Result<TipoEtapaSnapshot> copia = CopiaDoTipoDeEtapa.De(tipo);

        copia.IsFailure.Should().BeTrue();
        copia.Error!.Code.Should().Be("TipoEtapaSnapshot.SemCaraterAdmitido");
    }
}
