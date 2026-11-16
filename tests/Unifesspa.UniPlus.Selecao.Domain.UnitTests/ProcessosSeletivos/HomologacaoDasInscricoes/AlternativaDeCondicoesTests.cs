namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>Um grupo de condições que valem juntas (E); a regra homologa quando algum grupo se cumpre (OU).</summary>
public sealed class AlternativaDeCondicoesTests
{
    [Fact(DisplayName = "A alternativa mantém as condições na ordem declarada, porque a ordem é a dos motivos que a inscrição recebe")]
    public void Criar_ComCondicoes_MantemAOrdemDeclarada()
    {
        CondicaoDeHomologacao primeira = ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP");
        CondicaoDeHomologacao segunda = ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP_ANTERIOR");

        Result<AlternativaDeCondicoes> resultado = AlternativaDeCondicoes.Criar([primeira, segunda]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Condicoes.Should().Equal(primeira, segunda);
    }

    [Fact(DisplayName = "Alternativa sem nenhuma condição é recusada: um grupo vazio valeria para toda inscrição sem dizer o que se apura")]
    public void Criar_SemCondicao_Recusa()
    {
        Result<AlternativaDeCondicoes> resultado = AlternativaDeCondicoes.Criar([]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(HomologacaoInscricaoErrorCodes.AlternativaSemCondicao);
    }

    [Fact(DisplayName = "Alternativa com condição nula é recusada")]
    public void Criar_ComCondicaoNula_Lanca()
    {
        Action criar = () => AlternativaDeCondicoes.Criar([null!]);

        criar.Should().Throw<ArgumentException>();
    }
}
