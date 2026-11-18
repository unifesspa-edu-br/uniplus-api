namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O caminho completo do texto do documento até as declarações lidas pelo domínio, com o esquema publicado e a normalização reais:
/// a forma recusa a chave repetida e a ausente no ponteiro da fase, e o documento que passa pela forma chega ao domínio com a recusa
/// de cada parte no ponteiro do campo que a causa.
/// </summary>
public sealed class LeituraDaIdentificacaoEDoCronogramaDoDocumentoRealTests
{
    private const string Cabecalho = "\"formato\": \"uniplus.selecao.configuracao\", \"versaoFormato\": 1";

    private static readonly LeituraDoDocumentoDaConfiguracao LeituraDoTexto = new(new EsquemaDoDocumentoDaConfiguracao());

    private readonly IUnidadeReader _unidades = Substitute.For<IUnidadeReader>();
    private readonly IFaseCanonicaReader _fasesCanonicas = Substitute.For<IFaseCanonicaReader>();
    private readonly LeituraDaIdentificacaoEDoCronograma _leitura;

    public LeituraDaIdentificacaoEDoCronogramaDoDocumentoRealTests()
    {
        ITipoAtoPublicadoReader tiposDeAto = Substitute.For<ITipoAtoPublicadoReader>();
        tiposDeAto.ListarVigentesAsync(default!, default, default).ReturnsForAnyArgs([]);
        CadastrosDeTeste();
        _leitura = new(new(
            _unidades,
            tiposDeAto,
            Substitute.For<IRegraCatalogoReader>(),
            Substitute.For<IConferenciaEnderecoGeo>(),
            new(Substitute.For<ITipoProcessoReader>(), _fasesCanonicas, Substitute.For<ITipoBancaReader>(), Substitute.For<ICategoriaDocumentoReader>()),
            new(new RelogioFixo(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)), CenarioDeInscricaoPublicada.Fuso())));
    }

    private void CadastrosDeTeste()
    {
        _unidades.ObterPorCodigoAsync("CEPS", Arg.Any<CancellationToken>())
            .Returns(new UnidadeView(Guid.CreateVersion7(), "CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null));
        _fasesCanonicas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs(
        [
            new FaseCanonicaView(Guid.CreateVersion7(), "INSCRICAO", "Inscrição", null, false, false, null, true, false, "NO_EDITAL"),
            new FaseCanonicaView(Guid.CreateVersion7(), "RESULTADO", "Resultado", null, false, false, null, false, false, "NO_EDITAL"),
        ]);
    }

    private static Result<DocumentoLido> LerOTexto(string cronograma) => LeituraDoTexto.Ler($"{{ {Cabecalho}, {cronograma} }}");

    private async Task<Result<IdentificacaoECronogramaLidos>> LerODocumentoAsync(string cronograma)
    {
        Result<DocumentoLido> lido = LerOTexto(cronograma);
        lido.IsSuccess.Should().BeTrue();

        return await _leitura.LerAsync(lido.Value!.Documento, CancellationToken.None);
    }

    [Fact(DisplayName = "A fase com a chave de outra fase é recusada pela forma no ponteiro da chave da segunda ocorrência")]
    public void FaseComChaveRepetida_ERecusadaNoPonteiroDaChaveDaSegundaOcorrencia()
    {
        Result<DocumentoLido> lido = LerOTexto("""
            "cronograma": { "fases": [
              { "chave": "inscricao", "faseCanonicaCodigo": "INSCRICAO" },
              { "chave": "inscricao", "faseCanonicaCodigo": "RESULTADO" }
            ] }
            """);

        lido.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/cronograma/fases/1/chave", DocumentoDaConfiguracaoErrorCodes.ChaveRepetida));
    }

    [Fact(DisplayName = "A fase sem chave é recusada pela forma como campo obrigatório ausente no ponteiro da chave")]
    public void FaseSemChave_ERecusadaNoPonteiroDaChave()
    {
        Result<DocumentoLido> lido = LerOTexto("""
            "cronograma": { "fases": [
              { "chave": "inscricao", "faseCanonicaCodigo": "INSCRICAO" },
              { "faseCanonicaCodigo": "RESULTADO" }
            ] }
            """);

        lido.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/cronograma/fases/1/chave", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente));
    }

    [Fact(DisplayName = "As fases do documento são adotadas uma por posição, e a unidade que o cadastro não tem recusa só a parte, no ponteiro do campo")]
    public async Task Fases_SaoAdotadasEAUnidadeInexistenteRecusaSoAParte()
    {
        Result<IdentificacaoECronogramaLidos> lidos = await LerODocumentoAsync("""
            "cronograma": { "fases": [
              { "chave": "inscricao", "faseCanonicaCodigo": "INSCRICAO", "unidadeResponsavelCodigo": "CEPS" },
              { "chave": "resultado", "faseCanonicaCodigo": "RESULTADO", "unidadeResponsavelCodigo": "EXTINTA" }
            ] }
            """);

        lidos.Value!.Cronograma.CronogramaFases.Should().HaveCount(2);
        lidos.Value.Origem.PartesRecusadas.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code)).Should().Equal(
            ("/cronograma/fases/1/unidadeResponsavelCodigo", FaseCronogramaErrorCodes.UnidadeResponsavelNaoEncontrada));
    }

    [Fact(DisplayName = "A fase canônica que o cadastro não tem recusa a fase inteira no ponteiro do código, e a posição das demais não muda")]
    public async Task FaseCanonicaInexistente_RecusaAFaseNoPonteiroDoCodigo()
    {
        Result<IdentificacaoECronogramaLidos> lidos = await LerODocumentoAsync("""
            "cronograma": { "fases": [
              { "chave": "inscricao", "faseCanonicaCodigo": "INSCRICAO" },
              { "chave": "intermediaria", "faseCanonicaCodigo": "FASE_EXTINTA" },
              { "chave": "resultado", "faseCanonicaCodigo": "RESULTADO" }
            ] }
            """);

        lidos.Value!.Cronograma.CronogramaFases.Select(static fase => fase.Ordem).Should().Equal(1, 3);
        lidos.Value.Origem.PartesRecusadas.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code)).Should().Equal(
            ("/cronograma/fases/1/faseCanonicaCodigo", FaseCronogramaErrorCodes.FaseCanonicaNaoEncontrada));
    }
}
