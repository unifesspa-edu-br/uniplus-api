namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessoComMatrizDaLeiDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>As recusas do pedido de salvar e enviar sobre a versão publicada: fato só de grupo no nível superior, grupo e ocorrência nulos, aceites malformados, repetidos, fora do formulário e de outra versão.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class RecusasDoPedidoDeInscricaoTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public RecusasDoPedidoDeInscricaoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "Salvar e enviar sobre a versão publicada recusam, no nível superior, o fato que a versão só coleta dentro de um grupo")]
    public async Task Resposta_DeSubitemDeGrupoNoNivelSuperior_ERecusada()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Resposta_DeSubitemDeGrupoNoNivelSuperior_ERecusada));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true));
        pedido = ComSubitemDeGrupoNoNivelSuperior(pedido);

        Result<InscricaoDto> salvar = await _cenario.SalvarAsync(db, processoId, pedido, etag);
        Result<ReciboDaInscricaoDto> envio = await _cenario.EnviarAsync(db, processoId, pedido, etag, "00000103");

        ConferirRecusaDeFatoSoDeGrupo(salvar, envio);
    }

    [Fact(DisplayName = "Salvar e enviar recusam a lista de grupo nula e a ocorrência nula com o caminho do campo, sem lançar")]
    public async Task Grupo_ComListaOuOcorrenciaNula_ERecusado()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Grupo_ComListaOuOcorrenciaNula_ERecusado));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true));
        InscricaoRequest listaNula = ComComposicaoFamiliar(pedido, null!);
        InscricaoRequest ocorrenciaNula = ComComposicaoFamiliar(pedido, [null!]);

        Result<InscricaoDto> salvarListaNula = await _cenario.SalvarAsync(db, processoId, listaNula, etag);
        Result<InscricaoDto> salvarOcorrenciaNula = await _cenario.SalvarAsync(db, processoId, ocorrenciaNula, etag);
        Result<ReciboDaInscricaoDto> enviarListaNula = await _cenario.EnviarAsync(db, processoId, listaNula, etag, "00000104");
        Result<ReciboDaInscricaoDto> enviarOcorrenciaNula = await _cenario.EnviarAsync(db, processoId, ocorrenciaNula, etag, "00000105");

        ConferirRecusaDeGrupoMalFormado(salvarListaNula, salvarOcorrenciaNula, enviarListaNula, enviarOcorrenciaNula);
    }

    [Fact(DisplayName = "Salvar e enviar recusam o aceite nulo e o aceite sem código ou sem hash com o caminho do campo, sem lançar")]
    public async Task Aceite_NuloOuSemCodigoOuSemHash_ERecusado()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Aceite_NuloOuSemCodigoOuSemHash_ERecusado));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true));
        (InscricaoRequest aceiteNulo, InscricaoRequest semCodigoNemHash) = AceitesMalFormados(pedido);

        Result<InscricaoDto> salvarNulo = await _cenario.SalvarAsync(db, processoId, aceiteNulo, etag);
        Result<InscricaoDto> salvarIncompleto = await _cenario.SalvarAsync(db, processoId, semCodigoNemHash, etag);
        Result<ReciboDaInscricaoDto> enviarNulo = await _cenario.EnviarAsync(db, processoId, aceiteNulo, etag, "00000106");
        Result<ReciboDaInscricaoDto> enviarIncompleto = await _cenario.EnviarAsync(db, processoId, semCodigoNemHash, etag, "00000107");

        ConferirRecusaDeAceiteMalFormado(salvarNulo, salvarIncompleto, enviarNulo, enviarIncompleto);
    }

    [Fact(DisplayName = "Salvar e enviar recusam o aceite repetido, o de termo fora do formulário e o de outra versão, conferidos contra o termo congelado")]
    public async Task Aceite_RepetidoForaDoFormularioOuDeOutraVersao_ERecusado()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Aceite_RepetidoForaDoFormularioOuDeOutraVersao_ERecusado));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true));
        (InscricaoRequest repetido, InscricaoRequest foraDoFormulario, InscricaoRequest outraVersao) = AceitesInvalidos(pedido);

        Result<InscricaoDto> salvarRepetido = await _cenario.SalvarAsync(db, processoId, repetido, etag);
        Result<InscricaoDto> salvarForaDoFormulario = await _cenario.SalvarAsync(db, processoId, foraDoFormulario, etag);
        Result<InscricaoDto> salvarOutraVersao = await _cenario.SalvarAsync(db, processoId, outraVersao, etag);
        Result<ReciboDaInscricaoDto> enviarRepetido = await _cenario.EnviarAsync(db, processoId, repetido, etag, "00000108");
        Result<ReciboDaInscricaoDto> enviarForaDoFormulario = await _cenario.EnviarAsync(db, processoId, foraDoFormulario, etag, "00000109");

        ConferirRecusaDeAceitesInvalidos(salvarRepetido, salvarForaDoFormulario, salvarOutraVersao, enviarRepetido, enviarForaDoFormulario);
    }

    private static void ConferirRecusaDeFatoSoDeGrupo(Result<InscricaoDto> salvar, Result<ReciboDaInscricaoDto> envio)
    {
        salvar.Errors.Should().ContainSingle().Which.Field.Should().Be("PARENTESCO");
        salvar.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
        envio.Errors.Should().ContainSingle().Which.Field.Should().Be("PARENTESCO");
        envio.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
    }

    private static void ConferirRecusaDeGrupoMalFormado(Result<InscricaoDto> salvarListaNula, Result<InscricaoDto> salvarOcorrenciaNula,
        Result<ReciboDaInscricaoDto> enviarListaNula, Result<ReciboDaInscricaoDto> enviarOcorrenciaNula)
    {
        salvarListaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR");
        enviarListaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR");
        salvarOcorrenciaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR[0]");
        enviarOcorrenciaNula.Errors.Should().ContainSingle().Which.Field.Should().Be("COMPOSICAO_FAMILIAR[0]");
        new[] { salvarListaNula.Error, salvarOcorrenciaNula.Error, enviarListaNula.Error, enviarOcorrenciaNula.Error }
            .Should().OnlyContain(static e => e!.Code == InscricaoErrorCodes.GrupoMalFormado);
    }

    private static void ConferirRecusaDeAceiteMalFormado(Result<InscricaoDto> salvarNulo, Result<InscricaoDto> salvarIncompleto,
        Result<ReciboDaInscricaoDto> enviarNulo, Result<ReciboDaInscricaoDto> enviarIncompleto)
    {
        salvarNulo.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[0]");
        enviarNulo.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[0]");
        salvarIncompleto.Errors.Select(static e => e.Field).Should().BeEquivalentTo("aceites[0].codigo", "aceites[1].hashVersao");
        enviarIncompleto.Errors.Select(static e => e.Field).Should().BeEquivalentTo("aceites[0].codigo", "aceites[1].hashVersao");
        new[] { salvarNulo.Error, salvarIncompleto.Error, enviarNulo.Error, enviarIncompleto.Error }
            .Should().OnlyContain(static e => e!.Code == InscricaoErrorCodes.AceiteMalFormado);
    }

    private static void ConferirRecusaDeAceitesInvalidos(Result<InscricaoDto> salvarRepetido, Result<InscricaoDto> salvarForaDoFormulario, Result<InscricaoDto> salvarOutraVersao,
        Result<ReciboDaInscricaoDto> enviarRepetido, Result<ReciboDaInscricaoDto> enviarForaDoFormulario)
    {
        salvarRepetido.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        salvarRepetido.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteRepetido);
        enviarRepetido.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        enviarRepetido.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteRepetido);
        salvarForaDoFormulario.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        salvarForaDoFormulario.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteForaDoFormulario);
        enviarForaDoFormulario.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteForaDoFormulario);
        salvarOutraVersao.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[0]");
        salvarOutraVersao.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteDeOutraVersao);
    }

    private static (InscricaoRequest Nulo, InscricaoRequest Incompleto) AceitesMalFormados(InscricaoRequest pedido)
    {
        InscricaoRequest nulo = pedido with { Aceites = [null!] };
        InscricaoRequest incompleto = pedido with
        {
            Aceites = [new AceiteDeTermoInput(null!, Guid.NewGuid(), "hash"), new AceiteDeTermoInput("LGPD", Guid.NewGuid(), null!)],
        };
        return (nulo, incompleto);
    }

    private static (InscricaoRequest Repetido, InscricaoRequest ForaDoFormulario, InscricaoRequest OutraVersao) AceitesInvalidos(InscricaoRequest pedido)
    {
        AceiteDeTermoInput valido = pedido.Aceites![0];
        InscricaoRequest repetido = pedido with { Aceites = [valido, valido with { HashVersao = "hash-forjado" }] };
        InscricaoRequest foraDoFormulario = pedido with { Aceites = [valido, new AceiteDeTermoInput("TERMO_INEXISTENTE", Guid.NewGuid(), "hash")] };
        InscricaoRequest outraVersao = pedido with { Aceites = [valido with { VersaoId = Guid.NewGuid() }] };
        return (repetido, foraDoFormulario, outraVersao);
    }

    private static InscricaoRequest ComComposicaoFamiliar(InscricaoRequest pedido, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput> ocorrencias) => pedido with
    {
        Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal) { ["COMPOSICAO_FAMILIAR"] = ocorrencias },
    };

    private static InscricaoRequest ComSubitemDeGrupoNoNivelSuperior(InscricaoRequest pedido)
    {
        Dictionary<string, JsonElement> comSubitemSolto = new(pedido.Respostas!, StringComparer.Ordinal)
        {
            ["PARENTESCO"] = JsonSerializer.SerializeToElement("Mãe"),
        };
        return pedido with { Respostas = comSubitemSolto };
    }
}
