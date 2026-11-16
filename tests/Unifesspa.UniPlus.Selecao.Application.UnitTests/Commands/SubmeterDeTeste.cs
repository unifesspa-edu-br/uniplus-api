namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os cenários e as portas que as classes de teste do envio da inscrição compartilham.</summary>
internal static class SubmeterDeTeste
{
    internal static readonly Guid ProcessoId = Guid.CreateVersion7();

    internal static Inscricao NovaInscricao(Cpf cpf, string contaId, Guid versaoFormularioId) =>
        Inscricao.Iniciar(ProcessoId, cpf, contaId, "https://issuer", versaoFormularioId).Value!;

    internal static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1", string? nomeSocial = null)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.NomeCompleto.Returns(PersonasDeTeste.NomeFernandaDrumond);
        contexto.NomeSocial.Returns(nomeSocial);
        contexto.UserId.Returns(contaId);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }

    internal static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Inscricao, ComprovanteInscricao> Emitidos = [];

    /// <summary>O comprovante que o envio gravou para a inscrição: o recibo não o repete, quem o lê é a consulta.</summary>
    internal static ComprovanteInscricao Emitido(Inscricao inscricao) =>
        Emitidos.TryGetValue(inscricao, out ComprovanteInscricao? comprovante) ? comprovante : throw new InvalidOperationException("O envio não gravou comprovante.");

    internal static void Guardar(Inscricao inscricao, IInscricaoRepository repositorio)
    {
        ComprovanteInscricao? gravado = repositorio.ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IInscricaoRepository.AdicionarComprovanteAsync))
            .Select(static c => (ComprovanteInscricao)c.GetArguments()[0]!)
            .LastOrDefault();
        if (gravado is not null)
        {
            Emitidos.AddOrUpdate(inscricao, gravado);
        }
    }

    internal static Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(
        SubmeterInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IConferenciaEnderecoGeo? geo) =>
        SubmeterAsync(command, inscricaoRepository, processoSeletivoRepository, registroCodecs, unitOfWork, userContext, new PortasDoEnvioDeTeste(Geo: geo));

    internal static Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(
        SubmeterInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        PortasDoEnvioDeTeste? portas = null) =>
        SubmeterInscricaoCommandHandler.Handle(
            command,
            new InscricaoAtivaDoCandidato(inscricaoRepository),
            new PeriodoDaInscricao(DivulgacaoDeTeste.Certame(ProcessoId), processoSeletivoRepository, registroCodecs, DivulgacaoDeTeste.Fuso()),
            EnvioDe(inscricaoRepository, processoSeletivoRepository, registroCodecs, unitOfWork, portas ?? new PortasDoEnvioDeTeste()),
            userContext, portas?.Relogio ?? DivulgacaoDeTeste.Relogio, CancellationToken.None);

    private static EnvioDaInscricao EnvioDe(
        IInscricaoRepository inscricoes,
        IProcessoSeletivoRepository processos,
        IRegistroCodecsEnvelope codecs,
        IUnitOfWork unitOfWork,
        PortasDoEnvioDeTeste portas)
    {
        IDocumentoDaInscricaoRepository documentos = portas.Documentos ?? Substitute.For<IDocumentoDaInscricaoRepository>();
        return new EnvioDaInscricao(
            new PreparoDoEnvio(new VersaoDaInscricao(DivulgacaoDeTeste.Certame(ProcessoId), processos, codecs)),
            new AvaliacaoDoEnvio(portas.Geo ?? ConferenciaQueAceitaTudo(), DivulgacaoDeTeste.Fuso(), documentos),
            new GravacaoDoEnvio(inscricoes, documentos, portas.Gerador ?? GeradorQueDevolve("00000001"), new GravacaoDaInscricao(inscricoes, unitOfWork)));
    }

    internal static IGeradorDeNumeroInscricao GeradorQueDevolve(string numero)
    {
        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();
        gerador.ProximoAsync(Arg.Any<CancellationToken>()).Returns(numero);
        return gerador;
    }

    internal static IConferenciaEnderecoGeo ConferenciaQueAceitaTudo()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        conferencia.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        conferencia.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Success(new CepResolvido("1501402", "Marabá", "PA", "logradouro")));
        return conferencia;
    }

    /// <summary>
    /// As respostas de TODO o conjunto básico do candidato (25 fatos, UNI-REQ-0144): toda inscrição
    /// coleta esses fatos na seção reservada, então o veredito de submissão só passa quando todos
    /// os obrigatórios estão preenchidos — não só o item que o teste declara além do básico.
    /// Escolhas que mantêm os condicionais fora de obrigatoriedade: nacionalidade NATO (sem
    /// documento de estrangeiro), órgão emissor da lista (sem o nome do outro órgão), não deseja nome
    /// social, tipo de endereço urbano (sem comunidade).
    /// </summary>
    internal static Dictionary<string, JsonElement> RespostasDoConjuntoBasico(Cpf cpf) => new(StringComparer.Ordinal)
    {
        ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
        ["NACIONALIDADE"] = JsonSerializer.SerializeToElement("NATO"),
        ["CPF"] = JsonSerializer.SerializeToElement(cpf.Valor),
        ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
        ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement("SSP"),
        ["RG_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["RG_DATA_EMISSAO"] = JsonSerializer.SerializeToElement("2010-01-01"),
        ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2000-01-01"),
        ["NATURALIDADE_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["NATURALIDADE_MUNICIPIO"] = JsonSerializer.SerializeToElement("1501402"),
        ["NOME_MAE"] = JsonSerializer.SerializeToElement("Maria Drumond"),
        ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("SOLTEIRO"),
        ["SEXO"] = JsonSerializer.SerializeToElement("FEMININO"),
        ["SEXO_CIVIL"] = JsonSerializer.SerializeToElement("FEMININO"),
        ["IDENTIDADE_GENERO"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
        ["ORIENTACAO_SEXUAL"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
        ["COR_RACA"] = JsonSerializer.SerializeToElement("PARDA"),
        ["EMAIL"] = JsonSerializer.SerializeToElement("fernanda.drumond@example.com"),
        ["TELEFONE"] = JsonSerializer.SerializeToElement("91999999999"),
        ["TIPO_ENDERECO"] = JsonSerializer.SerializeToElement("URBANO"),
        ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
        {
            cep = "66010000",
            cidade = new { codigoIbge = "1501402", uf = "PA" },
        }),
    };

    /// <summary>
    /// Um item obrigatório na seção do formulário, mais um termo obrigatório exigido na mesma
    /// finalidade; <paramref name="outrosItens"/> e <paramref name="grupos"/> entram na mesma seção, e
    /// <paramref name="ajustar"/> muda o processo antes de a versão o congelar.
    /// </summary>
    internal static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo, VersaoTermoEscolhida Termo) MontarVersaoComItemObrigatorioETermo(
        IReadOnlyList<FatoColetado>? outrosItens = null, IReadOnlyList<GrupoColetado>? grupos = null, Action<ProcessoSeletivo>? ajustar = null,
        Func<ProcessoSeletivo, (IReadOnlyList<DocumentoExigido> Documentos, IReadOnlyList<NoExigencia> Nos)>? exigencias = null)
    {
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

        VersaoTermoEscolhida versaoDoTermo = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Termo de veracidade", "Declaro que as informações são verdadeiras.",
            "Lei 9.784/1999", "ACEITE_EXPLICITO", new string('b', 64));
        TermoExigidoFormulario termo = TermoExigidoFormulario.Criar(
            "TERMO_VERACIDADE", 0, versaoDoTermo, exibicao: null, Obrigatoriedade.Sempre, FinalidadeFormulario.Inscricao).Value!;

        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Submeter inscrição");
        Result itensDefinidos = processoBase.DefinirItens([item, .. outrosItens ?? []], PrecondicaoIfMatch.Ausente, grupos: grupos);
        itensDefinidos.IsSuccess.Should().BeTrue(itensDefinidos.Error?.Message);
        processoBase.DefinirTermosDoFormulario(FinalidadeFormulario.Inscricao, [termo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "o termo não cita fato nenhum, então não tem citação pendente de garantir");
        ajustar?.Invoke(processoBase);

        (IReadOnlyList<DocumentoExigido> documentos, IReadOnlyList<NoExigencia> nos) =
            exigencias?.Invoke(processoBase) ?? ([.. processoBase.DocumentosExigidos], []);
        GrafoConfiguracao grafo = new(
            [.. processoBase.Etapas], processoBase.AtendimentoEspecializado!, [.. processoBase.DistribuicaoVagas],
            processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
            [.. processoBase.CronogramaFases], documentos, nos, null,
            fatosColetados: [.. processoBase.FatosColetados],
            formularios: [.. processoBase.Formularios],
            termosExigidos: [.. processoBase.TermosExigidos],
            gruposColetados: [.. processoBase.GruposColetados],
            numeroDeOpcoesDeCurso: 1);

        // documentosExigidos.dataReferenciaFatos congelada: o conjunto básico traz DATA_NASCIMENTO,
        // que FAIXA_ETARIA apura contra esta data, nunca a de hoje.
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":"2026-12-10"}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-conta-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        return (versao, grafo, versaoDoTermo);
    }

    internal static InscricaoRequest RequestComApelidoETermoAceito(
        Cpf cpf, VersaoTermoEscolhida termo, GrafoConfiguracao grafo, string apelido = "Nanda")
    {
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement(apelido);
        return new(OfertaDo(grafo), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);
    }

    internal static Guid OfertaDo(GrafoConfiguracao grafo) => grafo.DistribuicaoVagas[0].OfertaCursoOrigemId;

    internal static async Task<Result<ReciboDaInscricaoDto>> SubmeterNaOfertaAsync(
        Cpf cpf, VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo, Inscricao inscricao, Guid ofertaEnviada,
        Guid? segundaOpcaoEnviada = null)
    {
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(inscricao);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        // A candidata responde o que faz valer a modalidade de ação afirmativa da oferta B.
        InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, grafo) with
        {
            OfertaCursoId = ofertaEnviada,
            OfertaCursoSegundaOpcaoId = segundaOpcaoEnviada,
        };
        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([inscricao.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf));
        Guardar(inscricao, inscricaoRepository);
        return resultado;
    }

    internal static async Task<(Result<ReciboDaInscricaoDto> Resultado, IUnitOfWork UnitOfWork)> SubmeterComEnderecoAsync(IConferenciaEnderecoGeo geo, string? cep = "66010000")
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep, cidade = new { codigoIbge = "1501402", uf = "PA" } });
        InscricaoRequest request = new(OfertaDo(grafo), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), geo);
        return (resultado, unitOfWork);
    }

    /// <summary>Um booleano que, respondido "não", esconde o que depende dele.</summary>
    internal static FatoColetado Pergunta(string fato, int ordem) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    /// <summary>Um texto opcional que só aparece com <paramref name="pergunta"/> respondida "sim".</summary>
    internal static FatoColetado SoComSim(string fato, int ordem, string pergunta) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
        [CondicaoPrecondicaoFato.Criar(0, pergunta, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
        classificacaoProtecao: "SENSIVEL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    internal static async Task<(Result<ReciboDaInscricaoDto> Resultado, Inscricao Inscricao)> SubmeterComAsync(
        IReadOnlyList<FatoColetado> itens,
        IReadOnlyList<GrupoColetado>? grupos,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? respostasDosGrupos = null,
        Action<ProcessoSeletivo>? ajustar = null,
        string? nomeSocialDaConta = null,
        IConferenciaEnderecoGeo? geo = null,
        IReadOnlyCollection<string>? remover = null)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(itens, grupos, ajustar);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        InscricaoRequest comApelido = RequestComApelidoETermoAceito(cpf, termo, grafo);
        Dictionary<string, JsonElement> todas = new(comApelido.Respostas!, StringComparer.Ordinal);
        foreach (string fato in remover ?? [])
        {
            todas.Remove(fato);
        }

        foreach ((string fato, JsonElement valor) in respostas)
        {
            todas[fato] = valor;
        }

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, comApelido with { Respostas = todas, Grupos = respostasDosGrupos }, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf, nomeSocial: nomeSocialDaConta), geo);
        Guardar(existente, inscricaoRepository);
        return (resultado, existente);
    }

    /// <summary>A ampla concorrência só para quem quer concorrer às vagas de pessoa com deficiência: a modalidade decide pela resposta.</summary>
    internal static void AmplaSoComDeficiencia(ProcessoSeletivo processo) =>
        processo.DefinirDistribuicaoVagas(
            [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(PredicadoDnf.CriarDeCondicoesAgrupadas(
                [(0, CondicaoDnf.Criar("CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!)],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    internal static readonly FatoColetado DataAteOEnvio = FatoColetado.Criar(
        "DATA_DO_EVENTO", 1, "Data do evento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
        restricoes: [new FaixaDeData(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0))], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    internal static InscricaoRequest RequestComDataDoEvento(Cpf cpf, VersaoTermoEscolhida termo, GrafoConfiguracao grafo, string dataDoEvento)
    {
        InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, grafo);
        Dictionary<string, JsonElement> respostas = new(request.Respostas!, StringComparer.Ordinal)
        {
            ["DATA_DO_EVENTO"] = JsonSerializer.SerializeToElement(dataDoEvento),
        };
        return request with { Respostas = respostas };
    }

    /// <summary>Um relógio cuja leitura seguinte é sempre mais tarde que a anterior, pelo passo.</summary>
    internal sealed class RelogioQueAvanca(DateTimeOffset inicio, TimeSpan passo) : TimeProvider
    {
        private int _leituras;

        public override DateTimeOffset GetUtcNow() => inicio + (passo * _leituras++);
    }

    internal const string Belem = "1501402";
    internal const string Salvador = "2927408";

    /// <summary>O Geo que conhece Belém no Pará e o CEP de Belém usado nas respostas básicas.</summary>
    internal static FakeConferenciaEnderecoGeo GeoQueConhece() =>
        new FakeConferenciaEnderecoGeo().ComMunicipio(Belem, "PA", "Belém").ComCep("66010000", Belem, "PA", cidade: "Belém");

    internal static Dictionary<string, JsonElement> Texto(params (string Fato, string Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal);

    internal static GrupoColetado GrupoMunicipal() => GrupoColetado.Criar(
        "LOCAIS", 1, FormularioDeTeste.Secao, "Locais", minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Nunca,
        [FatoColetado.Criar(
            "LOCAL_MUNICIPIO", 0, "Município", TipoRenderizacao.Municipio, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao,
            restricoes: [new MunicipiosDaUf("NATURALIDADE_UF")]).Value!],
        FinalidadeFormulario.Inscricao).Value!;

    internal static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> OcorrenciaMunicipal(string codigo) =>
        new(StringComparer.Ordinal) { ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", codigo)))] };

    internal static FatoColetado ObrigatorioSoParaQuemMoraEm(string municipio) => FatoColetado.Criar(
        "OUTRA_REGIAO", 7, "Outra região", TipoRenderizacao.Texto,
        Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement(municipio)).Value!)]).Value!),
        null, classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
        finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
}
