namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// A base da retificação é a configuração que o codec reidrata da versão publicada: ela carrega a importação do resultado e a primeira
/// fase congeladas, e é essa a declaração que a retificação herda, sem ler o cadastro vivo.
/// </summary>
public sealed class ImportacaoDaVersaoPublicadaNaBaseDaRetificacaoTests
{
    private static BlocosDoEnvelope Blocos(DeclaracaoDaImportacaoDoResultado? importacao) =>
        new(Configuracao(importacao), Documentos(), Publicacao(), null, [], Coleta());

    private static BlocosDaConfiguracao Configuracao(DeclaracaoDaImportacaoDoResultado? importacao) => new()
    {
        Dados = null,
        HashDocumento = new string('a', 64),
        MotivosDeDecisao = [],
        Etapas = [],
        Distribuicao = [],
        Atendimento = null,
        Fada = null,
        Cascata = null,
        Desempate = [],
        Classificacao = null,
        CronogramaFases = [],
        ImportacaoDoResultado = importacao,
    };

    private static BlocosDosDocumentos Documentos() => new()
    {
        Conformidade = null,
        DocumentosExigidos = [],
        ReferenciaTemporalFatos = null,
        MetadadosFatosCongelados = null,
        Formularios = [],
        TermosExigidos = [],
    };

    private static BlocosDaPublicacao Publicacao() => new()
    {
        Divulgacao = null,
        TaxaInscricao = null,
        Localidade = LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        FusoHorario = "America/Belem",
        NumeroDeOpcoesDeCurso = null,
        AlgoritmoContagemPrazo = null,
        CalendarioDiasUteis = null,
        IdentificadorLegivel = null,
    };

    private static BlocosDeColeta Coleta() => new()
    {
        FatosColetados = [],
        ValoresSelecionaveis = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(),
        GruposColetados = [],
        AgregadosDosGrupos = [],
        RegrasDerivacao = [],
        VersaoInterpretador = CalculoDosFatosDerivados.VersaoSemantica,
        ApresentacaoDosPressupostos = new Dictionary<string, ApresentacaoDoFato>(),
        TiposDeAtoCongelados = new Dictionary<string, TipoDeAtoCongelado>(),
        FatosDerivadosCongelados = new Dictionary<string, FatoDerivadoCongelado>(),
    };

    [Fact(DisplayName = "A configuração da base carrega a importação e a primeira fase que o envelope congelou")]
    public void Montar_CarregaAImportacaoCongelada()
    {
        Guid fase = Guid.CreateVersion7();
        DeclaracaoDaImportacaoDoResultado congelada = DeclaracaoDaImportacaoDoResultado.Declarar([new("INEP_RETORNO_ENEM", "v1")], fase).Value!;

        ConfiguracaoPublicada baseDaRetificacao = ConfiguracaoPublicadaDoEnvelope.Montar(Blocos(congelada), [], [], []);

        baseDaRetificacao.ImportacaoDoResultado.Should().BeSameAs(congelada);
        baseDaRetificacao.ImportacaoDoResultado!.PrimeiraFaseDepoisDaImportacaoId.Should().Be(fase);
    }

    [Fact(DisplayName = "O envelope sem importação congelada deixa a base sem declaração")]
    public void Montar_SemImportacao_FicaSemDeclaracao() =>
        ConfiguracaoPublicadaDoEnvelope.Montar(Blocos(null), [], [], []).ImportacaoDoResultado.Should().BeNull();
}
