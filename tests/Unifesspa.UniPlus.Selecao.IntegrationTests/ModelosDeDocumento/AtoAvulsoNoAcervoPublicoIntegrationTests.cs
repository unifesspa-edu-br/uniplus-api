namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O ato que não cria versão de configuração (um aviso, por exemplo) no acervo público (ADR-0132), com Postgres e
/// MinIO reais: o principal e os anexos do ato são copiados para o acervo e registrados na divulgação do certame
/// que já existe. O acervo é provisionado antes de cada teste.
/// </summary>
public sealed class AtoAvulsoNoAcervoPublicoIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>, IAsyncLifetime
{
    private const string BucketPrivado = "uniplus-documentos-acervo-test";

    private readonly AcervoPublicoDeTeste _acervo;
    private readonly SemeadorDoAcervoPublico _semeador;

    public AtoAvulsoNoAcervoPublicoIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        _acervo = new AcervoPublicoDeTeste(dbFixture, minio, BucketPrivado);
        _semeador = new SemeadorDoAcervoPublico(_acervo);
    }

    public Task InitializeAsync() => _acervo.ProvisionarAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Ato que não cria versão copia o principal e os anexos para o acervo e os registra na divulgação do certame")]
    public async Task AtoQueNaoCriaVersao_CopiaOPrincipalEOsAnexos_ERegistraNaDivulgacao()
    {
        AtoAvulsoComPrincipalEAnexo avulso = await SemearAtoComPrincipalEAnexoAsync();

        await _acervo.CopiarAtoAvulsoAsync(avulso.Ato);

        await ConferirCopiasNoAcervoAsync(avulso);
        await ConferirRegistroNaDivulgacaoAsync(avulso);
    }

    [Fact(DisplayName = "Ato que não cria versão entra na divulgação do certame sem endereço, e nada é copiado ao acervo antes da cópia")]
    public async Task AtoQueNaoCriaVersao_AntesDaCopia_EntraSemEndereco()
    {
        AtoAvulsoComPrincipalEAnexo avulso = await SemearAtoComPrincipalEAnexoAsync();

        AtoDivulgado pendente = (await _acervo.AtosDivulgadosAsync(avulso.ProcessoId)).Single(e => e.AtoId == avulso.Ato);
        pendente.UrlDownload.Should().BeNull("o ato foi publicado, mas a cópia dos documentos ao acervo ainda não aconteceu");
        (await _acervo.ObjetosNoAcervoAsync(avulso.ChaveDoPrincipal)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Reentrega do ato que não cria versão não duplica o registro na divulgação")]
    public async Task AtoQueNaoCriaVersao_Reentrega_NaoDuplica()
    {
        (ProcessoSeletivo processo, string identificador) = await _semeador.CertameDivulgadoAsync();
        DocumentoAPublicar principal = await _semeador.DocumentoDoAtoAsync(
            processo.Id, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, SemeadorDoAcervoPublico.ArquivoDeEdital("aviso"));
        Guid ato = Guid.CreateVersion7();
        await _semeador.AtoDoProcessoAsync(processo.Id, identificador, ato, principal);

        await _acervo.CopiarAtoAvulsoAsync(ato);
        await _acervo.CopiarAtoAvulsoAsync(ato);

        (await _acervo.AtosDivulgadosAsync(processo.Id)).Count(e => e.AtoId == ato).Should().Be(1);
    }

    [Fact(DisplayName = "Ato sem registro do processo, de outro domínio, não é divulgado nem falha")]
    public async Task AtoSemRegistroDoProcesso_NaoETocado()
    {
        (ProcessoSeletivo processo, _) = await _semeador.CertameDivulgadoAsync();

        await _acervo.CopiarAtoAvulsoAsync(Guid.CreateVersion7());

        (await _acervo.AtosDivulgadosAsync(processo.Id)).Should().ContainSingle("só o edital que a divulgação já tinha");
    }

    private async Task<AtoAvulsoComPrincipalEAnexo> SemearAtoComPrincipalEAnexoAsync()
    {
        (ProcessoSeletivo processo, string identificador) = await _semeador.CertameDivulgadoAsync();
        byte[] docx = ArquivosDeModeloDeTeste.Docx();
        byte[] odt = ArquivosDeModeloDeTeste.Odt();
        DocumentoAPublicar principal = await _semeador.DocumentoDoAtoAsync(processo.Id, "aviso.docx", TiposDeMidiaDeDocumento.Docx, docx);
        DocumentoAPublicar anexo = await _semeador.DocumentoDoAtoAsync(processo.Id, "relacao.odt", TiposDeMidiaDeDocumento.Odt, odt);
        Guid ato = Guid.CreateVersion7();
        AtoDoProcesso registro = await _semeador.AtoDoProcessoAsync(processo.Id, identificador, ato, principal, anexo);

        return new AtoAvulsoComPrincipalEAnexo(processo.Id, identificador, ato, (principal, docx), (anexo, odt), registro.Anexos.Single().Id);
    }

    private async Task ConferirCopiasNoAcervoAsync(AtoAvulsoComPrincipalEAnexo avulso)
    {
        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoDaChave(avulso.ChaveDoPrincipal))).Should().Equal(
            avulso.Principal.Conteudo, "a extensão da chave segue o formato do arquivo, e a cópia preserva os bytes");
        using HttpResponseMessage respostaDoAnexo = await AcervoPublicoDeTeste.LerAnonimamenteAsync(_acervo.EnderecoDaChave(avulso.ChaveDoAnexo));
        (await respostaDoAnexo.Content.ReadAsByteArrayAsync()).Should().Equal(avulso.Anexo.Conteudo);
        AcervoPublicoDeTeste.ConferirNomeDeDownload(
            respostaDoAnexo,
            "filename*=UTF-8''Rela%C3%A7%C3%A3o%20de%20inscritos.odt",
            "o nome de download leva a extensão do formato, que o rótulo exibido não tem");
    }

    private async Task ConferirRegistroNaDivulgacaoAsync(AtoAvulsoComPrincipalEAnexo avulso)
    {
        AtoDivulgado registrado = (await _acervo.AtosDivulgadosAsync(avulso.ProcessoId)).Single(e => e.AtoId == avulso.Ato);
        registrado.NumeroVersao.Should().BeNull("o ato não criou versão de configuração");
        registrado.UrlDownload.Should().Be(AcervoDeTeste.Endereco.De(avulso.ChaveDoPrincipal).OriginalString);
        registrado.Documentos.Should().Equal(
        [
            new DocumentoDivulgado(
                PapelDoDocumentoDivulgado.Principal, $"{avulso.Identificador}-aviso.docx", TiposDeMidiaDeDocumento.Docx,
                avulso.Principal.Conteudo.LongLength, avulso.Principal.Documento.HashSha256!, AcervoDeTeste.Endereco.De(avulso.ChaveDoPrincipal).OriginalString),
            new DocumentoDivulgado(
                PapelDoDocumentoDivulgado.Anexo, "Relação de inscritos", TiposDeMidiaDeDocumento.Odt,
                avulso.Anexo.Conteudo.LongLength, avulso.Anexo.Documento.HashSha256!, AcervoDeTeste.Endereco.De(avulso.ChaveDoAnexo).OriginalString),
        ]);
    }

    /// <summary>O ato avulso semeado, com os dois documentos congelados nele e os bytes selados de cada um.</summary>
    private sealed record AtoAvulsoComPrincipalEAnexo(
        Guid ProcessoId,
        string Identificador,
        Guid Ato,
        (DocumentoAPublicar Documento, byte[] Conteudo) Principal,
        (DocumentoAPublicar Documento, byte[] Conteudo) Anexo,
        Guid IdDoAnexoDeclarado)
    {
        public string ChaveDoPrincipal => ChaveNoAcervoPublico.DoEdital(
            AcervoPublicoDeTeste.AnoDoAto, AcervoPublicoDeTeste.UnidadeDoEnvelope, AcervoPublicoDeTeste.TipoAvulso,
            Identificador, Ato, Principal.Documento.HashSha256!, FormatoDeDocumento.Docx);

        public string ChaveDoAnexo => ChaveNoAcervoPublico.DoAnexo(
            ProcessoId, Ato, IdDoAnexoDeclarado, Anexo.Documento.HashSha256!, FormatoDeDocumento.Odt);
    }
}
