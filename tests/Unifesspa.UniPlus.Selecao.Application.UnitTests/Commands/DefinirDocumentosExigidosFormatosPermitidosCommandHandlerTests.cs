namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// Os formatos permitidos do documento exigido: a forma do valor, o token QUALQUER, a lista de formatos, o teto por formato e a substituição integral no reenvio.
/// </summary>
public sealed class DefinirDocumentosExigidosFormatosPermitidosCommandHandlerTests
{
    [Fact(DisplayName = "Story #918: reenviar o PUT com FormatosPermitidos/TamanhoMaximoBytes diferentes substitui integralmente (não faz merge)")]
    public async Task Handle_ReenviarComFormatoETamanhoDiferentes_SubstituiIntegralmente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement listaPdf = JsonSerializer.SerializeToElement(new[] { "PDF" });
        ItemDocumentoExigidoInput primeiro = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, listaPdf, 5_000_000);
        (await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", primeiro, null, null, null, null)], PrecondicaoIfMatch.Ausente)))
            .IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Single().FormatosPermitidos.Qualquer.Should().BeFalse();
        processo.DocumentosExigidos.Single().FormatosPermitidos.Lista!.Single().Formato.Should().Be(FormatoPermitido.Pdf);
        processo.DocumentosExigidos.Single().TamanhoMaximoBytes.Should().Be(5_000_000);

        JsonElement listaJpeg = JsonSerializer.SerializeToElement(new[] { "JPEG" });
        ItemDocumentoExigidoInput segundo = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, listaJpeg, 2_000_000);
        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", segundo, null, null, null, null)], PrecondicaoIfMatch.Curinga));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.FormatosPermitidos.Lista!.Single().Formato.Should().Be(
            FormatoPermitido.Jpeg, "substituição integral, sem vestígio do valor anterior");
        exigencia.TamanhoMaximoBytes.Should().Be(2_000_000);
    }

    [Fact(DisplayName = "FormatosPermitidos ausente (JsonElement? null) retorna FormatosPermitidos.Obrigatorio")]
    public async Task Handle_FormatosPermitidosAusente_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    [Theory(DisplayName = "FormatosPermitidos em forma inválida (ValueKind inesperado ou string diferente de QUALQUER) retorna FormatosPermitidos.FormaInvalida")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"PDF\"")]
    [InlineData("{}")]
    public async Task Handle_FormatosPermitidosFormaInvalida_RetornaErroNomeado(string valorJson)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement valor = JsonDocument.Parse(valorJson).RootElement;
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, valor, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormaInvalida");
    }

    [Fact(DisplayName = "FormatosPermitidos = QUALQUER é aceito")]
    public async Task Handle_FormatosPermitidosQualquer_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Single().FormatosPermitidos.Qualquer.Should().BeTrue();
    }

    [Fact(DisplayName = "FormatosPermitidos com lista {PDF,JPEG,PNG} (itens-texto simples) congela os 3 formatos, sem teto por formato")]
    public async Task Handle_FormatosPermitidosListaDeStrings_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement lista = JsonSerializer.SerializeToElement(new[] { "PDF", "JPEG", "PNG" });
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormatosPermitidos formatosPermitidos = processo.DocumentosExigidos.Single().FormatosPermitidos;
        formatosPermitidos.Qualquer.Should().BeFalse();
        formatosPermitidos.Lista!.Select(e => e.Formato).Should().BeEquivalentTo(
            [FormatoPermitido.Pdf, FormatoPermitido.Jpeg, FormatoPermitido.Png]);
        formatosPermitidos.Lista!.Should().AllSatisfy(e => e.TamanhoMaximoBytesMax.Should().BeNull());
    }

    [Fact(DisplayName = "FormatosPermitidos com item {formato, tamanhoMaximoBytesMax} congela o teto por formato")]
    public async Task Handle_FormatosPermitidosComTetoPorFormato_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement lista = JsonSerializer.SerializeToElement(new[]
        {
            new { formato = "PDF", tamanhoMaximoBytesMax = 5_000_000 },
        });
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormatoPermitidoEntry entrada = processo.DocumentosExigidos.Single().FormatosPermitidos.Lista!.Single();
        entrada.Formato.Should().Be(FormatoPermitido.Pdf);
        entrada.TamanhoMaximoBytesMax.Should().Be(5_000_000);
    }

    [Theory(DisplayName = "tamanhoMaximoBytesMax presente e malformado é recusado com erro nomeado, nunca 500")]
    [InlineData("\"5MB\"")]
    [InlineData("true")]
    [InlineData("5000000.5")]
    [InlineData("99999999999999999999")]
    public async Task Handle_FormatosPermitidosTamanhoMaximoBytesMaxMalformado_RetornaErroNomeado(string tamanhoJson)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement lista = JsonDocument.Parse($$"""[{"formato": "PDF", "tamanhoMaximoBytesMax": {{tamanhoJson}}}]""").RootElement;
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormaInvalida");
    }

    [Fact(DisplayName = "Um array contendo a string \"QUALQUER\" NÃO é o token especial — é lista com um formato desconhecido")]
    public async Task Handle_FormatosPermitidosQualquerComLista_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        // "QUALQUER" só é reconhecido como o token especial quando a forma é STRING — um
        // array contendo a string "QUALQUER" é só mais um item de lista (um formato
        // desconhecido, na prática), não o token. O cenário de exclusividade mútua
        // (FormatosPermitidos.QualquerComFormatosEspecificos) é coberto diretamente no VO
        // (FormatosPermitidosTests) — o handler não tem como produzir os dois braços ao
        // mesmo tempo a partir de um único JsonElement.
        JsonElement lista = JsonSerializer.SerializeToElement(new[] { "QUALQUER" });
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormatoInvalido");
    }
}
