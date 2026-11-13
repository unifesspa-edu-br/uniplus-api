namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Grava as inscrições do cenário no processo já publicado, na versão que a publicação congelou: a enviada de Fernanda,
/// com um arquivo verificado em cada classificação e as respostas que o conjunto básico admite; o rascunho; a de Erick,
/// com o número do passaporte em claro para o repositório cifrar; a da servidora; e a que tem o grupo repetível.
/// </summary>
internal sealed class MontagemDasInscricoes(CascadingFixture fixture)
{
    private static readonly Cpf CpfDeErick = CpfDaPersona("Erick");
    private static readonly Cpf CpfDeHelena = CpfDaPersona("Helena");

    private static readonly Cpf CpfDoResponsavel = Cpf.Criar(PersonasDeTeste.Candidatas
        .First(static c => new[] { "Fernanda", "Erick", "Helena", "Kevin" }.All(n => !c.Nome.StartsWith(n, StringComparison.Ordinal))).Cpf).Value!;

    public async Task<CenarioDeAcessoAosDados> SemearAsync(ProcessoPublicado processo)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        IServiceProvider sp = escopo.ServiceProvider;
        IInscricaoRepository inscricoes = sp.GetRequiredService<IInscricaoRepository>();
        IDocumentoDaInscricaoRepository documentos = sp.GetRequiredService<IDocumentoDaInscricaoRepository>();
        (Guid fernanda, ArquivosDaInscricao arquivos) = await AdicionarFernandaAsync(inscricoes, documentos, processo);
        RascunhoDeInscricao rascunho = await AdicionarRascunhoAsync(inscricoes, documentos, processo);
        Guid erick = await AdicionarEnviadaAsync(inscricoes, processo, CpfDeErick, "sub-erick", FatosDoCenario.PassaporteDeErick);
        Guid servidora = await AdicionarEnviadaAsync(inscricoes, processo, CpfDeHelena, PersonasDoAcesso.Caroline, null);
        Guid comGrupo = await AdicionarComGrupoAsync(inscricoes, processo);
        await sp.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();

