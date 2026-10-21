namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DocumentosDoComprovanteTests
{
    private static readonly Guid Fase = Guid.CreateVersion7();

    private static DocumentoExigido Documento(
        string nome, string? consequencia = null, bool obrigatorio = true, FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao) =>
        DocumentoExigido.Criar(
            Fase, Guid.CreateVersion7(), nome.ToUpperInvariant().Replace(' ', '_'), nome, "CAT", Aplicabilidade.Geral, obrigatorio, consequencia,
            [], [], null, FormatosPermitidos.Criar(true, null).Value!, null, finalidade: finalidade).Value!;

    private static DocumentosDaInscricao Avaliacao(params (DocumentoExigido Documento, SituacaoDoDocumento Situacao, string? Ocorrencia)[] itens) =>
        new([.. itens.Select(static i => new DocumentoAvaliado(i.Documento, i.Situacao, null, i.Ocorrencia, [], null))],
            new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), []);

    private static ArquivoConfirmado Arquivo(DocumentoExigido documento, string hash, string? ocorrencia = null, int minuto = 0) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), documento.Id, ocorrencia, FormatoPermitido.Pdf, 1024, hash,
            new DateTimeOffset(2026, 10, 10, 12, minuto, 0, TimeSpan.Zero));

    [Fact(DisplayName = "Cada exigência da inscrição sai com obrigatoriedade, situação e consequência escrita")]
    public void Compor_Exigencia_TrazObrigatoriedadeSituacaoEConsequencia()
    {
        DocumentoExigido laudo = Documento("Laudo médico", ConsequenciaIndeferimentoCodigo.RemoveVantagem);

        DocumentoDoComprovante item = DocumentosDoComprovante.Compor(Avaliacao((laudo, SituacaoDoDocumento.Pendente, null)), []).Single();

        item.Obrigatorio.Should().BeTrue();
        item.Situacao.Should().Be(SituacaoDoDocumentoCodigo.Pendente);
        item.Consequencia.Should().Be("Sem o documento Laudo médico, a vantagem que você pleiteia não será concedida.");
    }

    [Theory(DisplayName = "Cada consequência declarada tem texto próprio; sem consequência declarada, não há texto")]
    [InlineData(ConsequenciaIndeferimentoCodigo.Elimina, "eliminado")]
    [InlineData(ConsequenciaIndeferimentoCodigo.ReclassificaAc, "ampla concorrência")]
    [InlineData(ConsequenciaIndeferimentoCodigo.PendenciaReenvio, "reenviá-lo")]
    [InlineData(null, null)]
    public void ConsequenciaPorEscrito_PorCodigo(string? codigo, string? trecho)
    {
        string? texto = DocumentosDoComprovante.ConsequenciaPorEscrito(codigo, "RG");

        if (trecho is null)
        {
            texto.Should().BeNull();
        }
        else
        {
            texto.Should().Contain(trecho).And.Contain("RG");
        }
    }

    [Fact(DisplayName = "A exigência que não se aplica e a de outra finalidade ficam fora do comprovante da inscrição")]
    public void Compor_NaoExigidoEOutraFinalidade_FicamFora()
    {
        DocumentoExigido aplica = Documento("RG");
        DocumentosDaInscricao avaliacao = Avaliacao(
            (aplica, SituacaoDoDocumento.Satisfeita, null),
            (Documento("CPF"), SituacaoDoDocumento.NaoExigido, null),
            (Documento("Comprovante de renda", finalidade: FinalidadeFormulario.IsencaoTaxa), SituacaoDoDocumento.Pendente, null));

        DocumentosDoComprovante.Compor(avaliacao, []).Select(static d => d.Nome).Should().Equal("RG");
    }

    [Fact(DisplayName = "O arquivo traz SHA-256, tamanho e formato, e só os da exigência e da ocorrência dela")]
    public void Compor_Arquivos_SoOsDaExigenciaEOcorrencia()
    {
        DocumentoExigido rg = Documento("RG");
        DocumentoExigido laudo = Documento("Laudo médico");
        DocumentosDaInscricao avaliacao = Avaliacao(
            (rg, SituacaoDoDocumento.Satisfeita, "membro-1"), (rg, SituacaoDoDocumento.Pendente, "membro-2"), (laudo, SituacaoDoDocumento.Satisfeita, null));
        IReadOnlyList<ArquivoConfirmado> arquivos =
        [
            Arquivo(rg, new string('b', 64), "membro-1", minuto: 2),
            Arquivo(rg, new string('a', 64), "membro-1", minuto: 1),
            Arquivo(laudo, new string('c', 64)),
        ];

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(avaliacao, arquivos);

        itens[0].Arquivos.Should().Equal(new ArquivoDoComprovante(new string('a', 64), 1024, "PDF"), new ArquivoDoComprovante(new string('b', 64), 1024, "PDF"));
        itens[1].Arquivos.Should().BeEmpty("o arquivo da outra ocorrência não é desta");
        itens[2].Arquivos.Should().ContainSingle().Which.HashSha256.Should().Be(new string('c', 64));
    }

    private static DocumentosDaInscricao ComOu(
        GrupoDaArvore ou, bool grupoAtendido, params (DocumentoExigido Documento, SituacaoDoDocumento Situacao)[] alternativas) =>
        new([.. alternativas.Select(a => new DocumentoAvaliado(a.Documento, a.Situacao, null, null, [ou], null, DispensadoPeloGrupo: grupoAtendido && a.Situacao != SituacaoDoDocumento.Satisfeita))],
            new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), []);

    [Fact(DisplayName = "A alternativa não enviada de um grupo um-ou-outro já atendido sai dispensada e sem consequência")]
    public void Compor_OuAtendido_DispensaAAlternativaNaoEnviada()
    {
        DocumentoExigido rg = Documento("RG", ConsequenciaIndeferimentoCodigo.Elimina);
        DocumentoExigido cnh = Documento("CNH", ConsequenciaIndeferimentoCodigo.Elimina);
        GrupoDaArvore ou = new(Guid.CreateVersion7(), TipoNo.GrupoOu, 1, null);

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComOu(ou, true, (rg, SituacaoDoDocumento.Satisfeita), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens[0].Situacao.Should().Be(SituacaoDoDocumentoCodigo.Satisfeita);
        itens[1].Situacao.Should().Be(DocumentosDoComprovante.SituacaoDispensada);
        itens[1].Consequencia.Should().BeNull("o grupo está atendido: a CNH não precisava ser enviada");
    }

    [Fact(DisplayName = "No grupo um-ou-outro ainda não atendido, a alternativa segue pendente e a consequência do grupo vale para todas")]
    public void Compor_OuNaoAtendido_UsaAConsequenciaDoGrupo()
    {
        DocumentoExigido rg = Documento("RG");
        DocumentoExigido cnh = Documento("CNH");
        GrupoDaArvore ou = new(Guid.CreateVersion7(), TipoNo.GrupoOu, 1, null, ConsequenciaIndeferimentoCodigo.ReclassificaAc);

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComOu(ou, false, (rg, SituacaoDoDocumento.Pendente), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens.Should().OnlyContain(static d => d.Situacao == SituacaoDoDocumentoCodigo.Pendente);
        itens.Should().OnlyContain(static d => d.Consequencia!.Contains("RG, CNH") && d.Consequencia.Contains("ampla concorrência"));
    }

    [Fact(DisplayName = "No grupo um-ou-outro com consequência, a alternativa mostra a do grupo, e não a própria")]
    public void Compor_OuComConsequencia_PrevaleceSobreADaAlternativa()
    {
        DocumentoExigido rg = Documento("RG", ConsequenciaIndeferimentoCodigo.PendenciaReenvio);
        DocumentoExigido cnh = Documento("CNH");
        GrupoDaArvore ou = new(Guid.CreateVersion7(), TipoNo.GrupoOu, 1, null, ConsequenciaIndeferimentoCodigo.ReclassificaAc);

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComOu(ou, false, (rg, SituacaoDoDocumento.Pendente), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens.Should().OnlyContain(static d => d.Consequencia!.Contains("ampla concorrência"));
    }

    [Fact(DisplayName = "No grupo um-ou-outro sem consequência, a alternativa não traz texto, mesmo declarando a própria")]
    public void Compor_OuSemConsequencia_AlternativaSemTexto()
    {
        DocumentoExigido rg = Documento("RG", ConsequenciaIndeferimentoCodigo.Elimina);
        DocumentoExigido cnh = Documento("CNH");
        GrupoDaArvore ou = new(Guid.CreateVersion7(), TipoNo.GrupoOu, 1, null);

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComOu(ou, false, (rg, SituacaoDoDocumento.Pendente), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens.Should().OnlyContain(static d => d.Consequencia == null);
    }
}
