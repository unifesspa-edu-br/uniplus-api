namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// Na definição dos documentos exigidos, a recusa da montagem de uma exigência (tipo de documento, por exemplo) sai junto das recusas
/// do catálogo e do domínio das outras exigências, cada uma na posição do pedido. A exigência recusada na montagem, e o grupo
/// com item recusado, não recebem as conferências seguintes, e nada é gravado.
/// </summary>
public sealed class RecusasDaMontagemJuntasDasDemaisExigenciasTests
{
    private const string FaseFora = "DocumentoExigido.FaseNaoPertenceAoProcesso";
    private const string DadoDesativado = "VinculoCatalogo.FatoDesativado";

    private static RegraDeComprovacaoInformada Regra(ItemDocumentoExigidoDeTeste item) => RegraDeComprovacaoDeTeste.DeDocumento(item).Construir();

    private static ItemDocumentoExigidoDeTeste ComTipoInexistente(CenarioDaExigencia cenario) =>
        GeralNoFormularioDaExigencia(cenario.Fase.Id, Guid.CreateVersion7());

    private static ItemDocumentoExigidoDeTeste ForaDoCronograma(CenarioDaExigencia cenario) =>
        GeralNoFormularioDaExigencia(Guid.CreateVersion7(), cenario.TipoDocumentoId);

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(Result resultado) =>
        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code));

    private static Task<Result> DefinirAsync(CenarioDaExigencia cenario, IReadOnlyList<RegraDeComprovacaoInformada> entradas, IReadOnlyList<RegraDeComprovacao> mantidas) =>
        DefinicaoDosDocumentosExigidos.DefinirAsync(
            cenario.Processo, entradas, mantidas,
            new CadastrosDosDocumentosExigidos(cenario.Mocks.TipoDocumentoReader, cenario.Mocks.FatoCandidatoReader, cenario.Mocks.Modelos), CancellationToken.None);

    private static RegraDeComprovacao Mantida(CenarioDaExigencia cenario) =>
        RegraDeComprovacao.CriarRegraDeDocumento(
            new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
                .ComMomento(new MomentoDaExigencia(cenario.Fase.Id, null, CodigoDoFormulario.Criar(CodigoDoFormularioDaExigencia).Value!))
                .Construir(), 0).Value!;

    [Fact(DisplayName = "A recusa de montagem da primeira regra e a fase fora do cronograma da segunda saem juntas, nessa ordem, cada uma na posição do pedido")]
    public async Task MontagemDaPrimeiraEFaseForaDaSegunda_SaemJuntas()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [Regra(ComTipoInexistente(cenario)), Regra(ForaDoCronograma(cenario))]));

        Recusas(resultado).Should().HaveCount(2).And.SatisfyRespectively(
            primeira => primeira.Campo.Should().StartWith("regrasDeComprovacao[0].documento"),
            segunda => segunda.Should().Be(("regrasDeComprovacao[1].documento.momento.faseId", FaseFora)));
        cenario.Processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com regras mantidas, as recusas voltam à posição das regras no pedido")]
    public async Task ComRegrasMantidas_AsPosicoesVoltamAsDoPedido()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await DefinirAsync(cenario, [Regra(ComTipoInexistente(cenario)), Regra(ForaDoCronograma(cenario))], [Mantida(cenario)]);

        Recusas(resultado).Should().HaveCount(2).And.SatisfyRespectively(
            primeira => primeira.Campo.Should().StartWith("regrasDeComprovacao[0].documento"),
            segunda => segunda.Should().Be(("regrasDeComprovacao[1].documento.momento.faseId", FaseFora)));
    }

    [Fact(DisplayName = "O grupo com item recusado na montagem não recebe recusa do domínio, e a outra exigência recebe as do catálogo e as do domínio")]
    public async Task GrupoComItemRecusado_NaoRecebeConferenciaSeguinte()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        cenario.Mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([FatoTipoDeficiencia() with { Ativo = false }]);
        RegraDeComprovacaoInformada grupo = RegraDeComprovacaoDeTeste.Grupo("TODOS_OS_ITENS", Regra(ForaDoCronograma(cenario)), Regra(ComTipoInexistente(cenario))).Construir();
        RegraDeComprovacaoInformada outra = Regra(ForaDoCronograma(cenario).ComFatosComprovados("TIPO_DEFICIENCIA"));

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [grupo, outra]));

        Recusas(resultado).Should().HaveCount(3).And.SatisfyRespectively(
            montagem => montagem.Campo.Should().StartWith("regrasDeComprovacao[0].itens[1].documento"),
            catalogo => catalogo.Should().Be(("regrasDeComprovacao[1].documento.fatosComprovados[0]", DadoDesativado)),
            dominio => dominio.Should().Be(("regrasDeComprovacao[1].documento.momento.faseId", FaseFora)));
    }

    [Fact(DisplayName = "Só a recusa de montagem não altera as exigências já gravadas")]
    public async Task SoRecusaDeMontagem_NaoAlteraOProcesso()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        (await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [Regra(cenario.ItemGeral())]))).IsSuccess.Should().BeTrue();
        Guid[] gravadas = [.. cenario.Processo.DocumentosExigidos.Select(static documento => documento.Id)];

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [Regra(ComTipoInexistente(cenario))]));

        resultado.IsFailure.Should().BeTrue();
        cenario.Processo.DocumentosExigidos.Select(static documento => documento.Id).Should().Equal(gravadas);
        await cenario.Mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
