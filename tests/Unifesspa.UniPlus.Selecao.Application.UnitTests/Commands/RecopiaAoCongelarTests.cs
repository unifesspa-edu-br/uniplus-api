namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RecopiaAoCongelarTests
{
    [Fact(DisplayName = "O item incoerente e a unidade excluída saem no mesmo lote, os itens primeiro")]
    public async Task ItemIncoerenteEUnidadeExcluida_SaemNoMesmoLoteComOsItensPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Preparo", out _);
        RecopiaAoCongelar servico = new ServicosDoCongelamentoDeTeste().ComUnidades(ResolvedorDeUnidadeDeTeste.Com()).Recopia();

        Result<RecopiadoAoCongelar> recopia = await servico
            .RecopiarAsync(processo, CatalogoComRgUfPorMunicipio(), new PrimeiraPublicacao(), CancellationToken.None);

        recopia.IsFailure.Should().BeTrue();
        string[] codigos = [.. recopia.Errors.Select(static e => e.Error.Code)];
        codigos.Should().Contain("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        codigos.First().Should().NotBe("FaseCronograma.UnidadeResponsavelNaoEncontrada", "as recusas dos itens vêm antes das das unidades");
        codigos.Length.Should().BeGreaterThan(1);
    }

    private static CatalogoVivo CatalogoComRgUfPorMunicipio()
    {
        IReadOnlyList<FatoCandidatoView> fatos =
            [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo == "RG_UF" ? f with { FonteValores = "GEO_MUNICIPIO" } : f)];

        return new CatalogoVivo(
            fatos, fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal), FatosDeModalidadeDeTeste.DoCatalogo,
            new Dictionary<Guid, TipoDocumentoView>());
    }
}
