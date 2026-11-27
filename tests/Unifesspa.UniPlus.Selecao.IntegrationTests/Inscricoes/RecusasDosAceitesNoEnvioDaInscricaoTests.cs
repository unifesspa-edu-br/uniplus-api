namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessoComMatrizDaLeiDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>
/// As recusas de termo do envio da inscrição pelo fluxo real da conferência: o aceite de outra versão ou de outro
/// conteúdo devolve só a recusa do aceite, e o termo sem aceite devolve só a recusa do termo.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class RecusasDosAceitesNoEnvioDaInscricaoTests
{
    private const string Declaracao = "DECLARACAO_VERACIDADE";
    private const string Adicional = "TERMO_ADICIONAL";

    private readonly CenarioDeInscricaoPublicada _cenario;

    public RecusasDosAceitesNoEnvioDaInscricaoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "O envio com aceite de outra versão do termo devolve só a recusa do aceite, sem recusar o termo")]
    public async Task Envio_AceiteDeOutraVersao_DevolveSoOAceiteDeOutraVersao()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Envio_AceiteDeOutraVersao_DevolveSoOAceiteDeOutraVersao));
        InscricaoRequest pedido = PedidoDoProcesso(processo);
        AceiteDeTermoInput aceite = pedido.Aceites![0] with { VersaoId = Guid.NewGuid() };

        Result<ReciboDaInscricaoDto> envio = await EnviarAsync(db, processo, pedido with { Aceites = [aceite] }, "00000110");

        CamposECodigos(envio).Should().Equal(("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao));
    }

    [Fact(DisplayName = "O envio com aceite de conteúdo diferente do fixado devolve só a recusa do aceite, sem recusar o termo")]
    public async Task Envio_AceiteComHashDiferente_DevolveSoOAceiteDeOutraVersao()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Envio_AceiteComHashDiferente_DevolveSoOAceiteDeOutraVersao));
        InscricaoRequest pedido = PedidoDoProcesso(processo);
        AceiteDeTermoInput aceite = pedido.Aceites![0] with { HashVersao = new string('b', 64) };

        Result<ReciboDaInscricaoDto> envio = await EnviarAsync(db, processo, pedido with { Aceites = [aceite] }, "00000111");

        CamposECodigos(envio).Should().Equal(("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao));
    }

    [Fact(DisplayName = "O envio sem aceite do termo obrigatório devolve só a recusa do termo, no campo do termo")]
    public async Task Envio_TermoSemAceite_DevolveSoOTermoNaoAceito()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Envio_TermoSemAceite_DevolveSoOTermoNaoAceito));
        InscricaoRequest pedido = PedidoDoProcesso(processo);

        Result<ReciboDaInscricaoDto> envio = await EnviarAsync(db, processo, pedido with { Aceites = [] }, "00000112");

        CamposECodigos(envio).Should().Equal((CampoDoTermo(Declaracao), InscricaoErrorCodes.TermoNaoAceito));
    }

    [Fact(DisplayName = "O envio com um termo de outra versão e outro sem aceite devolve as duas recusas, cada uma do seu termo")]
    public async Task Envio_UmTermoDeOutraVersaoEOutroSemAceite_DevolveAsDuasRecusas()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Envio_UmTermoDeOutraVersaoEOutroSemAceite_DevolveAsDuasRecusas));
        InscricaoRequest pedido = PedidoDoProcesso(processo);
        AceiteDeTermoInput aceite = pedido.Aceites![0] with { VersaoId = Guid.NewGuid() };
        processo.DefinirTermos([ExemploDeProcessoSeletivoEntrada.Termo(Declaracao, 0), ExemploDeProcessoSeletivoEntrada.Termo(Adicional, 1)]).IsSuccess.Should().BeTrue();

        Result<ReciboDaInscricaoDto> envio = await EnviarAsync(db, processo, pedido with { Aceites = [aceite] }, "00000113");

        CamposECodigos(envio).Should().Equal(
            ("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao),
            (CampoDoTermo(Adicional), InscricaoErrorCodes.TermoNaoAceito));
    }

    private async Task<Result<ReciboDaInscricaoDto>> EnviarAsync(UniPlusDbContext db, ProcessoSeletivo processo, InscricaoRequest pedido, string numero)
    {
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        return await _cenario.EnviarAsync(db, processoId, pedido, etag, numero);
    }

    private static InscricaoRequest PedidoDoProcesso(ProcessoSeletivo processo) =>
        PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true));

    private static string CampoDoTermo(string termo) =>
        DefinicaoDoProcesso.CodigoDoTermo(CodigoDoFormulario.Criar(FormularioDeTeste.CodigoDaInscricao).Value!, termo);

    private static IEnumerable<(string? Campo, string Codigo)> CamposECodigos(Result<ReciboDaInscricaoDto> envio) =>
        envio.Errors.Select(static e => (e.Field, e.Error.Code));
}
