namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As regras de forma do formulário, as mesmas no processo e no modelo (ADR-0137): blocos que a
/// finalidade admite e exige, a revisão e aceite por último, e os itens em seções, na ordem delas.
/// </summary>
public sealed class EstruturaFormularioTests
{
    private static EtapaEstrutura Secao(string codigo, int ordem) => new(codigo, ordem, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum);

    private static EtapaEstrutura Bloco(BlocoSistema bloco, int ordem) => new(bloco.ToString(), ordem, TipoEtapaFormulario.Bloco, bloco);

    [Fact(DisplayName = "Seções seguidas da revisão e aceite formam um formulário válido")]
    public void ValidarEtapas_Valido() =>
        EstruturaFormulario.ValidarEtapas(
            FinalidadeFormulario.Inscricao,
            [Secao("DADOS", 0), Bloco(BlocoSistema.ModalidadesCalculadas, 1), Bloco(BlocoSistema.RevisaoEAceite, 2)])
            .Should().BeEmpty();

    [Theory(DisplayName = "A finalidade decide os blocos: modalidades calculadas só na inscrição, e todo formulário termina na revisão e aceite")]
    [InlineData(FinalidadeFormulario.Habilitacao, EstruturaFormularioErrorCodes.BlocoNaoAdmitido)]
    [InlineData(FinalidadeFormulario.IsencaoTaxa, EstruturaFormularioErrorCodes.BlocoNaoAdmitido)]
    public void ValidarEtapas_BlocoNaoAdmitido(FinalidadeFormulario finalidade, string esperado) =>
        EstruturaFormulario.ValidarEtapas(
            finalidade, [Bloco(BlocoSistema.ModalidadesCalculadas, 0), Bloco(BlocoSistema.RevisaoEAceite, 1)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(esperado);

    [Fact(DisplayName = "Sem a revisão e aceite, o formulário é recusado")]
    public void ValidarEtapas_SemRevisao() =>
        EstruturaFormulario.ValidarEtapas(FinalidadeFormulario.Habilitacao, [Secao("DADOS", 0)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.BlocoExigidoAusente);

    [Fact(DisplayName = "A revisão e aceite fora da última posição é recusada")]
    public void ValidarEtapas_RevisaoForaDoFim() =>
        EstruturaFormulario.ValidarEtapas(FinalidadeFormulario.Habilitacao, [Bloco(BlocoSistema.RevisaoEAceite, 0), Secao("DADOS", 1)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.RevisaoEAceiteForaDoFim);

    [Fact(DisplayName = "Código e ordem de etapa repetidos acumulam as duas recusas")]
    public void ValidarEtapas_Repeticoes() =>
        EstruturaFormulario.ValidarEtapas(
            FinalidadeFormulario.Habilitacao, [Secao("DADOS", 0), Secao("DADOS", 0), Bloco(BlocoSistema.RevisaoEAceite, 1)])
            .Select(static e => e.Error.Code).Should().BeEquivalentTo(
                [EstruturaFormularioErrorCodes.EtapaCodigoDuplicado, EstruturaFormularioErrorCodes.EtapaOrdemDuplicada]);

    [Theory(DisplayName = "Item fora de seção — sem seção, em bloco ou em seção inexistente — é recusado")]
    [InlineData(null)]
    [InlineData("RevisaoEAceite")]
    [InlineData("INEXISTENTE")]
    public void ValidarItens_ForaDeSecao(string? etapa) =>
        EstruturaFormulario.ValidarItens(
            [Secao("DADOS", 0), Bloco(BlocoSistema.RevisaoEAceite, 1)], [new ItemEstrutura("COR_RACA", 0, etapa)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);

    [Fact(DisplayName = "A ordem dos itens acompanha a das seções: item de seção anterior não vem depois")]
    public void ValidarItens_ForaDaOrdemDasSecoes()
    {
        EtapaEstrutura[] etapas = [Secao("DADOS", 0), Secao("ENDERECO", 1), Bloco(BlocoSistema.RevisaoEAceite, 2)];

        EstruturaFormulario.ValidarItens(etapas, [new ItemEstrutura("CEP", 0, "ENDERECO"), new ItemEstrutura("NOME", 1, "DADOS")])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDaOrdemDasSecoes);
        EstruturaFormulario.ValidarItens(etapas, [new ItemEstrutura("NOME", 0, "DADOS"), new ItemEstrutura("CEP", 1, "ENDERECO")])
            .Should().BeEmpty();
    }
}
