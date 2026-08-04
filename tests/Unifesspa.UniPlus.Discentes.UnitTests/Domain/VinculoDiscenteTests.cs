using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

public class VinculoDiscenteTests
{
    private static VinculoDiscenteSnapshot NovoSnapshot() =>
        VinculoDiscenteSnapshot.Criar(
            idDiscenteSigaa: 50231,
            matricula: "20142600001",
            cpf: Cpf.Criar("33287151002").Value!,
            nome: "ANA EXEMPLO",
            nivel: "G",
            curso: CursoSigaaSnapshot.Criar(
                id: 20,
                nome: "DIREITO",
                codigoEmec: "12078",
                unidadeId: 64,
                unidadeNome: "INSTITUTO DE DIREITO").Value!,
            situacao: SituacaoAcademicaSnapshot.Criar(
                id: 3,
                descricao: "CONCLUÍDO",
                vinculo: null).Value!,
            ingresso: PeriodoIngresso.Criar(ano: 2014, periodo: 2).Value!).Value!;

    [Fact(DisplayName = "VinculoDiscentes.Criar - Deve Gerar Uuid Valido e Instanciar Entidade")]
    public void Criar_DeveGerarUuidValidoEInstanciarEntidade()
    {
        VinculoDiscente discente = VinculoDiscente.Criar(NovoSnapshot());

        Assert.NotEqual(Guid.Empty, discente.Id);
        Assert.Equal(50231, discente.Snapshot.IdDiscenteSigaa);
        Assert.Equal("20142600001", discente.Snapshot.Matricula);
    }

    [Fact(DisplayName = "VinculoDiscentes.Reidratar - Deve Manter Guid Original sem Gerar novo ID")]
    public void Reidratar_DeveManterGuidOriginal_SemGerarNovoId()
    {
        Guid idOriginal = Guid.CreateVersion7();

        VinculoDiscente discente = VinculoDiscente.Reidratar(idOriginal, NovoSnapshot());

        Assert.Equal(idOriginal, discente.Id);
    }

    [Fact(DisplayName = "VinculoDiscentes.ToString - Nao Deve Expor Dados Pessoais")]
    public void ToString_NaoDeveExporDadosPessoais()
    {
        VinculoDiscenteSnapshot snapshot = VinculoDiscenteSnapshot.Criar(
            idDiscenteSigaa: 50231,
            matricula: "20142600001",
            cpf: Cpf.Criar("33287151002").Value!,
            nome: "ANA EXEMPLO DE SOUZA",
            nivel: "G",
            curso: CursoSigaaSnapshot.Criar(20, "DIREITO", "12078", 64, "INSTITUTO DE DIREITO").Value!,
            situacao: SituacaoAcademicaSnapshot.Criar(3, "CONCLUÍDO", null).Value!,
            ingresso: PeriodoIngresso.Criar(2014, 2).Value!).Value!;

        VinculoDiscente discente = VinculoDiscente.Criar(snapshot);

        string resultado = discente.ToString();

        Assert.DoesNotContain("ANA EXEMPLO DE SOUZA", resultado);
        Assert.DoesNotContain("20142600001", resultado);
        Assert.DoesNotContain("12345678901", resultado);

        Assert.Contains("50231", resultado);
        Assert.Contains(discente.Id.ToString(), resultado);
    }

    [Fact(DisplayName = "VinculoDiscenteSnapshot.ToString - Nao Deve Expor Nome nem Matricula")]
    public void VinculoDiscenteSnapshotToString_NaoDeveExporNomeNemMatricula()
    {
        VinculoDiscenteSnapshot snapshot = VinculoDiscenteSnapshot.Criar(
            idDiscenteSigaa: 50231,
            matricula: "20142600001",
            cpf: Cpf.Criar("33287151002").Value!,
            nome: "ANA EXEMPLO DE SOUZA",
            nivel: "G",
            curso: CursoSigaaSnapshot.Criar(20, "DIREITO", "12078", 64, "INSTITUTO DE DIREITO").Value!,
            situacao: SituacaoAcademicaSnapshot.Criar(3, "CONCLUÍDO", null).Value!,
            ingresso: PeriodoIngresso.Criar(2014, 2).Value!).Value!;

        string resultado = snapshot.ToString();

        Assert.DoesNotContain("ANA EXEMPLO DE SOUZA", resultado);
        Assert.DoesNotContain("20142600001", resultado);
        Assert.DoesNotContain("33287151002", resultado);
    }

    [Fact(DisplayName = "CursoSigaaSnapshot.Criar - Deve Falhar Quando Id For Invalido")]
    public void CursoSigaaSnapshot_Criar_DeveFalhar_QuandoIdForInvalido()
    {
        Result<CursoSigaaSnapshot> resultado =
            CursoSigaaSnapshot.Criar(id: 0, nome: "DIREITO", codigoEmec: null, unidadeId: 64, unidadeNome: "INSTITUTO");

        Assert.True(resultado.IsFailure);
    }

    [Fact(DisplayName = "SituacaoAcademicaSnapshot.Criar - Deve Falhar Quando Descricao For Vazia")]
    public void SituacaoAcademicaSnapshot_Criar_DeveFalhar_QuandoDescricaoForVazia()
    {
        Result<SituacaoAcademicaSnapshot> resultado =
            SituacaoAcademicaSnapshot.Criar(id: 3, descricao: "  ", vinculo: null);

        Assert.True(resultado.IsFailure);
    }

    [Fact(DisplayName = "PeriodoIngresso.Criar - Deve Falhar Quando Periodo For Invalido")]
    public void PeriodoIngresso_Criar_DeveFalhar_QuandoPeriodoForInvalido()
    {
        Result<PeriodoIngresso> resultado = PeriodoIngresso.Criar(ano: 2014, periodo: 0);

        Assert.True(resultado.IsFailure);
    }
}
