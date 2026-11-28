namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O campo de cada causa aponta um bloco do documento de configuração: o cliente leva quem corrige até o bloco pelo nome
/// que o esquema publicado dá a ele, então o nome de uma dimensão sem bloco, ou de um bloco que o esquema não tem, deixaria
/// a causa sem lugar para ser corrigida. O mesmo vale para o ponteiro inteiro do campo (RFC 6901): cada segmento tem de existir
/// no esquema, e o segmento numérico é a posição de um item da lista.
/// </summary>
public sealed class BlocosDoDocumentoDasCausasTests
{
    private static readonly HashSet<string> BlocosDoEsquema = LerBlocosDoEsquema();
    private static readonly CaminhosDoDocumentoNoEsquema Esquema = CaminhosDoDocumentoNoEsquema.Publicado;

    public static TheoryData<string> Dimensoes => [.. DimensoesConhecidas()];

    [Theory(DisplayName = "Toda dimensão de conformidade tem o bloco do documento, e o esquema publicado tem esse bloco")]
    [MemberData(nameof(Dimensoes))]
    public void Dimensao_TemOBlocoQueOEsquemaPublica(string dimensao)
    {
        string bloco = DimensaoConformidade.BlocoDoDocumento(dimensao);

        BlocosDoEsquema.Should().Contain(bloco);
    }

