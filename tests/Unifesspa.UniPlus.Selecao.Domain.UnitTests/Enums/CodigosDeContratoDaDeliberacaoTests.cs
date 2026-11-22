namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDeContratoDaDeliberacaoTests
{
    [Fact]
    public void ModoDeDeliberacao_Valores_CodigosPublicados() =>
        Enum.GetValues<ModoDeDeliberacao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<ModoDeDeliberacao, string>
            {
                [ModoDeDeliberacao.ColegiadaComVotacao] = "colegiadaComVotacao",
                [ModoDeDeliberacao.AtoIndividual] = "atoIndividual",
            });

    [Fact]
    public void CriterioDeVotacao_Valores_CodigosPublicados() =>
        Enum.GetValues<CriterioDeVotacao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<CriterioDeVotacao, string>
            {
                [CriterioDeVotacao.MaioriaSimples] = "maioriaSimples",
                [CriterioDeVotacao.MaioriaAbsoluta] = "maioriaAbsoluta",
                [CriterioDeVotacao.Unanimidade] = "unanimidade",
            });

    [Fact]
    public void DesempateDaVotacao_Valores_CodigosPublicados() =>
        Enum.GetValues<DesempateDaVotacao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<DesempateDaVotacao, string>
            {
                [DesempateDaVotacao.VotoDaPresidencia] = "votoDaPresidencia",
                [DesempateDaVotacao.NovaVotacao] = "novaVotacao",
            });

    [Fact]
    public void EncerramentoDaVotacao_Valores_CodigosPublicados() =>
        Enum.GetValues<EncerramentoDaVotacao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<EncerramentoDaVotacao, string>
            {
                [EncerramentoDaVotacao.TodosOsTitulares] = "todosOsTitulares",
                [EncerramentoDaVotacao.PresidenciaNoQuorum] = "presidenciaNoQuorum",
            });

    [Fact]
    public void ResultadoSemUnanimidade_Valores_CodigosPublicados() =>
        Enum.GetValues<ResultadoSemUnanimidade>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<ResultadoSemUnanimidade, string>
            {
                [ResultadoSemUnanimidade.Favoravel] = "favoravel",
                [ResultadoSemUnanimidade.Desfavoravel] = "desfavoravel",
                [ResultadoSemUnanimidade.NovaVotacao] = "novaVotacao",
            });

}
