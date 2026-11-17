namespace Unifesspa.UniPlus.Discentes.UnitTests.Persistencia;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Discentes.UnitTests.Domain;

/// <summary>
/// A tradução do vínculo entre o registro persistido e o domínio: tudo o que o vínculo tem
/// vai para o registro e volta igual, e o registro que não passa mais pela regra de domínio
/// falha alto.
/// </summary>
public sealed class MapeamentoDoVinculoDiscenteTests
{
    private static readonly byte[] CpfCifrado = [1, 2, 3];

    [Fact]
    public void Aplicar_copia_a_identificacao_e_o_cpf_cifrado_para_o_registro()
    {
        VinculoDiscenteSnapshot snapshot = VinculoDeTeste.Snapshot();
        VinculoDiscenteRecord registro = new();

        MapeamentoDoVinculoDiscente.Aplicar(registro, snapshot, CpfCifrado);

        registro.CpfCiphertext.Should().Equal(CpfCifrado);
        registro.IdDiscenteSigaa.Should().Be(snapshot.Identificacao.IdDiscenteSigaa);
        registro.Matricula.Should().Be(snapshot.Identificacao.Matricula);
        registro.Nome.Should().Be(snapshot.Identificacao.Nome);
        registro.Nivel.Should().Be(snapshot.Nivel);
    }

    [Fact]
    public void Aplicar_copia_o_curso_para_o_registro()
    {
        VinculoDiscenteSnapshot snapshot = VinculoDeTeste.Snapshot();
        VinculoDiscenteRecord registro = new();

        MapeamentoDoVinculoDiscente.Aplicar(registro, snapshot, CpfCifrado);

        registro.CursoId.Should().Be(snapshot.Curso.Id);
        registro.CursoNome.Should().Be(snapshot.Curso.Nome);
        registro.CursoCodigoEmec.Should().Be(snapshot.Curso.CodigoEmec);
        registro.CursoUnidadeId.Should().Be(snapshot.Curso.UnidadeId);
        registro.CursoUnidadeNome.Should().Be(snapshot.Curso.UnidadeNome);
    }

    [Fact]
    public void Aplicar_copia_a_situacao_e_o_ingresso_para_o_registro()
    {
        VinculoDiscenteSnapshot snapshot = VinculoDeTeste.Snapshot();
        VinculoDiscenteRecord registro = new();

        MapeamentoDoVinculoDiscente.Aplicar(registro, snapshot, CpfCifrado);

        registro.SituacaoId.Should().Be(snapshot.Situacao.Id);
        registro.SituacaoDescricao.Should().Be(snapshot.Situacao.Descricao);
        registro.SituacaoVinculo.Should().Be(snapshot.Situacao.Vinculo);
        registro.AnoIngresso.Should().Be(snapshot.Ingresso.Ano);
        registro.PeriodoIngresso.Should().Be(snapshot.Ingresso.Periodo);
    }

    [Fact]
    public void Qualificador_da_situacao_vai_e_volta_quando_existe()
    {
        VinculoDiscenteSnapshot snapshot = VinculoDeTeste.Valor(VinculoDiscenteSnapshot.Criar(
            VinculoDeTeste.Identificacao(),
            "G",
            VinculoDeTeste.Curso(),
            VinculoDeTeste.Valor(SituacaoAcademicaSnapshot.Criar(1, "ATIVO", "ATV")),
            VinculoDeTeste.Ingresso()));
        VinculoDiscenteRecord registro = new() { Id = Guid.NewGuid() };
        MapeamentoDoVinculoDiscente.Aplicar(registro, snapshot, CpfCifrado);

        VinculoDiscente lido = MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        registro.SituacaoVinculo.Should().Be("ATV");
        lido.Snapshot.Situacao.Vinculo.Should().Be("ATV");
    }

    [Fact]
    public void Registro_aplicado_volta_como_o_mesmo_vinculo()
    {
        VinculoDiscente original = VinculoDiscente.Criar(VinculoDeTeste.Snapshot());
        VinculoDiscenteRecord registro = new() { Id = original.Id };
        MapeamentoDoVinculoDiscente.Aplicar(registro, original.Snapshot, CpfCifrado);

        VinculoDiscente lido = MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        lido.Id.Should().Be(original.Id);
        lido.Snapshot.Should().Be(original.Snapshot);
    }

    [Theory]
    [InlineData("Curso corrompido", "Curso.NomeVazio")]
    [InlineData("Situação corrompida", "SituacaoAcademica.DescricaoVazia")]
    [InlineData("Período corrompido", "PeriodoIngresso.PeriodoInvalido")]
    public void Registro_corrompido_falha_alto_com_o_codigo_da_regra(string defeito, string codigo)
    {
        VinculoDiscenteRecord registro = RegistroValido();
        Corromper(registro, defeito);

        Action ler = () => MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        ler.Should().Throw<InvalidOperationException>().WithMessage($"{defeito} no registro persistido ({codigo}).");
    }

    [Fact]
    public void Identificacao_corrompida_no_registro_falha_alto()
    {
        VinculoDiscenteRecord registro = RegistroValido();
        registro.Matricula = "12A";

        Action ler = () => MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        ler.Should().Throw<InvalidOperationException>()
            .WithMessage("Snapshot corrompido no registro persistido (VinculoDiscente.MatriculaNaoNumerica).");
    }

    private static VinculoDiscenteRecord RegistroValido()
    {
        VinculoDiscenteRecord registro = new();
        MapeamentoDoVinculoDiscente.Aplicar(registro, VinculoDeTeste.Snapshot(), CpfCifrado);

        return registro;
    }

    private static void Corromper(VinculoDiscenteRecord registro, string defeito)
    {
        switch (defeito)
        {
            case "Curso corrompido":
                registro.CursoNome = " ";
                break;
            case "Situação corrompida":
                registro.SituacaoDescricao = " ";
                break;
            default:
                registro.PeriodoIngresso = 0;
                break;
        }
    }
}