    [Fact(DisplayName = "Dimensão desconhecida não tem bloco do documento")]
    public void DimensaoDesconhecida_Lanca()
    {
        Action buscar = () => DimensaoConformidade.BlocoDoDocumento("dimensao_inexistente");

        buscar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "O campo de toda causa do processo sem nada declarado existe no esquema publicado, o caminho inteiro")]
    public void CampoDeTodaCausa_DoProcessoSemNada_ExisteNoEsquema()
    {
        ProcessoSeletivo semNada = ProcessoSeletivo.Criar(
            "Processo incompleto", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        IReadOnlyList<FieldError> causas = PonteiroNoDocumento.Converter(
            ConfiguracaoAptaACongelar.Conferir(semNada.Id, semNada.Declaracao(), CadastrosDeTeste.Para(semNada)).Errors, OrigemNoDocumento.Identidade);

        causas.Should().NotBeEmpty();
        CaminhosAusentes(causas).Should().BeEmpty();
    }

    [Theory(DisplayName = "O campo de toda causa de um processo com defeito em todos os assuntos existe no esquema publicado, o caminho inteiro")]
    [MemberData(nameof(Transicoes))]
    public void CampoDeTodaCausa_DeTodosOsAssuntos_ExisteNoEsquema(string transicao)
    {
        CenarioDeCongelamentoPorAssunto cenario = CenarioDeCongelamentoPorAssunto.Para(transicao);

        IReadOnlyList<FieldError> causas = cenario.CausasDaConferencia(CenarioDeCongelamentoPorAssunto.AssuntosQueSabeQuebrar.ToHashSet(StringComparer.Ordinal));

        causas.Should().NotBeEmpty();
        CaminhosAusentes(causas).Should().BeEmpty();
    }

    [Fact(DisplayName = "O campo de toda causa do cronograma e das etapas declarados com defeito em cada campo existe no esquema publicado, o caminho inteiro")]
    public void CampoDeTodaCausa_DasFasesEDasEtapas_ExisteNoEsquema()
    {
        IReadOnlyList<FieldError> causas = PonteiroNoDocumento.Converter(
            CausasQueImpedemCongelar.De(DeclaracoesComDefeitoParaOEsquema.ContextoComTodosOsDefeitos()), OrigemNoDocumento.Identidade);

        string?[] traduzidos =
        [
            "/cronograma/fases/1/tipoFaseCodigo", "/cronograma/fases/0/unidadeResponsavelCodigo",
            "/cronograma/fases/0/bancasRequeridas/0/categoriasDocumentoCodigos", "/etapas/1/faseChave", "/etapas/1/chave",
        ];
        causas.Select(static causa => causa.Field).Should().Contain(traduzidos);
        CaminhosAusentes(causas).Should().BeEmpty();
    }

    [Fact(DisplayName = "O campo da causa da primeira fase depois da importação, no processo de resultado importado, existe no esquema publicado, o caminho inteiro")]
    public void CampoDaCausaDaImportacaoDoResultado_ExisteNoEsquema()
    {
        ProcessoSeletivo processo = ProcessoDaImportacaoDeTeste.ImportadoConforme();
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirImportacaoDoResultado(ProcessoDaImportacaoDeTeste.UmRelatorio, null).IsSuccess.Should().BeTrue();

        IReadOnlyList<FieldError> causas = PonteiroNoDocumento.Converter(
            ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors, OrigemNoDocumento.Identidade);

        causas.Select(static causa => causa.Field).Should().Contain("/importacao/primeiraFaseDepoisDaImportacaoChave");
        CaminhosAusentes(causas).Should().BeEmpty();
    }

    [Fact(DisplayName = "O campo da causa do procedimento de recurso da etapa existe no esquema publicado, o caminho inteiro")]
    public void CampoDaCausaDoRecursoDaEtapa_ExisteNoEsquema()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas([PendenciasDosProcedimentosDeRecursoTests.EtapaComJanela(ProcedimentosDeRecurso.Nenhum, TipoMotivoDeDecisao.Classificacao.Codigo())])
            .IsSuccess.Should().BeTrue();

        IReadOnlyList<FieldError> causas = PonteiroNoDocumento.Converter(
            ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors, OrigemNoDocumento.Identidade);

        causas.Select(static causa => causa.Field).Should().Contain("/etapas/0/recursos");
        CaminhosAusentes(causas).Should().BeEmpty();
    }

    [Theory(DisplayName = "O ponteiro que um emissor sem cenário construível produz existe no esquema publicado")]
    [InlineData("fases[0].regraRecurso")]
    [InlineData("fases[0].regraRecurso.regraCodigo")]
    [InlineData("fases[0].regraRecurso.procedimentosDeRecurso[0].regra")]
    [InlineData("etapas[0].recursos[0].regraCodigo")]
    [InlineData("etapas[0].recursos[0].procedimentosDeRecurso[0].regra")]
    [InlineData("classificacao.regrasEliminacao[0].regra")]
    [InlineData("classificacao.criteriosDeDesempate[0].regra")]
    [InlineData("fases[0].produtos[1].atoCodigo")]
    [InlineData("etapas[0].produtos[2].atoCodigo")]
    [InlineData("formularios[0].itens[0].fatoCodigo")]
    [InlineData("formularios[1].itens[1].restricoes")]
    [InlineData("formularios[1].grupos[1].subitens[0].fatoCodigo")]
    [InlineData("cronograma.fases")]
    [InlineData("etapas")]
    [InlineData("motivosDeDecisao")]
    [InlineData("motivosDeDecisao[2].justificativaDaInclusao")]
    [InlineData("formularios")]
    [InlineData("distribuicaoVagas")]
    [InlineData("distribuicaoVagas[1].modalidades")]
    [InlineData("taxaDeInscricao")]
    [InlineData("taxaDeInscricao.fundamentos")]
    public void PonteiroQueUmEmissorSemCenarioConstruivelProduz_ExisteNoEsquema(string campo) =>
        Esquema.PrimeiroSegmentoAusente(PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade)!).Should().BeNull();

    [Fact(DisplayName = "O ponteiro que o esquema não tem é apontado no primeiro segmento ausente, e o campo de nome do pedido não resolve")]
    public void PonteiroInexistente_ApontaOPrimeiroSegmentoAusente()
    {
        Esquema.PrimeiroSegmentoAusente("/cronograma/fases/2/faseCanonicaId").Should().Be("/cronograma/fases/2/faseCanonicaId");
        Esquema.PrimeiroSegmentoAusente("/etapas/0/conceito/tipo").Should().Be("/etapas/0/conceito");
        Esquema.PrimeiroSegmentoAusente("/cronograma/fases/2/tipoFaseCodigo").Should().BeNull();
        Esquema.PrimeiroSegmentoAusente("/motivos/0").Should().Be("/motivos");
        Esquema.PrimeiroSegmentoAusente("/taxaInscricao").Should().Be("/taxaInscricao");
    }

    public static TheoryData<string> Transicoes => ["Publicar", "Retificar"];

    private static string[] CaminhosAusentes(IEnumerable<FieldError> causas) =>
    [
        .. causas.Select(static causa => causa.Field).OfType<string>().Distinct(StringComparer.Ordinal)
            .Where(ponteiro => Esquema.PrimeiroSegmentoAusente(ponteiro) is not null),
    ];

    private static IEnumerable<string> DimensoesConhecidas() =>
    [
        DimensaoConformidade.Identificacao, DimensaoConformidade.AtendimentoEspecializado,
        DimensaoConformidade.DistribuicaoVagas, DimensaoConformidade.Classificacao, DimensaoConformidade.Cronograma,
        DimensaoConformidade.Etapas, DimensaoConformidade.TaxaInscricao, DimensaoConformidade.ExigenciasDocumentais,
        DimensaoConformidade.CascataRemanejamento, DimensaoConformidade.ColetaDeFatos, DimensaoConformidade.Fada,
        DimensaoConformidade.Desempate, DimensaoConformidade.ContagemDePrazos,
    ];

    private static HashSet<string> LerBlocosDoEsquema()
    {
        string texto = File.ReadAllText(Path.Join(CaminhosDoDocumentoNoEsquema.DiretorioPublicado, "documento.schema.json"));

        return [.. JsonNode.Parse(texto)!["properties"]!.AsObject().Select(static propriedade => propriedade.Key)];
    }
}
