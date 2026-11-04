namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class PreparoDoCongelamentoTests
{
    [Fact(DisplayName = "O item incoerente e a unidade excluída saem no mesmo lote, os itens primeiro")]
    public async Task ItemIncoerenteEUnidadeExcluida_SaemNoMesmoLoteComOsItensPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Preparo", out _);
        IReadOnlyList<FatoCandidatoView> fatos =
            [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo == "RG_UF" ? f with { FonteValores = "GEO_MUNICIPIO" } : f)];
        CatalogoConferido catalogo = new(
            fatos, fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal), FatosDeModalidadeDeTeste.DoCatalogo,
            new Dictionary<Guid, TipoDocumentoView>());

        Result<ProtecaoVigenteDoProcesso> preparo = await PreparoDoCongelamento
            .PrepararAsync(processo, catalogo, null, null, ResolvedorDeUnidadeDeTeste.Com(), CancellationToken.None);

        preparo.IsFailure.Should().BeTrue();
        string[] codigos = [.. preparo.Errors.Select(static e => e.Error.Code)];
        codigos.Should().Contain("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        codigos.First().Should().NotBe("FaseCronograma.UnidadeResponsavelNaoEncontrada", "as recusas dos itens vêm antes das das unidades");
        codigos.Length.Should().BeGreaterThan(1);
    }
}
