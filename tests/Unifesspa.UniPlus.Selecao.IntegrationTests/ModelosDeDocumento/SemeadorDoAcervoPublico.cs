namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Security.Cryptography;
using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Grava o que a divulgação do ato lê: o processo, o modelo e os documentos confirmados, cada um com a cópia
/// selada no bucket privado, e as versões e atos que os congelam.
/// </summary>
internal sealed class SemeadorDoAcervoPublico(AcervoPublicoDeTeste acervo)
{
    /// <summary>Um PDF mínimo; o texto distingue editais diferentes pelo conteúdo, e portanto pelo hash.</summary>
    public static byte[] ArquivoDeEdital(string texto = "edital") => Encoding.ASCII.GetBytes($"%PDF-1.4\n% {texto}\n%%EOF\n");

    public async Task<(ProcessoSeletivo Processo, ModeloDeDocumento Modelo)> ProcessoComModeloAsync(byte[]? conteudo = null)
    {
        ProcessoSeletivo processo;
        await using (UniPlusDbContext context = acervo.Db.CreateDbContext())
        {
            processo = await ProcessoSeletivoDeTeste.GravarAsync(context, "PS 2027 — modelo no acervo");
        }

        return (processo, await ModeloAsync(processo.Id, conteudo ?? ArquivosDeModeloDeTeste.Odt()));
    }

    /// <summary>Um modelo confirmado, com a cópia selada no bucket privado, como a confirmação o deixa.</summary>
    public async Task<ModeloDeDocumento> ModeloAsync(Guid processoId, byte[] conteudo)
    {
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(processoId, nomeArquivo: "Autodeclaração étnico-racial");
        modelo.Confirmar(conteudo.LongLength, HashDe(conteudo), TimeProvider.System).IsSuccess.Should().BeTrue();
        await acervo.Storage().SalvarConteudoSeladoAsync(modelo.ObjectKeyConfirmado!, conteudo, modelo.ContentType);

        await using UniPlusDbContext context = acervo.Db.CreateDbContext();
        context.ModelosDeDocumento.Add(modelo);
        await context.SaveChangesAsync();
        return modelo;
    }

    /// <summary>Um documento do edital confirmado, com a cópia selada no bucket privado.</summary>
    public Task<DocumentoAPublicar> EditalAsync(Guid processoId, byte[]? conteudo = null) =>
        DocumentoConfirmadoAsync(
            processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, conteudo ?? ArquivoDeEdital());

    /// <summary>Um documento de ato confirmado, com a cópia selada no bucket privado.</summary>
    public Task<DocumentoAPublicar> DocumentoDoAtoAsync(Guid processoId, string nome, string tipoDeMidia, byte[] conteudo) =>
        DocumentoConfirmadoAsync(processoId, FinalidadeDoDocumento.DocumentoDeAto, nome, tipoDeMidia, conteudo);

