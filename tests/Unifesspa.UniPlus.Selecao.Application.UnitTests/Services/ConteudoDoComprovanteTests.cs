namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static ComprovanteDeTeste;

/// <summary>
/// O que o comprovante mostra além das respostas: os cursos e as modalidades de cada opção, os dados que o sistema apura, os
/// termos aceitos e os documentos, tudo com palavras.
/// </summary>
public sealed class ConteudoDoComprovanteTests
{
    private static readonly Guid Direito = Guid.CreateVersion7();
    private static readonly Guid Computacao = Guid.CreateVersion7();

    private static FatoDerivadoCongelado Derivado(string codigo, string rotulo, string tipo) =>
        new(codigo, rotulo, "Descrição do fato.", null, tipo, "ESCALAR", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade.");

    private static Dictionary<string, FatoDerivadoCongelado> Derivados(params FatoDerivadoCongelado[] fatos) =>
        fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

    [Fact(DisplayName = "O identificador legível do processo vem da versão fixada da inscrição, e some quando a versão não o declara")]
    public void Montar_IdentificadorDoProcesso_VemDaVersaoFixada()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(inscricao, Respostas());

        Montar(inscricao, comprovante, Envelope(identificador: IdentificadorLegivel.Criar("pss-medicina-2027").Value)).IdentificadorDoProcesso
            .Should().Be("pss-medicina-2027");
        Montar(inscricao, comprovante, Envelope()).IdentificadorDoProcesso.Should().BeNull();
    }

    [Fact(DisplayName = "O número de opções vem da versão, e as opções, do comprovante: versão de duas e comprovante de uma saem com uma só")]
    public void Montar_VersaoDeDuasOpcoes_ComprovanteComUma()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao umaEscolha = Emitir(inscricao, Respostas(), opcoes: (Direito, null));
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas = [OfertasDeTeste.Distribuicao(Direito, "AC"), OfertasDeTeste.Distribuicao(Computacao, "AC")];

        ComprovanteDaInscricaoDto dto = Montar(inscricao, umaEscolha, Envelope(ofertas: ofertas, numeroDeOpcoes: 2));

