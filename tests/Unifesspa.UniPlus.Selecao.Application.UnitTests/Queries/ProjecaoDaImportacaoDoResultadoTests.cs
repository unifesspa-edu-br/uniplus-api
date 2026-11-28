namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class ProjecaoDaImportacaoDoResultadoTests
{
    private static ProcessoSeletivo Processo() => ProcessosDeTeste.SemConfiguracao("Processo de resultado importado", OrigemCandidatos.ImportacaoExterna);

    [Fact]
    public void Projetar_PorDeclarar_DevolveNull() =>
        ProjecaoDoProcessoSeletivo.Projetar(Processo()).ImportacaoDoResultado.Should().BeNull();

    [Fact]
    public void Projetar_SoComRelatorios_DevolveRelatoriosSemFase()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirImportacaoDoResultado([new("INEP_RETORNO_ENEM", "v1"), new("INEP_RETORNO_SISU", "v2")], null).IsSuccess.Should().BeTrue();

        ImportacaoDoResultadoDto importacao = ProjecaoDoProcessoSeletivo.Projetar(processo).ImportacaoDoResultado!;

        importacao.Relatorios.Should().Equal(new RelatorioDaImportacaoDto("INEP_RETORNO_ENEM", "v1"), new RelatorioDaImportacaoDto("INEP_RETORNO_SISU", "v2"));
        importacao.PrimeiraFaseDepoisDaImportacaoId.Should().BeNull();
    }
}
