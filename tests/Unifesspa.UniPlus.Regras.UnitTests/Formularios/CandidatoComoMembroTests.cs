namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>O grupo que inclui o próprio candidato identifica a ocorrência dele pelo parentesco (UNI-REQ-0146).</summary>
public sealed class CandidatoComoMembroTests
{
    private const string Parentesco = CandidatoComoMembro.FatoParentesco;

    [Theory]
    [InlineData("valido", null)]
    [InlineData("minimoZero", "minimo")]
    [InlineData("semParentesco", "subitens")]
    [InlineData("parentescoOpcional", "subitens[0]")]
    [InlineData("parentescoComExibicao", "subitens[0]")]
    [InlineData("opcoesSemOCandidato", "subitens[0].restricoes")]
    public void Conferir_GrupoQueIncluiOCandidato_RecusaOQueNaoIdentificaAOcorrenciaDele(string caso, string? campoRecusado)
    {
        Subitem parentesco = new(Parentesco, false, Obrigatoriedade.Sempre, []);
        (int minimo, Subitem[] subitens) = caso switch
        {
            "minimoZero" => (0, new[] { parentesco }),
            "semParentesco" => (1, [new Subitem("SEM_RENDA", false, Obrigatoriedade.Sempre, [])]),
            "parentescoOpcional" => (1, [parentesco with { Obrigatoriedade = Obrigatoriedade.Nunca }]),
            "parentescoComExibicao" => (1, [parentesco with { TemExibicao = true }]),
            "opcoesSemOCandidato" => (1, [parentesco with { Restricoes = [Opcoes("PAI_OU_MAE")] }]),
            _ => (1, [parentesco with { Restricoes = [Opcoes(CandidatoComoMembro.ProprioCandidato, "PAI_OU_MAE")] }]),
        };

        List<FieldError> erros = CandidatoComoMembro.Conferir(
            minimo, [.. subitens.Select(static s => (s.FatoCodigo, s.TemExibicao, s.Obrigatoriedade, s.Restricoes))]);

        erros.Select(static e => e.Field).Should().Equal(campoRecusado is null ? [] : [campoRecusado]);
        erros.Should().AllSatisfy(static e => e.Error.Code.Should().Be(GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto));
    }

    private static OpcoesPermitidas Opcoes(params string[] valores) => new([new OpcoesCondicionadas(null, valores)]);

    private sealed record Subitem(string FatoCodigo, bool TemExibicao, Obrigatoriedade Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes);
}
