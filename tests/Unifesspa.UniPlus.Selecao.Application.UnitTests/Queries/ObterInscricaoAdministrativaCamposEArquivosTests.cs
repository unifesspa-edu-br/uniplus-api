namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.ConsultaAdministrativaDeTeste;

/// <summary>
/// Os campos fixos da inscrição saem pela permissão da propriedade, e os arquivos pela permissão da classe que a exigência
/// congelou na versão da inscrição; o id, o processo, o número do último comprovante e a capacidade de emitir não dependem
/// das permissões de dado.
/// </summary>
public sealed class ObterInscricaoAdministrativaCamposEArquivosTests
{
    [Fact(DisplayName = "Cada arquivo sai pela permissão da classe da exigência: com documentos sensíveis o laudo aparece e o comprovante de residência, não")]
    public async Task Arquivo_PelaPermissaoDaClasse()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, diogo);

        ArquivoAdministrativoDto laudo = dto.Arquivos.Should().ContainSingle().Subject;
        laudo.ArquivoId.Should().Be(cenario.ArquivoDe("LAUDO_MEDICO"));
        laudo.NomeDoDocumento.Should().Be("Documento LAUDO_MEDICO");
    }

    [Fact(DisplayName = "Classificação não resolvida vale como sensível: só a permissão de dados sensíveis a revela")]
    public async Task ClassificacaoNaoResolvida_ValeSensivel()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("DADO_ANTIGO", "PUBLICO", TipoRenderizacao.Texto, Texto("a"))
            .Exigencia("COMPROVANTE_SEM_EXIGENCIA", ClassificacaoDeProtecao.Pessoal, noEnvelope: false);
        DecisoesDeAcessoDeTeste comPessoais = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosPessoaisConsultar,
            PermissaoSelecao.DocumentosPessoaisConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, comPessoais);

        dto.Arquivos.Should().BeEmpty("a exigência que a versão não tem vale como sensível, sem base legal");
    }

    [Fact(DisplayName = "Avaliação cega: Leandro vê curso, situação e o artigo publicado, sem nome nem CPF mascarado")]
    public async Task AvaliacaoCega_SemNomeNemCpf()
    {
        InscricaoAdministrativaDeTeste cenario = AvaliacaoDeKevin();
        DecisoesDeAcessoDeTeste leandro = new(
            "leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DocumentosPublicosConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, leandro);

        dto.Id.Should().Be(cenario.InscricaoId);
        dto.Situacao.Should().Be(SituacaoInscricao.Submetida);
        dto.Ofertas.Should().Equal(new OfertaEscolhidaDto(cenario.OfertaCursoId, InscricaoAdministrativaDeTeste.NomeDaOferta, 1));
        dto.Processo.Should().Be(new ProcessoDaInscricaoDto(cenario.ProcessoId, InscricaoAdministrativaDeTeste.NomeDoProcesso));
        dto.NumeroInscricao.Should().BeNull("o número é dado interno");
        ItensDe(dto).Single().FatoCodigo.Should().Be("RESUMO_DO_ARTIGO");
        dto.Arquivos.Single().NomeDoDocumento.Should().Be("Documento ARTIGO_PUBLICADO");
        JsonSerializer.Serialize(dto).Should().NotContain(PersonasDeTeste.NomeKevinCarvalho).And.NotContain("***.");
    }

    [Fact(DisplayName = "Sem dados públicos as ofertas são omitidas, mas o id e o processo saem")]
    public async Task SemDadosPublicos_OmiteAsOfertas()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste erick = new("erick.araujo", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, erick);

        dto.Ofertas.Should().BeNull();
        dto.Id.Should().Be(cenario.InscricaoId);
        dto.Processo.Nome.Should().Be(InscricaoAdministrativaDeTeste.NomeDoProcesso);
        JsonSerializer.Serialize(dto).Should().NotContain("ofertas");
    }

    [Fact(DisplayName = "A data do envio sai só com a decisão Interno, e o primeiro envio só é consultado quando ela é permitida")]
    public async Task DataDoEnvio_SaiSoComADecisaoInterna()
    {
        DateTimeOffset primeiro = new(2027, 1, 10, 9, 0, 0, TimeSpan.Zero);
        InscricaoAdministrativaDeTeste cenario = DeFernanda().EnviadaEm(primeiro);
        InscricaoAdministrativaDeTeste.Mundo mundoDeDiogo = cenario.Montar();
        InscricaoAdministrativaDeTeste.Mundo mundoDeKevin = cenario.Montar();

        InscricaoAdministrativaDto comInterno = await ConsultarComo("diogo.souza", PermissaoSelecao.DadosInternosConsultar, cenario, mundoDeDiogo);
        InscricaoAdministrativaDto semInterno = await ConsultarComo("kevin.carvalho", PermissaoSelecao.DadosPessoaisConsultar, cenario, mundoDeKevin);

        comInterno.SubmetidaEm.Should().Be(primeiro);
        semInterno.SubmetidaEm.Should().BeNull();
        JsonSerializer.Serialize(semInterno).Should().NotContain("submetidaEm");
        await mundoDeDiogo.Inscricoes.Received(1).ObterPrimeirosEnviosAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == cenario.InscricaoId), Arg.Any<CancellationToken>());
        await mundoDeKevin.Inscricoes.DidNotReceiveWithAnyArgs().ObterPrimeirosEnviosAsync(default!, default);
    }

    [Theory(DisplayName = "A leitura diz se quem consulta pode emitir o comprovante, pela mesma permissão que a emissão exige e sem depender das de dado")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PodeEmitirComprovante_PelaPermissaoDeEmitir(bool concedida)
    {
        PermissaoSelecao[] permissoes =
            [PermissaoSelecao.InscricoesConsultar, .. concedida ? [PermissaoSelecao.InscricoesEmitirComprovante] : Array.Empty<PermissaoSelecao>()];
        DecisoesDeAcessoDeTeste leandro = new("leandro.viana", permissoes);

        InscricaoAdministrativaDto dto = await ConsultarDto(DeFernanda(), leandro);

        dto.PodeEmitirComprovante.Should().Be(concedida);
        await leandro.NaoExigiuAsync(CodigoEmitir);
    }

    [Theory(DisplayName = "O número do último comprovante é a quantidade emitida, nulo sem nenhum, e vem na cancelada, sem decifrar comprovante e com só o acesso às inscrições")]
    [InlineData(SituacaoInscricao.Submetida, 0, null)]
    [InlineData(SituacaoInscricao.Submetida, 1, 1)]
    [InlineData(SituacaoInscricao.Submetida, 3, 3)]
    [InlineData(SituacaoInscricao.Cancelada, 2, 2)]
    [InlineData(SituacaoInscricao.Cancelada, 0, null)]
    public async Task NumeroDoUltimoComprovante_QuantidadeEmitida(SituacaoInscricao situacao, int emitidos, int? esperado)
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, situacao);
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        mundo.Inscricoes.ObterEmissoesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == cenario.InscricaoId), Arg.Any<CancellationToken>()).Returns(EmissoesDe(cenario, emitidos));

        InscricaoAdministrativaDto dto = await ConsultarComo("leandro.viana", PermissaoSelecao.InscricoesConsultar, cenario, mundo);

        dto.NumeroDoUltimoComprovante.Should().Be(esperado);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterComprovanteAsync(default, default, default);
    }

    private static InscricaoAdministrativaDeTeste AvaliacaoDeKevin() =>
        new InscricaoAdministrativaDeTeste(PersonasDeTeste.KevinCarvalho)
            .Fato("CPF", "IDENTIFICADOR", TipoRenderizacao.Texto, Texto(PersonasDeTeste.KevinCarvalho.Valor), formato: "CPF")
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto(PersonasDeTeste.NomeKevinCarvalho), ordem: 1)
            .Fato("RESUMO_DO_ARTIGO", "PUBLICO", TipoRenderizacao.Texto, Texto("Ensaio sobre o ensino"), ordem: 2)
            .Exigencia("ARTIGO_PUBLICADO", ClassificacaoDeProtecao.Publico)
            .Exigencia("COMPROVANTE_RESIDENCIA", ClassificacaoDeProtecao.Pessoal);

    private static IReadOnlyList<EmissaoDoComprovante> EmissoesDe(InscricaoAdministrativaDeTeste cenario, int quantidade) =>
        [.. Enumerable.Range(0, quantidade).Select(i => new EmissaoDoComprovante(cenario.InscricaoId, DateTimeOffset.UnixEpoch.AddDays(i)))];

    private static async Task<InscricaoAdministrativaDto> ConsultarComo(
        string persona, PermissaoSelecao permissaoDeDado, InscricaoAdministrativaDeTeste cenario, InscricaoAdministrativaDeTeste.Mundo mundo) =>
        (await Executar(mundo, new DecisoesDeAcessoDeTeste(persona, PermissaoSelecao.InscricoesConsultar, permissaoDeDado), cenario.ProcessoId, cenario.InscricaoId)).Value!;
}
