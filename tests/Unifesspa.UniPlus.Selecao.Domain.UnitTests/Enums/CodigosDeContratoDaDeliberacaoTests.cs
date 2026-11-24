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
                [ModoDeDeliberacao.ColegiadaComVotacao] = "COLEGIADA_COM_VOTACAO",
                [ModoDeDeliberacao.AtoIndividual] = "ATO_INDIVIDUAL",
            });

    [Fact]
    public void CriterioDeVotacao_Valores_CodigosPublicados() =>
        Enum.GetValues<CriterioDeVotacao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<CriterioDeVotacao, string>
            {
                [CriterioDeVotacao.MaioriaSimples] = "MAIORIA_SIMPLES",
                [CriterioDeVotacao.MaioriaAbsoluta] = "MAIORIA_ABSOLUTA",
                [CriterioDeVotacao.Unanimidade] = "UNANIMIDADE",
            });

    [Fact]
    public void DesempateDaVotacao_Valores_CodigosPublicados() =>
        Enum.GetValues<DesempateDaVotacao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<DesempateDaVotacao, string>
            {
                [DesempateDaVotacao.VotoDaPresidencia] = "VOTO_DA_PRESIDENCIA",
                [DesempateDaVotacao.NovaVotacao] = "NOVA_VOTACAO",
            });

    [Fact]
    public void EncerramentoDaVotacao_Valores_CodigosPublicados() =>
        Enum.GetValues<EncerramentoDaVotacao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<EncerramentoDaVotacao, string>
            {
                [EncerramentoDaVotacao.TodosOsTitulares] = "TODOS_OS_TITULARES",
                [EncerramentoDaVotacao.PresidenciaNoQuorum] = "PRESIDENCIA_NO_QUORUM",
            });

    [Fact]
    public void ResultadoSemUnanimidade_Valores_CodigosPublicados() =>
        Enum.GetValues<ResultadoSemUnanimidade>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<ResultadoSemUnanimidade, string>
            {
                [ResultadoSemUnanimidade.Favoravel] = "FAVORAVEL",
                [ResultadoSemUnanimidade.Desfavoravel] = "DESFAVORAVEL",
                [ResultadoSemUnanimidade.NovaVotacao] = "NOVA_VOTACAO",
            });

}
