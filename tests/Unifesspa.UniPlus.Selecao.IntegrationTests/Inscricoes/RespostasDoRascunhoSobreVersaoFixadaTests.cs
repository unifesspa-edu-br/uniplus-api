namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>O que o rascunho grava, recusa e descarta ao responder o formulário da versão fixada: nascimento fora dos limites, confirmação do CEP, telefone só com dígitos e item oculto no envio.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class RespostasDoRascunhoSobreVersaoFixadaTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public RespostasDoRascunhoSobreVersaoFixadaTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "O rascunho recusa o nascimento posterior ao envio e não grava a resposta")]
    public async Task Salvar_SobreVersaoFixadaReal_RecusaONascimentoNoFuturo()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Salvar_SobreVersaoFixadaReal_RecusaONascimentoNoFuturo));
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.FernandaDrumond);
        Result<InscricaoDto> salvar = await _cenario.SalvarAsync(escrita, processoId, PedidoComResposta("DATA_NASCIMENTO", "2027-01-10"),
            iniciar.ETag, Substitute.For<IConferenciaEnderecoGeo>());
        salvar.Errors.Should().ContainSingle(static e => e.Field == "DATA_NASCIMENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data entre 15/01/1906 e 15/01/2026.");
        await ConferirRespostaAusenteAsync(iniciar.Id, "DATA_NASCIMENTO");
    }

    private static InscricaoRequest PedidoComResposta(string fato, string valor) =>
        new(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [fato] = JsonSerializer.SerializeToElement(valor) }, null, null);

    private async Task ConferirRespostaAusenteAsync(Guid inscricaoId, string fato)
    {
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        Inscricao? inscricao = await _cenario.NovoRepositorioDeInscricao(leitura).ObterPorIdAsync(inscricaoId);
        inscricao!.Formulario.Preenchimento.Respostas.Should().NotContainKey(fato);
    }

    [Fact(DisplayName = "O rascunho grava e relê a confirmação do CEP de faixa")]
    public async Task Salvar_EnderecoComCepConfirmado_GravaEReleAConfirmacao()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Salvar_EnderecoComCepConfirmado_GravaEReleAConfirmacao));
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.KevinCarvalho);
        InscricaoRepository repositorio = _cenario.NovoRepositorioDeInscricao(escrita);
        Inscricao inscricao = (await repositorio.ObterPorIdAsync(iniciar.Id))!;
        ContextoDaVersaoFixada contexto = await ContextoAsync(escrita, inscricao.Formulario.VersaoFormularioId, processoId);
        SalvarEnderecoConfirmado(inscricao, contexto.Admitidas);
        await repositorio.AtualizarAsync(inscricao, CancellationToken.None);
        await escrita.SaveChangesAsync(CancellationToken.None);
        await ConferirCepConfirmadoAsync(inscricao.Id);
    }

    private static void SalvarEnderecoConfirmado(Inscricao inscricao, RespostasAdmitidas admitidas)
    {
        Result salvo = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["ENDERECO_RESIDENCIAL"] = EnderecoConfirmado() }, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([inscricao.ETag]));
        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);
    }

    private static JsonElement EnderecoConfirmado() => JsonSerializer.SerializeToElement(new
    {
        cep = "68627476",
        logradouro = "Rua Brigadeiro Eduardo Gomes",
        bairro = "Laércio Cabeline",
        cidade = new { codigoIbge = "1505502", nome = "Paragominas", uf = "PA" },
        cepConfirmado = true,
    });

    private async Task ConferirCepConfirmadoAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        Inscricao inscricao = (await _cenario.NovoRepositorioDeInscricao(leitura).ObterPorIdAsync(inscricaoId))!;
        inscricao.Formulario.Preenchimento.Respostas["ENDERECO_RESIDENCIAL"].GetProperty("cepConfirmado").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "O telefone da versão fixada é gravado apenas com dígitos")]
    public async Task Telefone_SobreVersaoFixada_GravaSoOsDigitos()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Telefone_SobreVersaoFixada_GravaSoOsDigitos));
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.FernandaDrumond);
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        ContextoDaVersaoFixada contexto = await ContextoAsync(leitura, iniciar.Formulario.VersaoFormularioId, processoId);
        Inscricao inscricao = (await _cenario.NovoRepositorioDeInscricao(leitura).ObterPorIdAsync(iniciar.Id))!;
        SalvarTelefone(inscricao, contexto.Admitidas);
        inscricao.Formulario.Preenchimento.Respostas["TELEFONE"].GetString().Should().Be("9433221234");
    }

    private static void SalvarTelefone(Inscricao inscricao, RespostasAdmitidas admitidas) =>
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["TELEFONE"] = JsonSerializer.SerializeToElement("(94) 3322-1234") }, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "O envio retira a resposta oculta do rascunho e do comprovante, preservando a integridade")]
    public async Task Envio_SobreVersaoFixadaReal_DescartaARespostaDoItemOculto()
    {
        ProcessoSeletivo processo = ProcessoComCondicao();
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.FernandaDrumond);
        Inscricao inscricao = (await _cenario.NovoRepositorioDeInscricao(escrita).ObterPorIdAsync(iniciar.Id))!;
        ContextoDaVersaoFixada contexto = await ContextoAsync(escrita, inscricao.Formulario.VersaoFormularioId, processoId);
        await SalvarCondicaoOcultaAsync(escrita, inscricao, contexto);
        await EnviarOcultandoAsync(inscricao.Id, contexto);
        await ConferirEnvioSemRespostaOcultaAsync(inscricao.Id);
    }

    private static ProcessoSeletivo ProcessoComCondicao() =>
        NovoProcessoDeInscricao(nameof(Envio_SobreVersaoFixadaReal_DescartaARespostaDoItemOculto), camposAdicionais: [PrecisaDeAtendimento(), DescricaoDaCondicao()]);

    private static FatoColetado PrecisaDeAtendimento() => FatoColetado.Criar(
        "PRECISA_ATENDIMENTO", 1, "Você precisa de atendimento especializado?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
        classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static FatoColetado DescricaoDaCondicao() => FatoColetado.Criar(
        "CONDICAO_DESCRITA", 2, "Descreva a sua condição", TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
        [CondicaoPrecondicaoFato.Criar(0, "PRECISA_ATENDIMENTO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
        classificacaoProtecao: "SENSIVEL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
        finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private async Task SalvarCondicaoOcultaAsync(UniPlusDbContext db, Inscricao inscricao, ContextoDaVersaoFixada contexto)
    {
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["PRECISA_ATENDIMENTO"] = JsonSerializer.SerializeToElement(false),
            ["CONDICAO_DESCRITA"] = JsonSerializer.SerializeToElement("Lactante"),
        }, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), contexto.Admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        await _cenario.NovoRepositorioDeInscricao(db).AtualizarAsync(inscricao, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task EnviarOcultandoAsync(Guid inscricaoId, ContextoDaVersaoFixada contexto)
    {
        await using UniPlusDbContext envio = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.NovoRepositorioDeInscricao(envio);
        Inscricao inscricao = (await repositorio.ObterPorIdAsync(inscricaoId))!;
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("CONDICAO_DESCRITA");
        ComprovanteInscricao comprovante = EmitirComprovante(inscricao, contexto);
        await repositorio.AtualizarAsync(inscricao, CancellationToken.None);
        await _cenario.NovoRepositorioDeComprovantes(envio).AdicionarComprovanteAsync(comprovante, CancellationToken.None);
        await envio.SaveChangesAsync(CancellationToken.None);
    }

    private static ComprovanteInscricao EmitirComprovante(Inscricao inscricao, ContextoDaVersaoFixada contexto)
    {
        ResultadoDaAvaliacaoDaInscricao avaliacao = AvaliacaoDaInscricao.Avaliar(contexto.Configuracao, new EntradaDaAvaliacaoDaInscricao(
            inscricao.Formulario.Preenchimento.Respostas, new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), AvaliacaoDaInscricao.CodigosDasEtapasDaInscricao(contexto.Configuracao.Definicao),
            Envio: null, Apresentacoes: EntradaDaAvaliacaoDaInscricao.SemApresentacoes)).Value!;
        return inscricao.Submeter("000901",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(contexto.Admitidas.OfertasDeCurso.Single(), null), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), contexto.Admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond },
            new ApuracaoDoEnvio(avaliacao, new Dictionary<string, JsonElement>(StringComparer.Ordinal), DerivadosDoSistemaInscricao.DefinicoesDasRespostas, [], null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), DentroDoPeriodo.GetUtcNow()).Value!;
    }

    private async Task ConferirEnvioSemRespostaOcultaAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.NovoRepositorioDeInscricao(leitura);
        Inscricao inscricao = (await repositorio.ObterPorIdAsync(inscricaoId))!;
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("PRECISA_ATENDIMENTO").And.NotContainKey("CONDICAO_DESCRITA");
        ComprovanteInscricao comprovante = (await _cenario.NovoRepositorioDeComprovantes(leitura).ObterComprovantesAsync([inscricaoId], CancellationToken.None)).Single();
        comprovante.Respostas.Should().NotContainKey("CONDICAO_DESCRITA");
        comprovante.IntegridadeConfere().Should().BeTrue();
    }
}