    /// <summary>Um anexo do edital: o documento confirmado, com a cópia selada no bucket privado, e o anexo congelado dele.</summary>
    public async Task<AnexoSemeado> AnexoAsync(Guid processoId, string nome, string tipoDeMidia, byte[] conteudo)
    {
        DocumentoAPublicar documento = await DocumentoConfirmadoAsync(processoId, FinalidadeDoDocumento.AnexoDoEdital, nome, tipoDeMidia, conteudo);
        return new AnexoSemeado(AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, nome).Value!, conteudo);
    }

    /// <summary>Um anexo em PDF; o texto distingue anexos diferentes pelo conteúdo, e portanto pelo hash.</summary>
    public Task<AnexoSemeado> AnexoPdfAsync(Guid processoId, string nome, string texto) =>
        AnexoAsync(processoId, nome, TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital(texto));

    public async Task<VersaoConfiguracao> VersaoAsync(
        ProcessoSeletivo processo,
        ModeloDeDocumento modelo,
        Guid ato,
        string? identificador = null,
        DocumentoAPublicar? edital = null,
        string? hashDoEditalCongelado = null,
        IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        edital ??= await EditalAsync(processo.Id);
        identificador ??= IdentificadoresDeTeste.NovoValor();
        byte[] envelope = EnvelopeDeReferenciaComModelo.Montar(
            modelo, identificador, edital.Id, hashDoEditalCongelado ?? edital.HashSha256!, anexos);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id, envelope, CorpusEnvelope.Codec.SchemaVersion, CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: ato, atoCriadorHash: new string('a', 64), atorUsuarioSub: "teste", instante: DateTimeOffset.UtcNow);

        return await PublicarAsync(processo, versao, AcervoPublicoDeTeste.TipoDoAto, identificador, edital);
    }

    public async Task<VersaoConfiguracao> RetificacaoAsync(
        VersaoConfiguracao anterior,
        ModeloDeDocumento modelo,
        Guid ato,
        string identificador,
        DocumentoAPublicar? edital = null,
        IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        edital ??= await EditalAsync(anterior.ProcessoSeletivoId);
        byte[] envelope = EnvelopeDeReferenciaComModelo.Montar(modelo, identificador, edital.Id, edital.HashSha256!, anexos);
        VersaoConfiguracao versao = VersaoConfiguracao.Suceder(
            anterior, envelope, CorpusEnvelope.Codec.SchemaVersion, CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: ato, atoCriadorHash: new string('b', 64), atoCriadorRetificaId: anterior.AtoCriadorId,
            atorUsuarioSub: "teste", instante: DateTimeOffset.UtcNow);

        await using UniPlusDbContext leitura = acervo.Db.CreateDbContext();
        ProcessoSeletivo processo = await leitura.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == anterior.ProcessoSeletivoId);

        return await PublicarAsync(processo, versao, AcervoPublicoDeTeste.Retificacao, identificador, edital);
    }

    /// <summary>A versão que o ato de abertura cria, já divulgada: o edital e os anexos foram copiados para o acervo.</summary>
    public async Task<VersaoConfiguracao> AberturaDivulgadaAsync(
        ProcessoSeletivo processo,
        ModeloDeDocumento modelo,
        Guid ato,
        string identificador,
        DocumentoAPublicar? edital = null,
        IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        VersaoConfiguracao versao = await VersaoAsync(processo, modelo, ato, identificador, edital, anexos: anexos);
        await acervo.CopiarAoAcervoAsync(ato);
        return versao;
    }

    /// <summary>A versão que o ato de retificação cria, já divulgada e copiada para o acervo.</summary>
    public async Task<VersaoConfiguracao> RetificacaoDivulgadaAsync(
        VersaoConfiguracao anterior,
        ModeloDeDocumento modelo,
        Guid ato,
        string identificador,
        DocumentoAPublicar? edital = null,
        IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        VersaoConfiguracao versao = await RetificacaoAsync(anterior, modelo, ato, identificador, edital, anexos);
        await acervo.CopiarAoAcervoAsync(ato);
        return versao;
    }

    public async Task<AtoDoProcesso> AtoDoProcessoAsync(
        Guid processoId, string identificador, Guid ato, DocumentoAPublicar principal, DocumentoAPublicar? anexo = null)
    {
        AtoDoProcesso registro = AtoDoProcesso.Registrar(
            DeclaracaoDoAto(processoId, AcervoPublicoDeTeste.TipoAvulso, identificador, ato, principal, anexo), TimeProvider.System.GetUtcNow()).Value!;
        await using UniPlusDbContext context = acervo.Db.CreateDbContext();
        context.AtosDoProcesso.Add(registro);
        CertameDivulgado? divulgado = await context.CertamesDivulgados.SingleOrDefaultAsync(c => c.Id == processoId);
        divulgado?.RegistrarEditalPendente(ato, numeroVersao: null);
        await context.SaveChangesAsync();
        return registro;
    }

    /// <summary>Um processo com o edital de abertura já divulgado: o que o ato avulso exige para ter onde entrar.</summary>
    public async Task<(ProcessoSeletivo Processo, string Identificador)> CertameDivulgadoAsync()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await ProcessoComModeloAsync();
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7();
        await VersaoAsync(processo, modelo, abertura, identificador);
        await acervo.CopiarAoAcervoAsync(abertura);
        return (processo, identificador);
    }

    /// <summary>
    /// A publicação como a Seleção a grava: a versão, o ato que a criou e a divulgação do certame com a entrada do ato
    /// ainda sem endereço, num mesmo commit. A cópia ao acervo vem depois.
    /// </summary>
    private async Task<VersaoConfiguracao> PublicarAsync(
        ProcessoSeletivo processo, VersaoConfiguracao versao, string tipoDoAto, string identificador, DocumentoAPublicar edital)
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            DeclaracaoDoAto(processo.Id, tipoDoAto, identificador, versao.AtoCriadorId, edital), versao.VigenteAPartirDe).Value!;
        await using UniPlusDbContext context = acervo.Db.CreateDbContext();
        context.VersoesConfiguracao.Add(versao);
        context.AtosDoProcesso.Add(ato);
        GravacaoDaDivulgacao divulgacao = new(new CertameDivulgadoRepository(context), AcervoDeTeste.Endereco);

        Result montada = await divulgacao.MontarDaVersaoAsync(processo, versao, ato, CancellationToken.None);

        montada.IsSuccess.Should().BeTrue(montada.Error?.Message);
        await context.SaveChangesAsync();
        return versao;
    }

    private static NovoAtoDoProcesso DeclaracaoDoAto(
        Guid processoId, string tipoDoAto, string identificador, Guid ato, DocumentoAPublicar principal, DocumentoAPublicar? anexo = null) =>
        new(
            ato,
            processoId,
            tipoDoAto,
            tipoDoAto,
            Numero: null,
            new DateOnly(AcervoPublicoDeTeste.AnoDoAto, 10, 1),
            "Diretoria do CEPS",
            AcervoPublicoDeTeste.UnidadeDoEnvelope,
            identificador,
            principal.Id,
            anexo is null ? [] : [new AnexoDeclarado(anexo.Id, "Relação de inscritos")]);

    private static string HashDe(byte[] conteudo) => Convert.ToHexStringLower(SHA256.HashData(conteudo));

    private async Task<DocumentoAPublicar> DocumentoConfirmadoAsync(
        Guid processoId, FinalidadeDoDocumento finalidade, string nome, string tipoDeMidia, byte[] conteudo)
    {
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, finalidade: finalidade, nomeArquivo: nome, tipoDeMidia: tipoDeMidia);
        documento.Confirmar(conteudo.LongLength, HashDe(conteudo), TimeProvider.System).IsSuccess.Should().BeTrue();
        await acervo.Storage().SalvarConteudoSeladoAsync(documento.ObjectKeyConfirmado!, conteudo, tipoDeMidia);

        await using UniPlusDbContext context = acervo.Db.CreateDbContext();
        context.DocumentosAPublicar.Add(documento);
        await context.SaveChangesAsync();
        return documento;
    }
}

/// <summary>Um anexo do edital semeado, com os bytes que foram selados para ele.</summary>
internal sealed record AnexoSemeado(AnexoDoEdital Anexo, byte[] Conteudo);
