namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelope;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeVagas;

internal static class ExemploDeEnvelopeEntrada
{
    internal static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        RetificacaoDoEdital? retificacao = null,
        ResultadoConformidade? conformidade = null,
        bool permutarValoresSelecionaveis = false) =>
        new(processo, DadosRicos(), HashDocumento, FusoInstitucional.ZoneId, retificacao, conformidade, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: ValoresSelecionaveisRicos(permutarValoresSelecionaveis), CalendarioDiasUteis: CalendarioRico(), AgregadosDosGrupos: AgregadosRicos, ApresentacaoDosPressupostos: ApresentacaoRica, TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

    /// <summary>
    /// A apresentação de dois derivados do sistema: o município, que o domínio categórico do Geo dá como
    /// campo de município, e a faixa etária, numérica.
    /// </summary>
    internal static IReadOnlyDictionary<string, ApresentacaoDoFato> ApresentacaoRica { get; } =
        new Dictionary<string, ApresentacaoDoFato>(StringComparer.Ordinal)
        {
            ["MUNICIPIO_RESIDENCIA"] = ApresentacaoDoFato.DoCatalogo(
                "MUNICIPIO_RESIDENCIA", "Município de residência", TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Municipios),
            ["FAIXA_ETARIA"] = ApresentacaoDoFato.DoCatalogo(
                "FAIXA_ETARIA", "Faixa etária", TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, null),
        };

    /// <summary>
    /// Os agregados sobre a composição familiar do exemplo rico (ADR-0138): um de cada operação, para
    /// o envelope congelar o fato de membro e a operação de cada um junto do grupo.
    /// </summary>
    internal static IReadOnlyList<DefinicaoAgregado> AgregadosRicos { get; } =
    [
        new("RURAL_NA_FAMILIA", "COMPOSICAO_FAMILIAR", "TRABALHADOR_RURAL", OperacaoAgregado.Existe),
        new("PARENTESCOS_NA_FAMILIA", "COMPOSICAO_FAMILIAR", CandidatoComoMembro.FatoParentesco, OperacaoAgregado.ValoresPresentes),
    ];

    /// <summary>
    /// Uma <see cref="VersaoConfiguracao"/> montada com <b>ids de ato fixos</b>, para que
    /// o envelope da versão N &gt; 1 — que carrega o 18º bloco com o id do ato retificado —
    /// seja determinístico. Passar por <c>Publicar</c>/<c>Retificar</c> geraria Guid v7 a
    /// partir do relógio, e a golden fixture rica não teria como existir.
    /// </summary>
    /// <remarks>
    /// O <paramref name="ato"/> é parametrizável pela mesma razão que o id da etapa: os
    /// testes de persistência compartilham um Postgres, e
    /// <c>ux_versoes_configuracao_ato_criador</c> garante que um ato cria <b>no máximo
    /// uma</b> versão. O default é o ato fixo do envelope congelado.
    /// </remarks>
    internal static VersaoConfiguracao VersaoDeAbertura(ProcessoSeletivo processo, byte[] bytes, Guid? ato = null) =>
        VersaoConfiguracao.Abrir(
            processo.Id, new DocumentoCongelado(bytes, SnapshotPublicacaoCanonicalizer.SchemaVersionAtual, PerfilCanonicoV1.Instancia.Algoritmo, HashDocumento),
            atoCriadorId: ato ?? AtoAbertura, atorUsuarioContaId: Ator, instante: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    internal static VersaoConfiguracao VersaoDeRetificacao(VersaoConfiguracao anterior, byte[] bytes) =>
        VersaoConfiguracao.Suceder(
            anterior, new DocumentoCongelado(bytes, SnapshotPublicacaoCanonicalizer.SchemaVersionAtual, PerfilCanonicoV1.Instancia.Algoritmo, HashDocumento),
            atoCriadorId: AtoRetificador, atorUsuarioContaId: Ator, instante: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Publica o processo para que ele fique em <see cref="SituacaoDoProcessoSeletivo.Publicado"/>, com
    /// a versão congelada. O processo que cobra taxa recebe, antes, o formulário facultativo de isenção que a publicação exige.
    /// </summary>
    internal static void Publicar(ProcessoSeletivo processo, EntradaCanonicalizacao? entrada = null)
    {
        IsencaoDaTaxaDeTeste.GarantirOFormularioDeQuemCobraTaxa(processo);
        SnapshotCanonico snapshot = Canonicalizador.Canonicalizar(entrada ?? Entrada(processo));
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = DadosRicos(), Documento = new DocumentoCongelado(snapshot.Bytes, snapshot.SchemaVersion, snapshot.AlgoritmoHash, HashDocumento), AtorUsuarioContaId = Ator, Contagem = ContextoRico(), MotivosEscolhidos = MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(processo) });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.ClearDomainEvents();
    }

    /// <summary>Um termo exigido com versão fixa, para envelope determinístico.</summary>
    internal static TermoExigidoFormulario Termo(
        string codigo, int ordem, FormaAceite formaAceite = FormaAceite.RegistroSemEnderecoIp, PredicadoDnf? exibicao = null, Obrigatoriedade? obrigatoriedade = null) =>
        TermoExigidoFormulario.Criar(
            codigo,
            ordem,
            new VersaoTermoEscolhida(
                Guid.Parse("0199a000-0000-7000-8000-00000000a001"), Guid.Parse("0199a000-0000-7000-8000-00000000b001"), "TERMO_DE_TESTE", 1,
                "Declaração de veracidade", "Declaro que as informações prestadas são verdadeiras.", "Lei 9.784/1999, art. 4º",
                formaAceite, new string('a', 64)),
            exibicao,
            obrigatoriedade ?? Obrigatoriedade.Sempre).Value!;
}
