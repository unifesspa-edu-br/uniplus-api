namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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
public sealed class RestauradorDeConfiguracaoTests
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

        Result resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado voltou a ser, byte a byte, o que a versão congelou");
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

        Result resultado = restaurador.Restaurar(processo, versao);

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
            CorpusEnvelope.HashDocumento,
            retificacao: null,
            conformidade: null)));
        registroDefeituoso
            .Recodificar(Arg.Any<string>(), Arg.Any<EntradaCanonicalizacao>())
            .Returns(call => CorpusEnvelope.Registro.Recodificar(
                call.Arg<string>(), call.Arg<EntradaCanonicalizacao>()));

        RestauradorDeConfiguracao restaurador = new(registroDefeituoso);

        byte[] antesDaTentativa = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        Result resultado = restaurador.Restaurar(processo, versao);

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

    [Fact(DisplayName = "Uma versão que não reidrata (1.0) faz a restauração falhar sem tocar no agregado")]
    public void VersaoNaoReidratavel_Falha()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao10 = VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            schemaVersion: "1.0",
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            atoCriadorHash: CorpusEnvelope.HashDocumento,
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);

        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        Result resultado = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(processo, versao10);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoNaoReidratavel);

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(antes, "uma restauração recusada não altera a configuração");
    }
}
