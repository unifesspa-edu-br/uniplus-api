namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public class VinculoDiscenteTests
{
    [Fact(DisplayName = "VinculoDiscentes.Criar - Deve Gerar Uuid Valido e Instanciar Entidade")]
    public void Criar_DeveGerarUuidValidoEInstanciarEntidade()
    {
        VinculoDiscente discente = VinculoDiscente.Criar(VinculoDeTeste.Snapshot());

        Assert.NotEqual(Guid.Empty, discente.Id);
        Assert.Equal(50231, discente.Snapshot.Identificacao.IdDiscenteSigaa);
        Assert.Equal("20142600001", discente.Snapshot.Identificacao.Matricula);
    }

    [Fact(DisplayName = "VinculoDiscentes.Reidratar - Deve Manter Guid Original sem Gerar novo ID")]
    public void Reidratar_DeveManterGuidOriginal_SemGerarNovoId()
    {
        Guid idOriginal = Guid.CreateVersion7();

        VinculoDiscente discente = VinculoDiscente.Reidratar(idOriginal, VinculoDeTeste.Snapshot());

        Assert.Equal(idOriginal, discente.Id);
    }

    [Fact(DisplayName = "VinculoDiscentes.ToString - Nao Deve Expor Dados Pessoais")]
    public void ToString_NaoDeveExporDadosPessoais()
    {
        VinculoDiscente discente = VinculoDiscente.Criar(VinculoDeTeste.Snapshot());

        string resultado = discente.ToString();

        Assert.DoesNotContain("ANA EXEMPLO DE SOUZA", resultado);
        Assert.DoesNotContain("20142600001", resultado);
        Assert.DoesNotContain(VinculoDeTeste.CpfValido, resultado);

        Assert.Contains("50231", resultado);
        Assert.Contains(discente.Id.ToString(), resultado);
    }

    [Fact(DisplayName = "VinculoDiscenteSnapshot.ToString - Nao Deve Expor Nome nem Matricula")]
    public void VinculoDiscenteSnapshotToString_NaoDeveExporNomeNemMatricula()
    {
        string resultado = VinculoDeTeste.Snapshot().ToString();

        Assert.DoesNotContain("ANA EXEMPLO DE SOUZA", resultado);
        Assert.DoesNotContain("20142600001", resultado);
        Assert.DoesNotContain(VinculoDeTeste.CpfValido, resultado);
    }

    [Fact(DisplayName = "IdentificacaoDoDiscente.ToString - Nao Deve Expor Nome, Matricula nem Cpf")]
    public void IdentificacaoDoDiscenteToString_NaoDeveExporDadosPessoais()
    {
        string resultado = VinculoDeTeste.Identificacao().ToString();

        Assert.DoesNotContain("ANA EXEMPLO DE SOUZA", resultado);
        Assert.DoesNotContain("20142600001", resultado);
        Assert.DoesNotContain(VinculoDeTeste.CpfValido, resultado);
        Assert.Contains("50231", resultado);
    }

    [Fact(DisplayName = "PeriodoIngresso.Criar - Deve Falhar Quando Periodo For Invalido")]
    public void PeriodoIngresso_Criar_DeveFalhar_QuandoPeriodoForInvalido()
    {
        Result<PeriodoIngresso> resultado = PeriodoIngresso.Criar(ano: 2014, periodo: 0);

        Assert.True(resultado.IsFailure);
    }
}