        return new CenarioDeAcessoAosDados(processo, fernanda, arquivos, rascunho, erick, servidora, comGrupo);
    }

    private static Cpf CpfDaPersona(string primeiroNome) =>
        Cpf.Criar(PersonasDeTeste.Candidatas.Single(c => c.Nome.StartsWith(primeiroNome, StringComparison.Ordinal)).Cpf).Value!;

    private static async Task<(Guid Id, ArquivosDaInscricao Arquivos)> AdicionarFernandaAsync(
        IInscricaoRepository inscricoes, IDocumentoDaInscricaoRepository documentos, ProcessoPublicado processo)
    {
        Inscricao inscricao = Inscricao.Iniciar(processo.Id, PersonasDeTeste.FernandaDrumond, "sub-fernanda", "https://idp.test", processo.VersaoId).Value!;
        Result<ComprovanteInscricao> submissao = Submeter(inscricao);
        submissao.IsSuccess.Should().BeTrue(string.Join("; ", submissao.Errors.Select(e => e.Error.Message)));
        await inscricoes.AdicionarAsync(inscricao);
        await inscricoes.AdicionarComprovanteAsync(submissao.Value!);
        return (inscricao.Id, await AdicionarArquivosAsync(documentos, inscricao.Id, processo));
    }

    private static Result<ComprovanteInscricao> Submeter(Inscricao inscricao) => inscricao.Submeter(
        "000001", null, null, RespostasDeFernanda(), SemGrupos(), [], AdmitidasDoNome(), FormularioDeTeste.AvaliacaoSemOcultos,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
        PrecondicaoIfMatch.DeTags([inscricao.ETag]), DateTimeOffset.UtcNow, []);

    private static async Task<ArquivosDaInscricao> AdicionarArquivosAsync(
        IDocumentoDaInscricaoRepository documentos, Guid inscricaoId, ProcessoPublicado processo)
    {
        Guid pessoal = await AdicionarArquivoDoTipoAsync(documentos, inscricaoId, processo, TiposDeDocumentoDoCenario.Pessoal);
        Guid sensivel = await AdicionarArquivoDoTipoAsync(documentos, inscricaoId, processo, TiposDeDocumentoDoCenario.Sensivel);
        Guid publico = await AdicionarArquivoDoTipoAsync(documentos, inscricaoId, processo, TiposDeDocumentoDoCenario.Publico);
        Guid interno = await AdicionarArquivoDoTipoAsync(documentos, inscricaoId, processo, TiposDeDocumentoDoCenario.Interno);
        Guid identificador = await AdicionarArquivoDoTipoAsync(documentos, inscricaoId, processo, TiposDeDocumentoDoCenario.Identificador);
        Guid naoResolvido = await AdicionarArquivoVerificadoAsync(documentos, inscricaoId, ExigenciaForaDaVersao(processo.FaseId));
        return new ArquivosDaInscricao(publico, interno, pessoal, identificador, sensivel, naoResolvido);
    }

    private static async Task<RascunhoDeInscricao> AdicionarRascunhoAsync(
        IInscricaoRepository inscricoes, IDocumentoDaInscricaoRepository documentos, ProcessoPublicado processo)
    {
        Inscricao rascunho = Inscricao.Iniciar(processo.Id, PersonasDeTeste.KevinCarvalho, "sub-kevin", "https://idp.test", processo.VersaoId).Value!;
        await inscricoes.AdicionarAsync(rascunho);
        Guid arquivo = await AdicionarArquivoDoTipoAsync(documentos, rascunho.Id, processo, TiposDeDocumentoDoCenario.Pessoal);
        return new RascunhoDeInscricao(rascunho.Id, arquivo);
    }

    /// <summary>Uma inscrição enviada, na versão publicada, com o CPF e, se houver, o número do passaporte em claro para o repositório cifrar.</summary>
    private static async Task<Guid> AdicionarEnviadaAsync(
        IInscricaoRepository inscricoes, ProcessoPublicado processo, Cpf cpf, string sub, string? passaporte)
    {
        Dictionary<string, JsonElement> respostas = RespostasDoCpf(cpf);
        Dictionary<string, DefinicaoDaResposta> definicoes = DefinicoesDoCpf();
        if (passaporte is not null)
        {
            respostas[FatosDoCenario.Identificador] = JsonSerializer.SerializeToElement(passaporte);
            definicoes[FatosDoCenario.Identificador] = IdentificadorEmTexto();
        }

        Inscricao inscricao = Submetida(processo, cpf, sub, "000002", respostas, definicoes, SemGrupos());
        await inscricoes.AdicionarAsync(inscricao);
        return inscricao.Id;
    }

    /// <summary>Uma inscrição enviada com duas ocorrências do grupo repetível, cada uma com o RG do responsável.</summary>
    private static async Task<Guid> AdicionarComGrupoAsync(IInscricaoRepository inscricoes, ProcessoPublicado processo)
    {
        Dictionary<string, DefinicaoDaResposta> definicoes = DefinicoesDoCpf();
        definicoes[processo.FatoDoGrupo] = IdentificadorEmTexto();

        Inscricao inscricao = Submetida(
            processo, CpfDoResponsavel, "sub-helena", "000003", RespostasDoCpf(CpfDoResponsavel), definicoes, OcorrenciasDosResponsaveis(processo.FatoDoGrupo));
        await inscricoes.AdicionarAsync(inscricao);
        return inscricao.Id;
    }

    private static Inscricao Submetida(
        ProcessoPublicado processo, Cpf cpf, string sub, string numero, Dictionary<string, JsonElement> respostas,
        Dictionary<string, DefinicaoDaResposta> definicoes, Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
        Inscricao.Reidratar(
            Guid.CreateVersion7(), processo.Id, cpf, sub, TestAuthHandler.DefaultIssuer, SituacaoInscricao.Submetida, processo.VersaoId, null, null,
            numero, null, null, null, respostas, new Dictionary<string, JsonElement>(StringComparer.Ordinal), grupos, [], definicoes,
            revisao: 1, DateTimeOffset.UtcNow, null);

    private static Dictionary<string, JsonElement> RespostasDoCpf(Cpf cpf) => new(StringComparer.Ordinal)
    {
        [ConjuntoBasicoDaInscricao.Cpf] = JsonSerializer.SerializeToElement(cpf.Valor),
    };

    private static Dictionary<string, JsonElement> RespostasDeFernanda() => new(StringComparer.Ordinal)
    {
        [ConjuntoBasicoDaInscricao.Nome] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        [ConjuntoBasicoDaInscricao.DesejaNomeSocial] = JsonSerializer.SerializeToElement(true),
        [ConjuntoBasicoDaInscricao.NomeSocial] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeSocialFernandaDrumond),
    };

    private static Dictionary<string, DefinicaoDaResposta> DefinicoesDoCpf() => new(StringComparer.Ordinal)
    {
        [ConjuntoBasicoDaInscricao.Cpf] = IdentificadorEmTexto(),
    };

    private static Dictionary<string, DefinicaoDaResposta> DefinicoesDoNome()
    {
        Dictionary<string, DefinicaoDaResposta> definicoes = DefinicoesDoCpf();
        definicoes[ConjuntoBasicoDaInscricao.Nome] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false);
        definicoes[ConjuntoBasicoDaInscricao.DesejaNomeSocial] = new(TipoDoValorDeResposta.Booleano, Multivalorado: false, Identificador: false);
        definicoes[ConjuntoBasicoDaInscricao.NomeSocial] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false);
        return definicoes;
    }

    private static DefinicaoDaResposta IdentificadorEmTexto() =>
        new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true);

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() =>
        new(StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> OcorrenciasDosResponsaveis(string fatoDoGrupo) => new(StringComparer.Ordinal)
    {
        [FatosDoCenario.GrupoDeResponsaveis] =
        [
            OcorrenciaDoResponsavel("membro-1", fatoDoGrupo, FatosDoCenario.RgDoPrimeiroResponsavel),
            OcorrenciaDoResponsavel("membro-2", fatoDoGrupo, FatosDoCenario.RgDoSegundoResponsavel),
        ],
    };

    private static OcorrenciaDeGrupoInscricao OcorrenciaDoResponsavel(string identificador, string fatoDoGrupo, string rg) =>
        new(identificador, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [fatoDoGrupo] = JsonSerializer.SerializeToElement(rg) });

    private static RespostasAdmitidas AdmitidasDoNome() => new(
        DefinicoesDoNome(),
        new HashSet<string>(StringComparer.Ordinal)
        {
            ConjuntoBasicoDaInscricao.Cpf, ConjuntoBasicoDaInscricao.Nome, ConjuntoBasicoDaInscricao.DesejaNomeSocial, ConjuntoBasicoDaInscricao.NomeSocial,
        },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
        new HashSet<Guid>(),
        new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal),
        PersonasDeTeste.NomeFernandaDrumond,
        PersonasDeTeste.NomeSocialFernandaDrumond);

    private static DocumentoExigido ExigenciaForaDaVersao(Guid faseId) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "COMPROVANTE_FORA_DA_VERSAO", "Comprovante", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal,
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
        [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;

    private static Task<Guid> AdicionarArquivoDoTipoAsync(
        IDocumentoDaInscricaoRepository documentos, Guid inscricaoId, ProcessoPublicado processo, string prefixoDoTipo) =>
        AdicionarArquivoVerificadoAsync(documentos, inscricaoId, processo.ExigenciaDoTipo(prefixoDoTipo));

    private static async Task<Guid> AdicionarArquivoVerificadoAsync(
        IDocumentoDaInscricaoRepository documentos, Guid inscricaoId, DocumentoExigido exigencia)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricaoId, exigencia, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricaoId, exigencia, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", TimeProvider.System).IsSuccess.Should().BeTrue();
        arquivo.RegistrarVerificado(1024, new string('c', 64), FormatoPermitido.Pdf).Should().BeTrue();
        await documentos.AdicionarApresentacaoAsync(apresentacao);
        await documentos.AdicionarArquivoAsync(arquivo);
        return arquivo.Id;
    }
}
