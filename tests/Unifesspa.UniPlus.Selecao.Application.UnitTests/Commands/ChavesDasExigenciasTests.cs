namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ApoioDosDocumentosExigidos;

public sealed class ChavesDasExigenciasTests
{
    [Fact]
    public async Task Handle_ChaveReconhecida_PreservaIdentidadeEAtualiza()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        (await cenario.DefinirAsync(cenario.ItemGeral())).IsSuccess.Should().BeTrue();
        DocumentoExigido anterior = cenario.Processo.DocumentosExigidos.Single();
        ItemDocumentoExigidoInput entrada = cenario.ItemGeral(tamanhoMaximoBytes: 1000).Construir() with { Chave = anterior.Chave.Valor };

        Result resultado = await HandleAsync(cenario.Mocks, cenario.Comando(entrada));

        resultado.IsSuccess.Should().BeTrue();
        DocumentoExigido atual = cenario.Processo.DocumentosExigidos.Single();
        atual.Id.Should().Be(anterior.Id);
        atual.Chave.Should().Be(anterior.Chave);
        atual.TamanhoMaximoBytes.Should().Be(1000);
    }

    [Fact]
    public async Task Handle_ChaveDesconhecida_CriaExigenciaNova()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        (await cenario.DefinirAsync(cenario.ItemGeral())).IsSuccess.Should().BeTrue();
        Guid anterior = cenario.Processo.DocumentosExigidos.Single().Id;
        ItemDocumentoExigidoInput entrada = cenario.ItemGeral().Construir() with { Chave = "exigencia_desconhecida" };

        (await HandleAsync(cenario.Mocks, cenario.Comando(entrada))).IsSuccess.Should().BeTrue();

        DocumentoExigido atual = cenario.Processo.DocumentosExigidos.Single();
        atual.Id.Should().NotBe(anterior);
        atual.Chave.Valor.Should().Be("exigencia_identidade");
    }

    [Fact]
    public async Task Handle_NovaAntesDaReconhecida_ReservaAChaveExistente()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        (await cenario.DefinirAsync(cenario.ItemGeral())).IsSuccess.Should().BeTrue();
        DocumentoExigido anterior = cenario.Processo.DocumentosExigidos.Single();
        ItemDocumentoExigidoInput nova = cenario.ItemGeral().Construir();
        ItemDocumentoExigidoInput reconhecida = nova with { Chave = anterior.Chave.Valor };
        DefinirDocumentosExigidosCommand comando = new(cenario.Processo.Id,
            [RegraDeComprovacaoDeTeste.DeDocumento(nova).Construir(), RegraDeComprovacaoDeTeste.DeDocumento(reconhecida).Construir()]);

        (await HandleAsync(cenario.Mocks, comando)).IsSuccess.Should().BeTrue();

        cenario.Processo.DocumentosExigidos.Select(static documento => documento.Chave.Valor)
            .Should().Equal("exigencia_identidade_2", "exigencia_identidade");
        cenario.Processo.DocumentosExigidos.Last().Id.Should().Be(anterior.Id);
    }
    [Fact]
    public async Task Handle_ChaveRepetida_ReconheceSomenteAPrimeiraExigencia()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        (await cenario.DefinirAsync(cenario.ItemGeral())).IsSuccess.Should().BeTrue();
        DocumentoExigido anterior = cenario.Processo.DocumentosExigidos.Single();
        ItemDocumentoExigidoInput entrada = cenario.ItemGeral().Construir() with { Chave = anterior.Chave.Valor };
        DefinirDocumentosExigidosCommand comando = new(cenario.Processo.Id,
            [RegraDeComprovacaoDeTeste.DeDocumento(entrada).Construir(), RegraDeComprovacaoDeTeste.DeDocumento(entrada).Construir()]);

        (await HandleAsync(cenario.Mocks, comando)).IsSuccess.Should().BeTrue();

        cenario.Processo.DocumentosExigidos.Select(static documento => documento.Chave.Valor)
            .Should().Equal("exigencia_identidade", "exigencia_identidade_2");
        cenario.Processo.DocumentosExigidos.First().Id.Should().Be(anterior.Id);
    }

    [Fact]
    public async Task Handle_ExigenciaEmGrupo_ReconheceAChave()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        (await cenario.DefinirAsync(cenario.ItemGeral())).IsSuccess.Should().BeTrue();
        DocumentoExigido anterior = cenario.Processo.DocumentosExigidos.Single();
        ItemDocumentoExigidoInput entrada = cenario.ItemGeral().Construir() with { Chave = anterior.Chave.Valor };
        RegraDeComprovacaoInformada reconhecida = RegraDeComprovacaoDeTeste.DeDocumento(entrada).Construir();
        RegraDeComprovacaoInformada nova = RegraDeComprovacaoDeTeste.DeDocumento(cenario.ItemGeral()).Construir();
        DefinirDocumentosExigidosCommand comando = new(cenario.Processo.Id,
            [RegraDeComprovacaoDeTeste.Grupo("TODOS_OS_ITENS", reconhecida, nova).Construir()]);

        (await HandleAsync(cenario.Mocks, comando)).IsSuccess.Should().BeTrue();

        cenario.Processo.DocumentosExigidos.Should().Contain(documento => documento.Id == anterior.Id && documento.Chave == anterior.Chave);
    }

}
