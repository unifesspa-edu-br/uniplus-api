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

    [Fact(DisplayName = "O conjunto básico publicado recusa o nascimento depois do envio com os limites do dia, sem gravar o rascunho")]
    public async Task Salvar_SobreVersaoFixadaReal_RecusaONascimentoNoFuturo()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Salvar_SobreVersaoFixadaReal_RecusaONascimentoNoFuturo));

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        InscricaoRequest request = new(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2027-01-10") },
            null,
            null);
        Result<InscricaoDto> salvar = await _cenario.SalvarAsync(
            writeContext, processoId, request, iniciar.Value!.ETag, Substitute.For<IConferenciaEnderecoGeo>());

        salvar.Errors.Should().ContainSingle(static e => e.Field == "DATA_NASCIMENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data entre 15/01/1906 e 15/01/2026.");

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        Inscricao? gravada = await _cenario.NovoRepositorioDeInscricao(readContext)
            .ObterPorIdAsync(iniciar.Value!.Id);
        gravada!.Respostas.Should().NotContainKey("DATA_NASCIMENTO", "a resposta recusada não é gravada no rascunho");
    }

    [Fact(DisplayName = "A confirmação do CEP de faixa fica gravada no endereço da inscrição e volta na leitura")]
    public async Task Salvar_EnderecoComCepConfirmado_GravaEReleAConfirmacao()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Salvar_EnderecoComCepConfirmado_GravaEReleAConfirmacao));

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        InscricaoRepository inscricaoRepository = _cenario.NovoRepositorioDeInscricao(writeContext);
        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.KevinCarvalho));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Inscricao inscricao = (await inscricaoRepository.ObterPorIdAsync(iniciar.Value!.Id))!;
        RespostasAdmitidas admitidas = (await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, processoId, ProcessoSeletivoRepositoryDeTeste.Criar(writeContext), RegistroCodecs, ofertaCursoId: null, null, CancellationToken.None))
            .Value!.Admitidas with
        { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond };

        Result salvar = inscricao.Salvar(
            null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
                {
                    cep = "68627476",
                    logradouro = "Rua Brigadeiro Eduardo Gomes",
                    bairro = "Laércio Cabeline",
                    cidade = new { codigoIbge = "1505502", nome = "Paragominas", uf = "PA" },
                    cepConfirmado = true,
                }),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            [],
            admitidas,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]));
        salvar.IsSuccess.Should().BeTrue(salvar.Error?.Message);

        await inscricaoRepository.AtualizarAsync(inscricao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        InscricaoRepository readRepository = _cenario.NovoRepositorioDeInscricao(readContext);
        Inscricao lida = (await readRepository.ObterPorIdAsync(inscricao.Id))!;
        lida.Respostas["ENDERECO_RESIDENCIAL"].GetProperty("cepConfirmado").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Sobre a versão fixada, a inscrição grava o telefone do conjunto básico só com os dígitos")]
    public async Task Telefone_SobreVersaoFixada_GravaSoOsDigitos()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Telefone_SobreVersaoFixada_GravaSoOsDigitos));

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            iniciar.Value!.VersaoFormularioId, processoId, ProcessoSeletivoRepositoryDeTeste.Criar(readContext), RegistroCodecs, ofertaCursoId: null, null, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);

        InscricaoRepository repositorio = _cenario.NovoRepositorioDeInscricao(readContext);
        Inscricao inscricao = (await repositorio.ObterPorIdAsync(iniciar.Value!.Id))!;
        inscricao.Salvar(
            null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["TELEFONE"] = JsonSerializer.SerializeToElement("(94) 3322-1234") },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            [], contexto.Value!.Admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();

        inscricao.Respostas["TELEFONE"].GetString().Should().Be("9433221234",
            "o formato do telefone congelado na versão chega à definição da resposta, que grava a forma canônica");
    }

    [Fact(DisplayName = "O rascunho guarda a resposta ao item que outra resposta esconde, e o envio a descarta da inscrição gravada e do comprovante, que continua íntegro")]
    public async Task Envio_SobreVersaoFixadaReal_DescartaARespostaDoItemOculto()
    {
        FatoColetado precisa = FatoColetado.Criar(
            "PRECISA_ATENDIMENTO", 1, "Você precisa de atendimento especializado?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        FatoColetado condicao = FatoColetado.Criar(
            "CONDICAO_DESCRITA", 2, "Descreva a sua condição", TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, "PRECISA_ATENDIMENTO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            classificacaoProtecao: "SENSIVEL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Envio_SobreVersaoFixadaReal_DescartaARespostaDoItemOculto), camposAdicionais: [precisa, condicao]);

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);
        InscricaoRepository inscricaoRepository = _cenario.NovoRepositorioDeInscricao(writeContext);
        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Inscricao inscricao = (await inscricaoRepository.ObterPorIdAsync(iniciar.Value!.Id))!;
        ContextoDaVersaoFixada contexto = (await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, processoId, ProcessoSeletivoRepositoryDeTeste.Criar(writeContext), RegistroCodecs, ofertaCursoId: null, null, CancellationToken.None))
            .Value!;

        // O candidato disse "sim", descreveu a condição e voltou para "não": a descrição fica no rascunho.
        inscricao.Salvar(
            null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["PRECISA_ATENDIMENTO"] = JsonSerializer.SerializeToElement(false),
                ["CONDICAO_DESCRITA"] = JsonSerializer.SerializeToElement("Lactante"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], contexto.Admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond },
            PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        await inscricaoRepository.AtualizarAsync(inscricao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        await using UniPlusDbContext envioContext = _cenario.CriarDbContext();
        InscricaoRepository envioRepository = _cenario.NovoRepositorioDeInscricao(envioContext);
        Inscricao rascunho = (await envioRepository.ObterPorIdAsync(inscricao.Id))!;
        rascunho.Respostas.Should().ContainKey("CONDICAO_DESCRITA", "o rascunho guarda a resposta oculta, para o candidato poder voltar atrás");

        ResultadoDaAvaliacaoDaInscricao avaliacao = AvaliacaoDaInscricao.Avaliar(contexto.Configuracao, new EntradaDaAvaliacaoDaInscricao(
            rascunho.Respostas,
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            AvaliacaoDaInscricao.CodigosDasEtapasDaInscricao(contexto.Configuracao.Definicao),
            Envio: null, Apresentacoes: EntradaDaAvaliacaoDaInscricao.SemApresentacoes)).Value!;
        ComprovanteInscricao comprovante = rascunho.Submeter(
            "000901", contexto.Admitidas.OfertasDeCurso.Single(), null, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], contexto.Admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, avaliacao,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), DerivadosDoSistemaInscricao.DefinicoesDasRespostas,
            PrecondicaoIfMatch.DeTags([rascunho.ETag]), DentroDoPeriodo.GetUtcNow(), []).Value!;
        await envioRepository.AtualizarAsync(rascunho, CancellationToken.None);
        await envioRepository.AdicionarComprovanteAsync(comprovante, CancellationToken.None);
        await envioContext.SaveChangesAsync(CancellationToken.None);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        InscricaoRepository leitura = _cenario.NovoRepositorioDeInscricao(readContext);
        Inscricao enviada = (await leitura.ObterPorIdAsync(inscricao.Id))!;
        enviada.Respostas.Should().ContainKey("PRECISA_ATENDIMENTO").And.NotContainKey("CONDICAO_DESCRITA");
        ComprovanteInscricao gravado = (await leitura.ObterComprovantesAsync([inscricao.Id], CancellationToken.None)).Single();
        gravado.Respostas.Should().NotContainKey("CONDICAO_DESCRITA");
        gravado.IntegridadeConfere().Should().BeTrue("o hash cobre só o que foi gravado");
    }
}
