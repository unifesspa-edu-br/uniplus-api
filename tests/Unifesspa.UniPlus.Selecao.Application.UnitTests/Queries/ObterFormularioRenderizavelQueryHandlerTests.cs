namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura do <see cref="ObterFormularioRenderizavelQueryHandler"/> (Story #559/#1059): a
/// distinção 404/422/200, a guarda contra versão vigente congelada ANTES de a apresentação do
/// formulário — ou os valores selecionáveis (UNI-REQ-0072) — existirem no envelope, e a recusa de
/// uma <c>SchemaVersion</c> aposentada (issue #1089) mesmo quando os bytes coincidem com a forma
/// atual. Sem <see cref="Unifesspa.UniPlus.Selecao.Infrastructure"/> disponível aqui (Application
/// não a alcança), a projeção lê o JSON cru e tem de recusar com um erro nomeado, nunca estourar,
/// quando as chaves novas (rotulo/tipoRenderizacao/obrigatorio/formulario/valoresSelecionaveis)
/// não existem, ou quando <c>valoresSelecionaveis</c> descumpre a bicondicional com
/// <c>tipoRenderizacao</c>.
/// </summary>
public sealed class ObterFormularioRenderizavelQueryHandlerTests
{
    /// <summary>
    /// A versão que o registro de codecs FAKE (<see cref="RegistroReconhecendoVersaoCorrente"/>)
    /// declara como única capacidade de leitura — o caso corrente de todo teste que não é sobre o
    /// gate de versão em si.
    /// </summary>
    private const string VersaoCorrenteReconhecida = "0.0.7";

    /// <summary>
    /// Uma versão que já foi corrente e deixou de ser reconhecida quando o codec vivo avançou —
    /// usada SÓ no teste que prova a recusa (issue #1089); nenhum outro teste deste arquivo rotula
    /// o caso corrente com ela.
    /// </summary>
    private const string VersaoAposentada = "0.0.6";

    private static readonly TimeProvider Relogio = TimeProvider.System;

    /// <summary>
    /// Substituto da porta reconhecendo, como única capacidade de leitura, exatamente
    /// <see cref="VersaoCorrenteReconhecida"/> — o mesmo perfil do registro de produção, que hoje
    /// tem um único codec vivo (ADR-0110 Emenda 2).
    /// </summary>
    private static readonly IRegistroCodecsEnvelope RegistroReconhecendoVersaoCorrente =
        CriarRegistroReconhecendo(VersaoCorrenteReconhecida);

    private static Task<Result<FormularioRenderizavelDto>> HandleAsync(
        IProcessoSeletivoRepository repository, Guid processoId) =>
        ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId),
            repository,
            RegistroReconhecendoVersaoCorrente,
            Relogio,
            CancellationToken.None);

    private static IRegistroCodecsEnvelope CriarRegistroReconhecendo(params string[] schemaVersionsReconhecidas)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns(schemaVersionsReconhecidas
            .Select(static v => new CapacidadeCodec(v, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null))
            .ToList());
        return registro;
    }

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
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":"true","precondicao":null,"valoresSelecionaveis":[]}]}""")]
    [InlineData(
        // "rotulo" presente mas null — chave existe, valor não é o esperado.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":null,"tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":null,"valoresSelecionaveis":[]}]}""")]
    [InlineData(
        // "precondicao" presente com tipo errado (objeto em vez de array) — não pode virar
        // silenciosamente "sem pré-condição", que mudaria a semântica do campo.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":{},"valoresSelecionaveis":[]}]}""")]
    [InlineData(
        // "valoresSelecionaveis" null num fato de seleção — descumpre a bicondicional (issue
        // #1059): SELECAO_UNICA/SELECAO_MULTIPLA exige array, nunca null.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":null,"valoresSelecionaveis":null}]}""")]
    [InlineData(
        // "valoresSelecionaveis" ausente — envelope congelado antes de a chave existir.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":null}]}""")]
    [InlineData(
        // "tipoRenderizacao" fora dos quatro tokens fechados, com valoresSelecionaveis null — o
        // decoder converteria o token em TipoRenderizacao.Nenhuma e FatoColetado.Criar recusaria;
        // sem esta guarda aqui, o token desconhecido cairia no ramo "não é seleção" por omissão.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"TEXTO","obrigatorio":false,"precondicao":null,"valoresSelecionaveis":null}]}""")]
    [InlineData(
        // "ordem" negativa dentro de um item de valoresSelecionaveis — o decoder recusa.
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":null,"valoresSelecionaveis":[{"valorCodigo":"BRANCA","descricao":null,"ordem":-1}]}]}""")]
    [InlineData(
        // "valorCodigo" repetido no array — o decoder recusa (o encoder nunca emite duas entradas
        // para o mesmo valor).
        """{"formulario":{"titulo":null,"termoAceiteTexto":null},"fatosColetados":[{"fatoCodigo":"COR_RACA","ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatorio":false,"precondicao":null,"valoresSelecionaveis":[{"valorCodigo":"BRANCA","descricao":null,"ordem":0},{"valorCodigo":"BRANCA","descricao":null,"ordem":1}]}]}""")]
    public async Task Handle_EnvelopeComValorIncoerente_RecusaSemEstourar(string envelopeJson)
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeJson);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Fact(DisplayName = "Versão vigente congelada com a forma corrente projeta título/termo/fatos com apresentação e valores selecionáveis")]
    public async Task Handle_EnvelopeCorrente_ProjetaApresentacao()
    {
        const string envelopeCorrente = """
            {
              "formulario": {"titulo": "Formulário de Inscrição", "termoAceiteTexto": null},
              "fatosColetados": [
                {
                  "fatoCodigo": "COR_RACA", "ordem": 0, "rotulo": "Cor ou raça",
                  "tipoRenderizacao": "SELECAO_UNICA", "obrigatorio": true, "precondicao": null,
                  "valoresSelecionaveis": [
                    {"valorCodigo": "BRANCA", "descricao": "Autodeclaração de cor/raça branca.", "ordem": 0},
                    {"valorCodigo": "PRETA", "descricao": "Autodeclaração de cor/raça preta.", "ordem": 1}
                  ]
                }
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeCorrente);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Titulo.Should().Be("Formulário de Inscrição");
        resultado.Value!.TermoAceiteTexto.Should().BeNull();
        FatoFormularioRenderizavelDto fato = resultado.Value!.FatosColetados.Should().ContainSingle().Which;
        fato.FatoCodigo.Should().Be("COR_RACA");
        fato.Rotulo.Should().Be("Cor ou raça");
        fato.TipoRenderizacao.Should().Be("SELECAO_UNICA");
        fato.Obrigatorio.Should().BeTrue();
        fato.ValoresSelecionaveis.Should().SatisfyRespectively(
            primeiro =>
            {
                primeiro.Codigo.Should().Be("BRANCA");
                primeiro.Ordem.Should().Be(0);
            },
            segundo =>
            {
                segundo.Codigo.Should().Be("PRETA");
                segundo.Ordem.Should().Be(1);
            });
    }

    /// <summary>
    /// O teste central da issue #1089: a versão é aposentada, mas o JSON congelado tem a forma
    /// ATUAL e é válido — exatamente o cenário em que decidir pela forma (o que o handler fazia
    /// antes desta correção) mascara a recusa que o registro de codecs já dá em outras
    /// superfícies (ex.: <c>AbrirRetificacaoCommandHandler</c>). Um envelope malformado não
    /// provaria nada aqui: o handler já o recusava antes, por <c>FormularioInscricao.VersaoSemApresentacao</c>.
    /// O que só esta correção resolve é a versão desconhecida com bytes que "passariam" no shape.
    /// </summary>
    [Fact(DisplayName = "Versão aposentada com bytes de forma corrente recusa com EnvelopeCodec.VersaoDesconhecida, sem tentar decidir pela forma")]
    public async Task Handle_VersaoAposentadaComFormaCorrente_RetornaVersaoDesconhecida()
    {
        const string envelopeFormaCorrente = """
            {
              "formulario": {"titulo": "Formulário de Inscrição", "termoAceiteTexto": null},
              "fatosColetados": []
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeFormaCorrente, VersaoAposentada);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue(
            $"a versão '{VersaoAposentada}' deixou de ser reconhecida quando o codec vivo avançou para " +
            $"'{VersaoCorrenteReconhecida}' — bytes coincidentemente válidos na forma atual não a devolvem " +
            "à lista de capacidades reconhecidas");
        resultado.Error!.Code.Should().Be("EnvelopeCodec.VersaoDesconhecida");
    }

    /// <summary>
    /// Prova ESTRUTURAL (não comportamental) de que a leitura pública nunca reconsulta o
    /// catálogo: nenhum parâmetro do <c>Handle</c> é um leitor cross-módulo do catálogo de fatos
    /// (<c>IFatoCandidatoReader</c>) — os valores selecionáveis entregues vêm inteiramente do
    /// envelope congelado já persistido em <see cref="VersaoConfiguracao.ConfiguracaoCongelada"/>,
    /// nunca de uma releitura do cadastro vivo. É essa ausência estrutural, e não um mock que
    /// "não é chamado", que garante que alterar/inativar/remover descrições no catálogo vivo
    /// nunca muda a resposta deste endpoint.
    /// </summary>
    [Fact(DisplayName = "O grafo de dependências do Handle não alcança IFatoCandidatoReader — a leitura pública é imune ao catálogo vivo")]
    public void Handle_NaoDependeDeLeitorDeCatalogo()
    {
        System.Reflection.MethodInfo handle = typeof(ObterFormularioRenderizavelQueryHandler)
            .GetMethod(nameof(ObterFormularioRenderizavelQueryHandler.Handle))!;

        handle.GetParameters().Select(static p => p.ParameterType.Name).Should().NotContain(
            "IFatoCandidatoReader",
            "os valores selecionáveis do formulário público vêm do envelope congelado, nunca de uma releitura do " +
            "catálogo vivo — se o Handle injetasse o leitor cross-módulo, alterar/inativar/remover uma descrição " +
            "no cadastro mudaria a resposta de uma versão já publicada, o que a imutabilidade do envelope proíbe");
    }

    private static IProcessoSeletivoRepository MockComVersaoVigente(
        Guid processoId, string envelopeJson, string schemaVersion = VersaoCorrenteReconhecida)
    {
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId,
            Encoding.UTF8.GetBytes(envelopeJson),
            schemaVersion,
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
