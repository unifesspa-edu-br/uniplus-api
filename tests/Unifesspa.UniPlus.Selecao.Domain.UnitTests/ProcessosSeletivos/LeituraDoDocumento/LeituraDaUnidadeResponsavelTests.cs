namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>A unidade responsável da fase vem do cadastro pelo código citado; a que o cadastro não tem deixa a fase sem unidade e suspende a completude dela.</summary>
public sealed class LeituraDaUnidadeResponsavelTests
{
    [Fact(DisplayName = "O código citado resolve a unidade responsável da fase com o id do cadastro")]
    public void Unidade_Existente_EntraNaFase()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "inscricao", unidade: "CGA")));

        lidos.Cronograma.CronogramaFases.Single().UnidadeResponsavel!.Codigo.Should().Be("CGA");
    }

    [Fact(DisplayName = "A unidade que o cadastro não tem deixa a fase adotada sem unidade e aponta o campo que a cita")]
    public void Unidade_Inexistente_FaseFicaSemUnidade()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "inscricao", unidade: "UNIDADE-DIGITADA")));

        lidos.Cronograma.CronogramaFases.Single().UnidadeResponsavel.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/unidadeResponsavelCodigo", FaseCronogramaErrorCodes.UnidadeResponsavelNaoEncontrada));
        Cronograma(lidos).Fases.Single().ParteRecusadaEm(FaseDeclarada.CampoDaUnidadeResponsavel).Should().BeTrue();
    }

    [Fact(DisplayName = "A fase sem código de unidade fica sem unidade e sem parte recusada, para a completude recusá-la")]
    public void Unidade_Ausente_NaoEParteRecusada()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "inscricao", unidade: null)));

        lidos.Cronograma.CronogramaFases.Single().UnidadeResponsavel.Should().BeNull();
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }
}
