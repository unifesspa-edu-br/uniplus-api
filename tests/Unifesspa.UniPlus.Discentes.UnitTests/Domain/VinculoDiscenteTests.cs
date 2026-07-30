using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

public class VinculoDiscenteTests
{
    [Fact(DisplayName = "VinculoDiscentes.Criar - Deve Gerar Uuid Valido e Instanciar Entidade")]
    public void Criar_DeveGerarUuidValidoEInstanciarEntidade()
    {
        Cpf cpf = Cpf.Criar("33287151002").Value!;


        VinculoDiscente discente = VinculoDiscente.Criar(
            idDiscenteSigaa: 50231,
            matricula: "20142600001",
            cpf: cpf,
            nome: "ANA EXEMPLO",
            nivel: "G",
            cursoId: 20,
            cursoNome: "DIREITO",
            cursoCodigoEmec: "12078",
            cursoUnidadeId: 64,
            cursoUnidadeNome: "INSTITUTO DE DIREITO",
            situacaoId: 3,
            situacaoDescricao: "CONCLUÍDO",
            situacaoVinculo: null,
            anoIngresso: 2014,
            periodoIngresso: 2);

        // Assert
        Assert.NotEqual(Guid.Empty, discente.Id);
        Assert.Equal(50231, discente.IdDiscenteSigaa);
        Assert.Equal("20142600001", discente.Matricula);
    }

    [Fact(DisplayName = "VinculoDiscentes.Reidratar - Deve Manter Guid Original sem Gerar novo ID")]
    public void Reidratar_DeveManterGuidOriginal_SemGerarNovoId()
    {
        Guid idOriginal = Guid.NewGuid();
        Cpf cpf = Cpf.Criar("33287151002").Value!;

        VinculoDiscente discente = VinculoDiscente.Reidratar(
            id: idOriginal,
            idDiscenteSigaa: 50231,
            matricula: "20142600001",
            cpf: cpf,
            nome: "ANA EXEMPLO",
            nivel: "G",
            cursoId: 20,
            cursoNome: "DIREITO",
            cursoCodigoEmec: "12078",
            cursoUnidadeId: 64,
            cursoUnidadeNome: "INSTITUTO DE DIREITO",
            situacaoId: 3,
            situacaoDescricao: "CONCLUÍDO",
            situacaoVinculo: null,
            anoIngresso: 2014,
            periodoIngresso: 2);

        Assert.Equal(idOriginal, discente.Id);
    }

    [Fact(DisplayName = "VinculoDiscentes.ToString - Nao Deve Expor Dados Pessoais")]
    public void ToString_NaoDeveExporDadosPessoais()
    {
        Cpf cpf = Cpf.Criar("33287151002").Value!;

        VinculoDiscente discente = VinculoDiscente.Criar(
            idDiscenteSigaa: 50231,
            matricula: "20142600001",
            cpf: cpf,
            nome: "ANA EXEMPLO DE SOUZA",
            nivel: "G",
            cursoId: 20,
            cursoNome: "DIREITO",
            cursoCodigoEmec: "12078",
            cursoUnidadeId: 64,
            cursoUnidadeNome: "INSTITUTO DE DIREITO",
            situacaoId: 3,
            situacaoDescricao: "CONCLUÍDO",
            situacaoVinculo: null,
            anoIngresso: 2014,
            periodoIngresso: 2);

        string resultado = discente.ToString();

        Assert.DoesNotContain("ANA EXEMPLO DE SOUZA", resultado);
        Assert.DoesNotContain("20142600001", resultado);
        Assert.DoesNotContain("12345678901", resultado);

        Assert.Contains("50231", resultado);
        Assert.Contains(discente.Id.ToString(), resultado);
    }
}
