namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Xunit;

/// <summary>
/// A falta de uma banca que a origem dos candidatos exige chega ao cliente com o status 422 e o código estável do módulo, que o
/// <c>type</c> do problema resolve na página do catálogo.
/// </summary>
public sealed class ErrosDasBancasQueAOrigemExigeTests
{
    private static readonly IDomainErrorMapper Mapper = MapperDoModuloDeTeste.Criar();

    [Theory(DisplayName = "A falta de banca que a origem exige é mapeada para 422 com o código estável do módulo")]
    [InlineData("ProcessoSeletivo.HomologacaoDasInscricoesSemBanca", "uniplus.selecao.processo_seletivo.homologacao_das_inscricoes_sem_banca")]
    [InlineData("ProcessoSeletivo.NotaDoEnemSemBancaDeImportacaoDasNotas", "uniplus.selecao.processo_seletivo.nota_do_enem_sem_banca_de_importacao_das_notas")]
    [InlineData("ProcessoSeletivo.ImportacaoExternaSemBancaDeImportacaoDeCandidatos", "uniplus.selecao.processo_seletivo.importacao_externa_sem_banca_de_importacao_de_candidatos")]
    public void Recusa_MapeadaPara422ComOCodigoEstavel(string codigoDoDominio, string codigoNoFio)
    {
        bool mapeada = Mapper.TryGetMapping(codigoDoDominio, out DomainErrorMapping? mapeamento);

        mapeada.Should().BeTrue();
        mapeamento!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        mapeamento.Code.Should().Be(codigoNoFio);
        mapeamento.Title.Should().NotBeNullOrWhiteSpace();
        Mapper.GetProblemTypeUri(codigoNoFio).Should().Be(MapperDoModuloDeTeste.BaseUriDeErros + codigoNoFio);
    }
}
