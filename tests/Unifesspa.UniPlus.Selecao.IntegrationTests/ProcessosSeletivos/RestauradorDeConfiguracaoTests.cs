namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A prova de round-trip como <b>guard de produção</b> — não só como teste (ADR-0110).
/// </summary>
/// <remarks>
/// <para>
/// O <c>RestaurarConfiguracaoCongelada</c> do agregado valida que a versão é <b>do
/// processo</b>, mas não tem como saber que o grafo veio <b>daquela</b> versão — o Domain
/// não canonicaliza (ADR-0042). É aqui, onde o codec e o agregado coexistem, que a
/// reposição é <b>autenticada</b>: recanonicaliza-se o que foi reposto e exige-se que
/// reproduza os bytes congelados.
/// </para>
/// <para>
/// Sem esta prova, um decoder com um campo a menos repõe uma configuração empobrecida e
/// <b>ninguém fica sabendo</b> — o certame publicado passa a divergir do documento que o
/// publicou. Com ela, o descarte falha alto.
/// </para>
/// </remarks>
public sealed partial class RestauradorDeConfiguracaoTests
{
    [Fact(DisplayName = "Restaurar decodifica, repõe e PROVA — e o agregado reposto recanonicaliza nos bytes congelados")]
    public void Restaurar_ReporEProvar()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // A sessão editorial descaracterizou a configuração viva — é o que o descarte desfaz.
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        // A prova é a do restaurador; a APLICAÇÃO na raiz viva é do descarte (Story #986): limpa as
        // coleções mutáveis e repõe o grafo provado — as demais dimensões reconciliam por reuse.
        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado voltou a ser, byte a byte, o que a versão congelou");
    }

    [Fact(DisplayName = "Restaurar sobre o formulário vivo editado repõe o conteúdo congelado dele")]
    public void Restaurar_FormularioVivoEditado_RepoeOCongelado()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        string? tituloCongelado = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Titulo;
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        Result<GrafoConfiguracao> grafo = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(processo, versao);
        grafo.IsSuccess.Should().BeTrue(grafo.Error?.Message);

        processo.AbrirRetificacao("Editar o formulário", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), CorpusEnvelope.Ator, TimeProvider.System.GetUtcNow())
            .IsSuccess.Should().BeTrue();
        processo.DefinirTitulo("Título editado na sessão", PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.RestaurarConfiguracaoCongelada(versao, grafo.Value!).IsSuccess.Should().BeTrue();

        processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Titulo.Should().Be(tituloCongelado);
    }

    /// <summary>
    /// Story #575: o corpus rico (<see cref="CorpusEnvelope.ProcessoRico"/>) tem a cascata
    /// de remanejamento configurada (as 8 federais de <c>DistribuicaoLei12711</c> são
    /// <c>SegueCascata</c>, INV-12), então <see cref="Restaurar_ReporEProvar"/> já a exercita
    /// no round-trip de descarte — mas só implicitamente, via a comparação de bytes. Este
    /// teste é a prova EXPLÍCITA: a cascata reposta na raiz VIVA é a mesma que a versão
    /// congelou, não uma sombra vazia que só coincide nos bytes agregados.
    /// </summary>
    [Fact(DisplayName = "Restaurar repõe a cascata de remanejamento na raiz viva — não só nos bytes recanonicalizados")]
    public void Restaurar_ComCascataCongelada_RepoeNaRaizViva()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        processo.Cascata.Should().NotBeNull("pré-condição: o corpus rico tem a cascata configurada");
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // A sessão editorial descaracterizou a configuração viva — GrafoPobre não tem
        // cascata nenhuma. É o que o descarte tem de desfazer.
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();
        processo.Cascata.Should().BeNull("pré-condição: a sessão editorial removeu a cascata da raiz viva");

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);
        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        processo.Cascata.Should().NotBeNull(
            "a cascata voltou à raiz VIVA, não só aos bytes recanonicalizados — é isso que o comando de " +
            "definição de cascata (e qualquer leitura seguinte da configuração viva) vai enxergar");
        processo.Cascata!.FallbackCodigo.Should().Be("AC");
        processo.Cascata.Destinos.Should().HaveCount(56, "a matriz legal completa do corpus rico tem 8 origens × 7 destinos");
    }

    /// <summary>
    /// Issue #849: <c>SombraParaVerificacao()</c> copia <c>UnidadeAdministradoraOrigemId</c>/
    /// <c>UnidadeAdministradora</c> da raiz viva — sem essa cópia, a recanonicalização do bloco
    /// <c>identidadesUnidade</c> a partir da sombra (vazia) nunca bateria com o congelado, e TODA
    /// restauração de TODO processo criado com Unidade falharia com
    /// <see cref="RestauradorDeConfiguracao.RoundTripDivergente"/>. Este teste prova o round-trip
    /// de ponta a ponta e que a raiz viva preserva a identidade da Unidade — diferente da cascata,
    /// não há "GrafoPobre" que a zere: ela nunca faz parte do <see cref="GrafoConfiguracao"/>
    /// (imutável desde a criação, sem operação de re-bind).
    /// </summary>
    [Fact(DisplayName = "Restaurar prova o round-trip com Unidade administradora — a sombra reproduz a identidade congelada (issue #849)")]
    public void Restaurar_ComUnidadeAdministradora_ProvaRoundTrip()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        Guid unidadeOrigemId = processo.UnidadeAdministradoraOrigemId;
        string siglaOriginal = processo.UnidadeAdministradora.Sigla;
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);
        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(
            $"se SombraParaVerificacao() não copiasse a Unidade administradora, a recanonicalização de " +
            $"'identidadesUnidade' nunca bateria com o congelado — {resultado.Error?.Message}");

        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        processo.UnidadeAdministradoraOrigemId.Should().Be(unidadeOrigemId,
            "a identidade da Unidade administradora na raiz viva nunca sai de sincronia — é imutável desde a criação");
        processo.UnidadeAdministradora.Sigla.Should().Be(siglaOriginal);
    }

    /// <summary>
    /// Regressão: <c>Restaurar</c> montava a <see cref="EntradaCanonicalizacao"/> da prova
    /// SEM repassar <see cref="EnvelopeReidratado.Conformidade"/> — o canonicalizador recebia
    /// <see langword="null"/> e emitia <c>obrigatoriedades: []</c>, divergindo dos bytes
    /// congelados sempre que a versão carregasse regras legais avaliadas (não vazio). Este
    /// teste falha sem o campo repassado em <c>RestauradorDeConfiguracao.cs</c>.
    /// </summary>
    [Fact(DisplayName = "Restaurar repassa Conformidade adiante — a prova não diverge quando a versão congelou obrigatoriedades legais")]
    public void Restaurar_ComConformidadeCongelada_ReporEProvar()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();

        RegraAvaliada regra = new(
            RegraId: Guid.CreateVersion7(),
            RegraCodigo: "REGRA-RESTAURADOR",
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: new EtapaObrigatoria("Prova Objetiva"),
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste",
            AtoNormativoUrl: null,
            PortariaInterna: null,
            DescricaoHumana: "Regra de teste do restaurador",
            VigenciaInicio: new DateOnly(2020, 1, 1),
            VigenciaFim: null,
            Hash: new string('r', 64));
        ResultadoConformidade conformidade = new([regra], []);

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, conformidade: conformidade));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    /// <summary>
    /// O teste decisivo desta classe: um codec que <b>perde um campo</b> não passa. Sem o
    /// guard, a restauração devolveria <c>Success</c> e a configuração empobrecida seria
    /// gravada.
    /// </summary>
    [Fact(DisplayName = "Um decoder que PERDE um campo faz a restauração FALHAR — não grava configuração empobrecida")]
    public void DecoderQuePerdeCampo_Falha()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // Um registro cujo decoder devolve um grafo VÁLIDO, mas empobrecido — exatamente o
        // que um campo esquecido produziria. O agregado o aceita (é conforme); só a prova
        // de round-trip o rejeita.
        IRegistroCodecsEnvelope registroDefeituoso = Substitute.For<IRegistroCodecsEnvelope>();
        registroDefeituoso.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            CorpusEnvelope.GrafoPobre(),
            CorpusEnvelope.DadosRicos(),
            CorpusEnvelope.HashDocumento, FusoInstitucional.ZoneId,
            retificacao: null,
            conformidade: null)));
        registroDefeituoso
            .Recodificar(Arg.Any<string>(), Arg.Any<EntradaCanonicalizacao>())
            .Returns(call => CorpusEnvelope.Registro.Recodificar(
                call.Arg<string>(), call.Arg<EntradaCanonicalizacao>()));

        RestauradorDeConfiguracao restaurador = new(registroDefeituoso);

        byte[] antesDaTentativa = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsFailure.Should().BeTrue(
            "a configuração reposta não recanonicaliza nos bytes congelados — algo se perdeu. Aceitar isto faria o " +
            "certame publicado divergir do documento que o publicou, e nada acusaria.");
        resultado.Error!.Code.Should().Be(RestauradorDeConfiguracao.RoundTripDivergente);

        // A parte que importa: o agregado NÃO FOI TOCADO. Provar depois de repor deixaria a
        // raiz tracked empobrecida quando a prova falhasse, e bastaria um SaveChanges adiante
        // no mesmo escopo para gravar o estrago — a atomicidade dependeria de o handler
        // lembrar de não salvar. A prova roda sobre uma sombra destacada, antes.
        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(antesDaTentativa,
                "uma prova que falha não pode deixar resíduo no agregado — se ela repusesse primeiro e provasse " +
                "depois, este assert falharia, e o campo perdido estaria a um SaveChanges de ser persistido");
    }

    [Fact(DisplayName = "Uma versão de schema desconhecido faz a restauração falhar sem tocar no agregado")]
    public void VersaoNaoReidratavel_Falha()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao10 = VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            schemaVersion: "9.9",
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            atoCriadorHash: CorpusEnvelope.HashDocumento,
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);

        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        Result<GrafoConfiguracao> resultado = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(processo, versao10);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(antes, "uma restauração recusada não altera a configuração");
    }
}
