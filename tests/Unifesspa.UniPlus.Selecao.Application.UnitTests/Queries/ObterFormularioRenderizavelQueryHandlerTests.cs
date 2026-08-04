namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura do <see cref="ObterFormularioRenderizavelQueryHandler"/> (Story #559): a
/// distinção 404/422/200, e a guarda contra versão vigente congelada ANTES de a apresentação do
/// formulário existir no envelope — sem <see cref="Unifesspa.UniPlus.Selecao.Infrastructure"/>
/// disponível aqui (Application não a alcança), a projeção lê o JSON cru e tem de recusar com um
/// erro nomeado, nunca estourar, quando as chaves novas (rotulo/tipoRenderizacao/obrigatorio/
/// formulario) não existem.
/// </summary>
public sealed class ObterFormularioRenderizavelQueryHandlerTests
{
    private static readonly TimeProvider Relogio = TimeProvider.System;

    private static Task<Result<FormularioRenderizavelDto>> HandleAsync(
        IProcessoSeletivoRepository repository, Guid processoId) =>
        ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId), repository, Relogio, CancellationToken.None);

    [Fact(DisplayName = "Processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns((VersaoConfiguracao?)null);
        repository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Processo existente sem versão vigente retorna Snapshot.VigenteAusente")]
    public async Task Handle_SemVersaoVigente_RetornaVigenteAusente()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns((VersaoConfiguracao?)null);
        repository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
    }

    [Fact(DisplayName = "Versão vigente congelada ANTES desta Story (sem formulario/campos novos) recusa com FormularioInscricao.VersaoSemApresentacao, nunca estoura")]
    public async Task Handle_EnvelopeAntigoSemApresentacao_RetornaErroNomeado()
    {
        // Forma real de uma VersaoConfiguracao congelada sob schema_version anterior a esta
        // Story: "formulario" é stub (nao_construido) e os itens de "fatosColetados" não têm
        // rotulo/tipoRenderizacao/obrigatorio — exatamente o que já existe hoje no banco
        // compartilhado de desenvolvimento (versões em 0.0.2/1.2/1.3/1.4).
        const string envelopeAntigo = """
            {
              "formulario": {"status": "nao_construido"},
              "fatosColetados": [
                {"fatoCodigo": "COR_RACA", "ordem": 0, "precondicao": null}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeAntigo);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue("um envelope sem as chaves novas não pode ser interpretado como formulário vazio nem estourar — é um estado nomeado");
        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Theory(DisplayName = "Envelope com valor de tipo/nulidade incoerente (só alcançável por linha adulterada) recusa com o mesmo erro nomeado, nunca estoura")]
    [InlineData(
        // "obrigatorio" como texto em vez de booleano.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":"true","precondicao":null}]}""")]
    [InlineData(
        // "rotulo" presente mas null — chave existe, valor não é o esperado.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":null,"tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":null}]}""")]
    [InlineData(
        // "precondicao" presente com tipo errado (objeto em vez de array) — não pode virar
        // silenciosamente "sem pré-condição", que mudaria a semântica do campo.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":{}}]}""")]
    public async Task Handle_EnvelopeComValorIncoerente_RecusaSemEstourar(string envelopeJson)
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeJson);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Fact(DisplayName = "Versão vigente congelada com a forma corrente projeta título/termo/fatos com apresentação")]
    public async Task Handle_EnvelopeCorrente_ProjetaApresentacao()
    {
        const string envelopeCorrente = """
            {
              "formulario": {"titulo": "Formulário de Inscrição", "termoAceiteTexto": null},
              "fatosColetados": [
                {"fatoCodigo": "COR_RACA", "ordem": 0, "rotulo": "Cor ou raça", "tipoRenderizacao": "SELECAO_UNICA", "obrigatorio": true, "precondicao": null}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeCorrente);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Titulo.Should().Be("Formulário de Inscrição");
        resultado.Value!.TermoAceiteTexto.Should().BeNull();
        FatoColetadoDto fato = resultado.Value!.FatosColetados.Should().ContainSingle().Which;
        fato.FatoCodigo.Should().Be("COR_RACA");
        fato.Rotulo.Should().Be("Cor ou raça");
        fato.TipoRenderizacao.Should().Be("SELECAO_UNICA");
        fato.Obrigatorio.Should().BeTrue();
    }

    private static IProcessoSeletivoRepository MockComVersaoVigente(Guid processoId, string envelopeJson)
    {
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId,
            Encoding.UTF8.GetBytes(envelopeJson),
            "0.0.4",
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            new string('a', 64),
            "user-sub-123",
            Relogio.GetUtcNow());

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(versao);
        return repository;
    }
}