        dto.NumeroDeOpcoesDeCurso.Should().Be(2);
        dto.OpcoesDeCurso.Should().ContainSingle().Which.Ordem.Should().Be(1);
    }

    [Fact(DisplayName = "A versão que declara uma só opção diz isso no comprovante")]
    public void Montar_VersaoDeUmaOpcao_DizUmaOpcao()
    {
        Inscricao inscricao = UmaInscricao();

        ComprovanteDaInscricaoDto dto = Montar(inscricao, Emitir(inscricao, Respostas(), opcoes: (Direito, null)), Envelope(ofertas: [OfertasDeTeste.Distribuicao(Direito, "AC")]));

        dto.NumeroDeOpcoesDeCurso.Should().Be(1);
    }

    [Fact(DisplayName = "As modalidades saem por opção de curso, com o nome que o envio registrou, e a segunda opção só leva as dela")]
    public void Montar_ModalidadesPorOpcao()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(
            inscricao, Respostas(), opcoes: (Direito, Computacao),
            modalidades: [new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0), new ModalidadeDeConcorrencia("AC_FEM", "Ampla concorrência feminina", 1)]);
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas = [OfertasDeTeste.Distribuicao(Direito, "AC", "AC_FEM"), OfertasDeTeste.Distribuicao(Computacao, "AC")];

        ComprovanteDaInscricaoDto dto = Montar(inscricao, comprovante, Envelope(ofertas: ofertas, numeroDeOpcoes: 2));

        dto.OpcoesDeCurso.Select(static o => o.Ordem).Should().Equal(1, 2);
        dto.OpcoesDeCurso[0].Modalidades!.Select(static m => (m.Codigo, m.Nome)).Should().Equal(("AC", "Ampla concorrência"), ("AC_FEM", "Ampla concorrência feminina"));
        dto.OpcoesDeCurso[1].Modalidades.Should().BeNull();
        dto.Derivados.Should().BeEmpty("a modalidade não é um dado apurado do formulário: sai na opção de curso");
    }

    [Fact(DisplayName = "O comprovante antigo mostra o curso que ele registrou, mesmo depois de a oferta trocar de nome")]
    public void Montar_CursoDoComprovanteAntigo()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao antigo = Emitir(inscricao, Respostas(), opcoes: (Direito, null));

        ComprovanteDaInscricaoDto dto = Montar(
            inscricao, antigo, Envelope(ofertas: [OfertasDeTeste.DistribuicaoDoCurso(Direito, "Medicina", "AC")]));

        dto.OpcoesDeCurso.Should().ContainSingle().Which.Nome.Should().Contain("Medicina");
    }

    [Fact(DisplayName = "O estado de residência sai pelo nome do estado, e não pela sigla")]
    public void Montar_Derivados_EstadoPeloNome()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(inscricao, Respostas(), derivados: Respostas(("UF_RESIDENCIA", "PA")));

        ComprovanteDaInscricaoDto dto = Montar(
            inscricao, comprovante, Envelope(derivados: Derivados(Derivado("UF_RESIDENCIA", "Estado de residência", "CATEGORICO"))));

        ItemDoComprovanteDto estado = dto.Derivados.Should().ContainSingle(d => d.Rotulo == "Estado de residência").Subject;
        estado.Valor!.Value.GetProperty("rotulo").GetString().Should().Be("Pará");
    }

    [Fact(DisplayName = "A idade sai em anos completos, em texto, com o rótulo que a versão congelou")]
    public void Montar_Derivados_IdadeEmAnos()
    {
        Inscricao inscricao = UmaInscricao();
        FatoDerivadoCongelado faixaEtaria = Derivado("FAIXA_ETARIA", "Idade", "NUMERICO");
        ComprovanteInscricao comprovante = Emitir(inscricao, Respostas(), derivados: Respostas((faixaEtaria.Codigo, 26)));

        ComprovanteDaInscricaoDto dto = Montar(inscricao, comprovante, Envelope(derivados: Derivados(faixaEtaria)));

        ItemDoComprovanteDto idade = dto.Derivados.Should().ContainSingle().Subject;
        idade.Rotulo.Should().Be(faixaEtaria.Nome);
        idade.Valor!.Value.ValueKind.Should().Be(JsonValueKind.String);
        idade.Valor!.Value.GetString().Should().Be("26");
    }

    [Fact(DisplayName = "O documento sai com a situação por extenso, e a situação que a versão não conhece sai a confirmar")]
    public void Montar_Documentos_SituacaoPorExtenso()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = ComprovanteComDocumentos(
            inscricao,
            Documento("Laudo", SituacaoDoDocumentoCodigo.Satisfeita, new ArquivoDoComprovante(new string('a', 64), 2048, "PDF")),
            Documento("Histórico", SituacaoDoDocumentoCodigo.Pendente),
            Documento("Renda", DocumentosDoComprovante.SituacaoDispensada),
            Documento("Diploma", SituacaoDoDocumentoCodigo.DependeDeFasePosterior),
            Documento("Declaração", SituacaoDoDocumentoCodigo.PodeSerExigido),
            Documento("Outro", "SITUACAO_NOVA"));

        ComprovanteDaInscricaoDto dto = Montar(inscricao, comprovante, Envelope());

        dto.Documentos.Select(static d => d.Situacao).Should().Equal(
            "Atendido", "Pendente", "Dispensado", "Pode ser exigido, conforme uma fase posterior do processo",
            "Pode ser exigido, conforme as respostas", "A confirmar");
        dto.Documentos[0].Arquivos.Should().Equal(new ArquivoDoComprovanteDto(new string('a', 64), 2048, "PDF"));
        dto.Documentos[0].Consequencia.Should().Be("Sem o documento, a vantagem não é concedida.");
    }

    [Fact(DisplayName = "O termo sai com o nome da versão da inscrição; o termo que a versão não tem sai pelo código")]
    public void Montar_Termos_NomeDaVersaoOuCodigo()
    {
        Inscricao inscricao = UmaInscricao();
        Guid versaoDoTermo = Guid.CreateVersion7();
        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            inscricao.Id, "00000001", Instante, null, Respostas(), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(),
            [new AceiteDeTermo("TERMO_VERACIDADE", versaoDoTermo, new string('b', 64))], []);

        ComprovanteDaInscricaoDto dto = Montar(inscricao, comprovante, Envelope());

        dto.TermosAceitos.Should().ContainSingle().Which.Should().Be(
            new TermoAceitoDoComprovanteDto("TERMO_VERACIDADE", "TERMO_VERACIDADE", versaoDoTermo, new string('b', 64)));
    }

    private static DocumentoDoComprovante Documento(string nome, string situacao, params ArquivoDoComprovante[] arquivos) =>
        new(Guid.CreateVersion7(), nome.ToUpperInvariant(), nome, Obrigatorio: true, situacao, null, "Sem o documento, a vantagem não é concedida.", arquivos);

    private static ComprovanteInscricao ComprovanteComDocumentos(Inscricao inscricao, params DocumentoDoComprovante[] documentos) =>
        ComprovanteInscricao.Emitir(
            inscricao.Id, "00000001", Instante, null, Respostas(), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [], documentos);
}
