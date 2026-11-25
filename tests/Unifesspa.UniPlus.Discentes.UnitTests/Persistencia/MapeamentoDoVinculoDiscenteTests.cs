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
        VinculoDiscenteCopiado dadosCopiados = VinculoDeTeste.DadosCopiados();
        VinculoDiscenteRecord registro = new();

        MapeamentoDoVinculoDiscente.Aplicar(registro, dadosCopiados, CpfCifrado);

        registro.CpfCiphertext.Should().Equal(CpfCifrado);
        registro.IdDiscenteSigaa.Should().Be(dadosCopiados.Identificacao.IdDiscenteSigaa.Valor);
        registro.Matricula.Should().Be(dadosCopiados.Identificacao.Matricula.Valor);
        registro.Nome.Should().Be(dadosCopiados.Identificacao.Nome.Valor);
        registro.Nivel.Should().Be(dadosCopiados.Nivel.Valor);
    }

    [Fact]
    public void Aplicar_copia_o_curso_para_o_registro()
    {
        VinculoDiscenteCopiado dadosCopiados = VinculoDeTeste.DadosCopiados();
        VinculoDiscenteRecord registro = new();

        MapeamentoDoVinculoDiscente.Aplicar(registro, dadosCopiados, CpfCifrado);

        registro.CursoId.Should().Be(dadosCopiados.Curso.Id.Valor);
        registro.CursoNome.Should().Be(dadosCopiados.Curso.Nome.Valor);
        registro.CursoCodigoEmec.Should().Be(dadosCopiados.Curso.CodigoEmec?.Valor);
        registro.CursoUnidadeId.Should().Be(dadosCopiados.Curso.Unidade.Id.Valor);
        registro.CursoUnidadeNome.Should().Be(dadosCopiados.Curso.Unidade.Nome.Valor);
    }

    [Fact]
    public void Aplicar_copia_a_situacao_e_o_ingresso_para_o_registro()
    {
        VinculoDiscenteCopiado dadosCopiados = VinculoDeTeste.DadosCopiados();
        VinculoDiscenteRecord registro = new();

        MapeamentoDoVinculoDiscente.Aplicar(registro, dadosCopiados, CpfCifrado);

        registro.SituacaoId.Should().Be(dadosCopiados.Situacao.Id.Valor);
        registro.SituacaoDescricao.Should().Be(dadosCopiados.Situacao.Descricao.Valor);
        registro.SituacaoVinculo.Should().Be(dadosCopiados.Situacao.Qualificador?.Valor);
        registro.AnoIngresso.Should().Be(dadosCopiados.Ingresso.Ano);
        registro.PeriodoIngresso.Should().Be(dadosCopiados.Ingresso.Periodo);
    }

    [Fact]
    public void Qualificador_da_situacao_vai_e_volta_quando_existe()
    {
        VinculoDiscenteCopiado dadosCopiados = VinculoDeTeste.Valor(VinculoDiscenteCopiado.Criar(
            VinculoDeTeste.Identificacao(),
            "G",
            VinculoDeTeste.Curso(),
            VinculoDeTeste.Valor(SituacaoAcademicaCopiada.Criar(1, "ATIVO", "ATV")),
            VinculoDeTeste.Ingresso()));
        VinculoDiscenteRecord registro = new() { Id = Guid.NewGuid() };
        MapeamentoDoVinculoDiscente.Aplicar(registro, dadosCopiados, CpfCifrado);

        VinculoDiscente lido = MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        registro.SituacaoVinculo.Should().Be("ATV");
        lido.DadosCopiados.Situacao.Qualificador!.Valor.Should().Be("ATV");
    }

    [Fact]
    public void Registro_aplicado_volta_como_o_mesmo_vinculo()
    {
        VinculoDiscente original = VinculoDiscente.Criar(VinculoDeTeste.DadosCopiados());
        VinculoDiscenteRecord registro = new() { Id = original.Id };
        MapeamentoDoVinculoDiscente.Aplicar(registro, original.DadosCopiados, CpfCifrado);

        VinculoDiscente lido = MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        lido.Id.Should().Be(original.Id);
        lido.DadosCopiados.Should().Be(original.DadosCopiados);
    }

    [Theory]
    [InlineData(nameof(CursoCopiadoDoSigaa), "Curso.NomeVazio")]
    [InlineData(nameof(SituacaoAcademicaCopiada), "SituacaoAcademica.DescricaoVazia")]
    [InlineData(nameof(PeriodoIngresso), "PeriodoIngresso.PeriodoInvalido")]
    public void Registro_corrompido_falha_alto_com_o_codigo_da_regra(string nome, string codigo)
    {
        VinculoDiscenteRecord registro = RegistroValido();
        Corromper(registro, nome);

        Action ler = () => MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        ler.Should().Throw<InvalidOperationException>().WithMessage($"Dado inválido no banco ao reidratar {nome}: {codigo}. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
    }

    [Fact]
    public void Identificacao_corrompida_no_registro_falha_alto()
    {
        VinculoDiscenteRecord registro = RegistroValido();
        registro.Matricula = "12A";

        Action ler = () => MapeamentoDoVinculoDiscente.ParaDominio(registro, VinculoDeTeste.CpfDeTeste());

        ler.Should().Throw<InvalidOperationException>()
            .WithMessage("Dado inválido no banco ao reidratar IdentificacaoDoDiscente: VinculoDiscente.MatriculaNaoNumerica. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
    }

    private static VinculoDiscenteRecord RegistroValido()
    {
        VinculoDiscenteRecord registro = new();
        MapeamentoDoVinculoDiscente.Aplicar(registro, VinculoDeTeste.DadosCopiados(), CpfCifrado);

        return registro;
    }

    private static void Corromper(VinculoDiscenteRecord registro, string nome)
    {
        switch (nome)
        {
            case nameof(CursoCopiadoDoSigaa):
                registro.CursoNome = " ";
                break;
            case nameof(SituacaoAcademicaCopiada):
                registro.SituacaoDescricao = " ";
                break;
            default:
                registro.PeriodoIngresso = 0;
                break;
        }
    }
}
