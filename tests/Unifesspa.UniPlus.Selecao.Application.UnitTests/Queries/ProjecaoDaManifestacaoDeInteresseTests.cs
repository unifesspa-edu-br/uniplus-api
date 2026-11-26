namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

public sealed class ProjecaoDaManifestacaoDeInteresseTests
{
    [Fact(DisplayName = "Sem declaração, a leitura devolve a manifestação não prevista, nula")]
    public void Projetar_SemDeclaracao_DevolveNulo() =>
        ProjecaoDoProcessoSeletivo.Projetar(Processo()).ManifestacaoDeInteresse.Should().BeNull();

    [Theory(DisplayName = "Com declaração, a leitura traz o código com o rótulo")]
    [InlineData("UMA_VEZ", "Manifesta interesse uma só vez")]
    [InlineData("CONFIRMA_A_CADA_CHAMADA", "Confirma o interesse a cada chamada")]
    public void Projetar_ComDeclaracao_TrazCodigoERotulo(string codigo, string rotulo)
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirManifestacaoDeInteresse(codigo).IsSuccess.Should().BeTrue();

        CodigoComRotulo<ManifestacaoDeInteresse>? leitura = ProjecaoDoProcessoSeletivo.Projetar(processo).ManifestacaoDeInteresse;

        leitura.Should().NotBeNull();
        (leitura!.Codigo, leitura.Rotulo).Should().Be((codigo, rotulo));
    }

    private static ProcessoSeletivo Processo() => ProcessosDeTeste.SemConfiguracao("Processo de manifestação", OrigemCandidatos.InscricaoPropria);
}
