namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DocumentosDoComprovanteTests
{
    private static readonly Guid Fase = Guid.CreateVersion7();

    private static DocumentoExigido Documento(
        string nome, string? consequencia = null, bool obrigatorio = true, FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), nome.ToUpperInvariant().Replace(' ', '_'), nome, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(Fase, null, FormularioDeTeste.CodigoDoPapel(finalidade)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(obrigatorio, consequencia, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static readonly CodigoDoFormulario[] FormulariosDaInscricao = [FormularioDeTeste.CodigoDoPapel(FinalidadeFormulario.Inscricao)];

    private static DocumentosDaInscricao Avaliacao(params (DocumentoExigido Documento, SituacaoDoDocumento Situacao, string? Ocorrencia)[] itens) =>
        new([.. itens.Select(static i => new DocumentoAvaliado(i.Documento, i.Situacao, null, i.Ocorrencia, [], null))],
            new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), [])
        {
            CodigosDosFormulariosDaInscricao = FormulariosDaInscricao,
        };

    private static ArquivoConfirmado Arquivo(DocumentoExigido documento, string hash, string? ocorrencia = null, int minuto = 0) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), documento.Id, ocorrencia, FormatoPermitido.Pdf, 1024, hash,
            new DateTimeOffset(2026, 10, 10, 12, minuto, 0, TimeSpan.Zero));

    [Fact(DisplayName = "Cada exigência da inscrição sai com obrigatoriedade, situação e consequência escrita")]
    public void Compor_Exigencia_TrazObrigatoriedadeSituacaoEConsequencia()
    {
        DocumentoExigido laudo = Documento("Laudo médico", "RETIRA_O_ACRESCIMO");

        DocumentoDoComprovante item = DocumentosDoComprovante.Compor(Avaliacao((laudo, SituacaoDoDocumento.Pendente, null)), []).Single();

        item.Obrigatorio.Should().BeTrue();
        item.Situacao.Should().Be(SituacaoDoDocumento.Pendente.Codigo());
        item.ConsequenciaDoDescumprimento.Should().Be("Sem o documento Laudo médico, o acréscimo do Fator de Atenuação das Desigualdades de Acesso não será concedido.");
    }

    [Theory(DisplayName = "Cada consequência declarada tem texto próprio; sem consequência declarada, não há texto")]
    [InlineData("ELIMINA_DO_PROCESSO", "eliminado")]
    [InlineData("RETIRA_A_MODALIDADE", "modalidade")]
    [InlineData("RETIRA_O_ACRESCIMO", "acréscimo")]
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

    [Fact(DisplayName = "A exigência que não se aplica e a de outro formulário ficam fora do comprovante da inscrição")]
    public void Compor_NaoExigidoEDeOutroFormulario_FicamFora()
    {
        DocumentoExigido aplica = Documento("RG");
        DocumentosDaInscricao avaliacao = Avaliacao(
            (aplica, SituacaoDoDocumento.Satisfeita, null),
            (Documento("CPF"), SituacaoDoDocumento.NaoExigido, null),
            (Documento("Comprovante de renda", finalidade: FinalidadeFormulario.IsencaoTaxa), SituacaoDoDocumento.Pendente, null));

        DocumentosDoComprovante.Compor(avaliacao, []).Select(static d => d.Nome).Should().Equal("RG");
    }

    [Fact(DisplayName = "Com dois formulários de inscrição, o comprovante traz as exigências de cada um e deixa de fora as dos demais")]
    public void Compor_DoisFormulariosDaInscricao_TrazOsDosDois()
    {
        DocumentosDaInscricao avaliacao = Avaliacao(
            (Documento("RG"), SituacaoDoDocumento.Satisfeita, null),
            (Documento("Laudo", finalidade: FinalidadeFormulario.Habilitacao), SituacaoDoDocumento.Pendente, null),
            (Documento("Comprovante de renda", finalidade: FinalidadeFormulario.IsencaoTaxa), SituacaoDoDocumento.Pendente, null))
            with
        {
            CodigosDosFormulariosDaInscricao = [.. FormulariosDaInscricao, FormularioDeTeste.CodigoDoPapel(FinalidadeFormulario.Habilitacao)],
        };

        DocumentosDoComprovante.Compor(avaliacao, []).Select(static d => d.Nome).Should().Equal("RG", "Laudo");
    }

    [Fact(DisplayName = "O arquivo traz SHA-256, tamanho e formato, e só os da exigência e da ocorrência dela")]
    public void Compor_Arquivos_SoOsDaExigenciaEOcorrencia()
    {
        DocumentoExigido rg = Documento("RG");
        DocumentoExigido laudo = Documento("Laudo médico");
        DocumentosDaInscricao avaliacao = Avaliacao(
            (rg, SituacaoDoDocumento.Satisfeita, "membro-1"), (rg, SituacaoDoDocumento.Pendente, "membro-2"), (laudo, SituacaoDoDocumento.Satisfeita, null));
        IReadOnlyList<ArquivoConfirmado> arquivos = ArquivosDasOcorrencias(rg, laudo);

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(avaliacao, arquivos);

        itens[0].Arquivos.Should().Equal(new ArquivoDoComprovante(new string('a', 64), 1024, "PDF"), new ArquivoDoComprovante(new string('b', 64), 1024, "PDF"));
        itens[1].Arquivos.Should().BeEmpty("o arquivo da outra ocorrência não é desta");
        itens[2].Arquivos.Should().ContainSingle().Which.HashSha256.Should().Be(new string('c', 64));
    }

    private static IReadOnlyList<ArquivoConfirmado> ArquivosDasOcorrencias(DocumentoExigido rg, DocumentoExigido laudo) =>
    [
        Arquivo(rg, new string('b', 64), "membro-1", minuto: 2),
        Arquivo(rg, new string('a', 64), "membro-1", minuto: 1),
        Arquivo(laudo, new string('c', 64)),
    ];

    private static DocumentosDaInscricao ComGrupo(
        GrupoDeComprovacao grupo, bool grupoAtendido, params (DocumentoExigido Documento, SituacaoDoDocumento Situacao)[] alternativas) =>
        new([.. alternativas.Select(a => new DocumentoAvaliado(a.Documento, a.Situacao, null, null, [grupo], null, DispensadoPeloGrupo: grupoAtendido && a.Situacao != SituacaoDoDocumento.Satisfeita))],
            new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), [])
        {
            CodigosDosFormulariosDaInscricao = FormulariosDaInscricao,
        };

    [Fact(DisplayName = "A alternativa não enviada de um grupo que exige um item e já foi cumprido sai dispensada e sem consequência")]
    public void Compor_OuAtendido_DispensaAAlternativaNaoEnviada()
    {
        DocumentoExigido rg = Documento("RG", "ELIMINA_DO_PROCESSO");
        DocumentoExigido cnh = Documento("CNH", "ELIMINA_DO_PROCESSO");
        GrupoDeComprovacao grupo = new(Guid.CreateVersion7(), FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null);

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComGrupo(grupo, true, (rg, SituacaoDoDocumento.Satisfeita), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens[0].Situacao.Should().Be(SituacaoDoDocumento.Satisfeita.Codigo());
        itens[1].Situacao.Should().Be(DocumentosDoComprovante.SituacaoDispensada);
        itens[1].ConsequenciaDoDescumprimento.Should().BeNull("o grupo está atendido: a CNH não precisava ser enviada");
    }

    [Fact(DisplayName = "No grupo que exige um item ainda não cumprido, a alternativa segue pendente e a consequência do grupo vale para todas")]
    public void Compor_OuNaoAtendido_UsaAConsequenciaDoGrupo()
    {
        DocumentoExigido rg = Documento("RG");
        DocumentoExigido cnh = Documento("CNH");
        GrupoDeComprovacao grupo = new(Guid.CreateVersion7(), FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null, "RETIRA_A_MODALIDADE");

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComGrupo(grupo, false, (rg, SituacaoDoDocumento.Pendente), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens.Should().OnlyContain(static d => d.Situacao == SituacaoDoDocumento.Pendente.Codigo());
        itens.Should().OnlyContain(static d => d.ConsequenciaDoDescumprimento!.Contains("RG, CNH") && d.ConsequenciaDoDescumprimento.Contains("modalidade"));
    }

    [Fact(DisplayName = "No grupo que exige um item com consequência própria, a alternativa mostra a do grupo, e não a própria")]
    public void Compor_OuComConsequencia_PrevaleceSobreADaAlternativa()
    {
        DocumentoExigido rg = Documento("RG", "RETIRA_O_ACRESCIMO");
        DocumentoExigido cnh = Documento("CNH");
        GrupoDeComprovacao grupo = new(Guid.CreateVersion7(), FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null, "RETIRA_A_MODALIDADE");

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComGrupo(grupo, false, (rg, SituacaoDoDocumento.Pendente), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens.Should().OnlyContain(static d => d.ConsequenciaDoDescumprimento!.Contains("modalidade"));
    }

    [Fact(DisplayName = "No grupo que exige um item sem consequência própria, a alternativa não traz texto, mesmo declarando a própria")]
    public void Compor_OuSemConsequencia_AlternativaSemTexto()
    {
        DocumentoExigido rg = Documento("RG", "ELIMINA_DO_PROCESSO");
        DocumentoExigido cnh = Documento("CNH");
        GrupoDeComprovacao grupo = new(Guid.CreateVersion7(), FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null);

        IReadOnlyList<DocumentoDoComprovante> itens = DocumentosDoComprovante.Compor(
            ComGrupo(grupo, false, (rg, SituacaoDoDocumento.Pendente), (cnh, SituacaoDoDocumento.Pendente)), []);

        itens.Should().OnlyContain(static d => d.ConsequenciaDoDescumprimento == null);
    }
}
